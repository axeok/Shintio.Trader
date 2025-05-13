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
	
	private static readonly string DataPath = "skis-data.json";
	private static readonly string OptionsPath = "skis-options.json";

	private static readonly string Pair = CurrencyPair.DOGE_USDT;
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
		var nextHour = now.AddHours(0);
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
		
		await RunStrategy();
	}

	private async Task RunStrategy()
	{
		var (usdt, _, _) = await BinanceHelper.FetchBalances(_binanceClient);
		var currentPrice = (await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(Pair)).Data.MarkPrice;

		var data = GetOrCreateData();
		var options = GetOrCreateOptions();

		var (newData, orders, closeLongs, closeShorts) = SkisTradeStrategy.Run(currentPrice, usdt - ReservedBalance, data, options);

		var report = new StringBuilder();

		report.AppendLine(Invariant($"Свободный баланс: {usdt - ReservedBalance:F2} ({usdt:F2}) USDT"));
		report.AppendLine(Invariant($"Текущая цена: {currentPrice:F6} {Pair}"));
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

		SaveData(newData);

		if (closeLongs)
		{
			
		}

		if (closeShorts)
		{
			
		}
		
		var exchangeInfo = await _binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
		var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == Pair);
		var quantityPrecision = symbolInfo.QuantityPrecision;

		var reports = new List<string>();
		foreach (var order in orders)
		{
			reports.Add(await BinanceHelper.TryPlaceOrder(_binanceClient, Pair, currentPrice, quantityPrecision, order));
		}

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

	private async Task LogParameters()
	{
		var data = GetOrCreateData();
		var options = GetOrCreateOptions();
		
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

		switch (message.Text)
		{
			case "/ping":
				await BotLog("Живой!");
				break;
			case "/balance":
				await LogBalance();
				break;
			case "/parameters":
				await LogParameters();
				break;
			case "/run":
				await RunStrategy();
				break;
		}
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

	private SkisData GetOrCreateData()
	{
		if (!File.Exists(DataPath))
		{
			return new SkisData(Trend.Flat, 0, 0, decimal.MaxValue);
		}

		return JsonSerializer.Deserialize<SkisData>(File.ReadAllText(DataPath))!;
	}

	private void SaveData(SkisData data)
	{
		File.WriteAllText(DataPath, JsonSerializer.Serialize(data));
	}

	private SkisOptions GetOrCreateOptions()
	{
		if (!File.Exists(OptionsPath))
		{
			return new SkisOptions(5, 10, 0.010m, 0.04m);
		}

		return JsonSerializer.Deserialize<SkisOptions>(File.ReadAllText(OptionsPath))!;
	}

	private void SaveOptions(SkisOptions options)
	{
		File.WriteAllText(OptionsPath, JsonSerializer.Serialize(options));
	}
}