using System.Text;
using System.Text.Json;
using System.Timers;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shintio.Trader.Configuration;
using Shintio.Trader.Enums;
using Shintio.Trader.Models;
using Shintio.Trader.Services.Strategies;
using Shintio.Trader.Utils;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Timer = System.Timers.Timer;
using static System.FormattableString;

namespace Shintio.Trader.Services.Background;

public class TraderService : BackgroundService
{
	private static readonly TimeSpan TimerInterval = TimeSpan.FromHours(1);

	private static readonly string[] Pairs =
	[
		CurrencyPair.DOGE_USDT,
		CurrencyPair.PEPE_USDT,
	];

	private static readonly decimal ReservedBalance = 300;

	private readonly ILogger<TraderService> _logger;
	private readonly ITelegramBotClient _bot;
	private readonly IBinanceRestClient _binanceClient;

	private readonly TraderConfig _traderConfig;

	private readonly Timer _timer;

	public TraderService(
		ILogger<TraderService> logger,
		ITelegramBotClient bot,
		IBinanceRestClient binanceClient,
		IOptions<TraderConfig> traderConfig
	)
	{
		_logger = logger;
		_bot = bot;
		_binanceClient = binanceClient;

		_traderConfig = traderConfig.Value;
		
		var now = DateTime.UtcNow;
		var nextHour = now.AddHours(1);
		var nextAligned = new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 15, 0);

		var delay = nextAligned - now;
		
		_timer = new Timer();
		_timer.Interval = delay.TotalMilliseconds;
		_timer.AutoReset = true;
		_timer.Elapsed += TimerOnElapsed;
	}

	public override Task StartAsync(CancellationToken cancellationToken)
	{
		var receiverOptions = new ReceiverOptions()
		{
			AllowedUpdates = Array.Empty<UpdateType>()
		};

		_bot.StartReceiving(
			updateHandler: HandleUpdateAsync,
			errorHandler: HandleErrorAsync,
			receiverOptions: receiverOptions,
			cancellationToken: cancellationToken
		);
		
		_timer.Start();

		return base.StartAsync(cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await BotLog("Перезапустился");
	}

	private async void TimerOnElapsed(object? sender, ElapsedEventArgs e)
	{
		_timer.Interval = TimeSpan.FromHours(1).TotalMilliseconds;

		foreach (var pair in Pairs)
		{
			await RunStrategy(pair);
		}
	}

	private async Task RunStrategy(string pair)
	{
		var (usdt, _, _) = await BinanceHelper.FetchBalances(_binanceClient);
		var currentPrice = (await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(pair)).Data.MarkPrice;

		var data = GetOrCreateData(pair);
		var options = GetOrCreateOptions(pair);

		var (newData, orders, closeLongs, closeShorts) = SkisTradeStrategy.Run(currentPrice, usdt - ReservedBalance, data, options);

		var report = new StringBuilder();

		report.AppendLine(Invariant($"Свободный баланс: {usdt - ReservedBalance:F2} ({usdt:F2}) USDT"));
		report.AppendLine(Invariant($"Текущая цена: {currentPrice:F6} {pair}"));
		report.AppendLine($"Старые параметры: {FormatData(data)}");
		report.AppendLine($"Новые параметры: {FormatData(newData)}");

		if (closeLongs)
		{
			report.AppendLine("Закрываем лонги");
		}

		if (closeShorts)
		{
			report.AppendLine("Закрываем шорты");
		}

		report.AppendLine();

		foreach (var order in orders)
		{
			report.AppendLine(Invariant(
				$"Открываем {(order.IsShort ? "шорт" : "лонг")} на сумму {order.Quantity:F2} с плечом x{order.Leverage:F0}"));
		}
		
		await BotLog(report.ToString());

		SaveData(newData, pair);
		
		var reports = new List<string>();

		if (closeLongs)
		{
			reports.Add(await BinanceHelper.CloseAllLongs(_binanceClient, pair));
		}

		if (closeShorts)
		{
			reports.Add(await BinanceHelper.CloseAllShorts(_binanceClient, pair));
		}
		
		var exchangeInfo = await _binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
		var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == pair);
		var quantityPrecision = symbolInfo.QuantityPrecision;

		foreach (var order in orders)
		{
			reports.Add(await BinanceHelper.TryPlaceOrder(_binanceClient, pair, currentPrice, quantityPrecision, order));
		}

		foreach (var message in reports)
		{
			await BotLog(message);
		}

		await LogBalance();
	}

	private async Task SellAll(string pair)
	{
		var reports = new List<string>();

		reports.Add(await BinanceHelper.CloseAllLongs(_binanceClient, pair));
		reports.Add(await BinanceHelper.CloseAllShorts(_binanceClient, pair));

		foreach (var message in reports)
		{
			await BotLog(message);
		}

		await LogBalance();
	}

	private async Task LogBalance()
	{
		var (usdt, orders, bnb) = await BinanceHelper.FetchBalances(_binanceClient);

		await BotLog(Invariant($"Текущий баланс: {usdt:F2} + {orders:F2} = {usdt + orders:F2} USDT | {bnb:F6} BNB"));
	}

	private async Task LogParameters(string pair)
	{
		var data = GetOrCreateData(pair);
		var options = GetOrCreateOptions(pair);
		
		var result = new StringBuilder();

		result.AppendLine(FormatData(data));
		result.AppendLine(FormatOptions(options));

		await BotLog(result.ToString());
	}

	private async Task BotLog(string message)
	{
		await _bot.SendMessage(
			_traderConfig.LogsChannelId,
			message,
			messageThreadId: _traderConfig.LogsThreadId
		);
	}

	private async Task HandleUpdateAsync(
		ITelegramBotClient botClient,
		Update update,
		CancellationToken cancellationToken
	)
	{
		await (update.Type switch
		{
			UpdateType.Message => OnMessageReceived(botClient, update.Message!, cancellationToken),
			_ => Task.CompletedTask
		});
	}

	private async Task OnMessageReceived(
		ITelegramBotClient botClient,
		Message message,
		CancellationToken cancellationToken
	)
	{
		if (message.Chat.Id != _traderConfig.LogsChannelId || message.MessageThreadId != _traderConfig.LogsThreadId)
		{
			return;
		}
		
		var split = message.Text.Split(' ');
		var command = split[0];
		var pair = split.Length < 2 ? null : split[1];

		switch (command)
		{
			case "/ping":
				await BotLog("Живой!");
				break;
			case "/balance":
				await LogBalance();
				break;
			case "/parameters" when await ValidatePair(pair):
				await LogParameters(pair!);
				break;
			case "/sell" when await ValidatePair(pair):
				await SellAll(pair!);
				break;
			case "/run" when await ValidatePair(pair):
				await RunStrategy(pair!);
				break;
		}
	}

	private async Task<bool> ValidatePair(string? pair)
	{
		if (Pairs.Contains(pair))
		{
			return true;
		}

		await BotLog("Неправильно указана валюта");

		return false;
	}
	
	private Task HandleErrorAsync(
		ITelegramBotClient botClient,
		Exception exception,
		CancellationToken cancellationToken
	)
	{
		var errorMessage = exception switch
		{
			ApiRequestException apiRequestException
				=> $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
			_ => exception.ToString()
		};

		_logger.LogError(errorMessage);

		return Task.CompletedTask;
	}

	private string FormatData(SkisData data)
	{
		return Invariant($"{data.Trend} | {data.TrendSteps} | {data.LastHigh:F4} | {data.LastLow:F4}");
	}

	private string FormatOptions(SkisOptions options)
	{
		return Invariant($"{options.Quantity} | {options.Leverage} | {options.StartDelta:F4} | {options.StopDelta:F4}");
	}

	private SkisData GetOrCreateData(string pair)
	{
		var path = GetDataPath(pair);
		if (!File.Exists(path))
		{
			return new SkisData(Trend.Flat, 0, 0, decimal.MaxValue);
		}

		return JsonSerializer.Deserialize<SkisData>(File.ReadAllText(path))!;
	}

	private void SaveData(SkisData data, string pair)
	{
		File.WriteAllText(GetDataPath(pair), JsonSerializer.Serialize(data));
	}

	private SkisOptions GetOrCreateOptions(string pair)
	{
		var path = GetOptionsPath(pair);
		if (!File.Exists(path))
		{
			return new SkisOptions(5, 10, 0.010m, 0.04m);
		}

		return JsonSerializer.Deserialize<SkisOptions>(File.ReadAllText(path))!;
	}

	private void SaveOptions(SkisOptions options, string pair)
	{
		File.WriteAllText(GetOptionsPath(pair), JsonSerializer.Serialize(options));
	}

	private string GetDataPath(string pair)
	{
		return $"skis-data-{pair}.json";
	}

	private string GetOptionsPath(string pair)
	{
		return $"skis-options-{pair}.json";
	}
}