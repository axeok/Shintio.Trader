using System.Text;
using System.Text.Json;
using System.Timers;
using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shintio.Trader.Configuration;
using Shintio.Trader.Enums;
using Shintio.Trader.Models.Strategies.Skis;
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
		CurrencyPair._1000PEPE_USDT,
		CurrencyPair.WIF_USDT,
		CurrencyPair.ETH_USDT,
		CurrencyPair.NEAR_USDT,
	];

	private static readonly decimal ReservedBalance = 900;
	private static readonly decimal StopLossProfitMultiplier = 0.8m;

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
			try
			{
				await UpdateStopLoss(pair);
				await RunStrategy(pair);
			}
			catch (Exception exception)
			{
				await BotLog($"{pair}: {exception.Message}");
			}
		}

		await LogBalance();
	}

	private async Task RunStrategy(string pair)
	{
		var (usdt, ordersBalance, _) = await BinanceHelper.FetchBalances(_binanceClient);
		
		var currentPrice = (await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(pair)).Data.MarkPrice;

		var data = GetOrCreateData(pair);
		if (data.Trend != Trend.Flat && ordersBalance == 0)
		{
			await BotLog($"[{pair}] Тренд закрылся по стоп лоссу, обнуление...");
			SaveData(GetDefaultData(), pair);
			data = GetOrCreateData(pair);
		}
		
		var options = GetOrCreateOptions(pair);

		var (newData, orders, closeLongs, closeShorts) =
			SkisTradeStrategy.Run(currentPrice, usdt - ReservedBalance, data, options);

		var report = new StringBuilder();

		report.AppendLine($"[{pair}] https://www.binance.com/futures/{pair}");
		report.AppendLine();

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

		SaveData(newData, pair);

		if (closeLongs)
		{
			report.AppendLine(await BinanceHelper.CloseAllLongs(_binanceClient, pair));
		}

		if (closeShorts)
		{
			report.AppendLine(await BinanceHelper.CloseAllShorts(_binanceClient, pair));
		}

		var exchangeInfo = await _binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
		var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == pair);
		var quantityPrecision = symbolInfo.QuantityPrecision;

		foreach (var order in orders)
		{
			report.AppendLine(await BinanceHelper.TryPlaceOrder(_binanceClient, pair, currentPrice, quantityPrecision,
				order));
		}

		await BotLog(report.ToString());
	}

	private async Task CloseOrders(string pair)
	{
		var currentPrice = (await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(pair)).Data.MarkPrice;
		var orders = await BinanceHelper.FetchOrdersPnl(_binanceClient, [pair]);

		var result = orders.Select(p => Invariant($"[{p.Key}]: {p.Value:F2}"));

		var report = new StringBuilder();

		report.AppendLine(Invariant($"Текущая цена: {currentPrice:F6} {pair}"));
		report.AppendLine(string.Join("\n", result));

		report.AppendLine(await BinanceHelper.CloseAllLongs(_binanceClient, pair));
		report.AppendLine(await BinanceHelper.CloseAllShorts(_binanceClient, pair));

		await BotLog(report.ToString());
	}

	private async Task ResetStrategy(string pair)
	{
		SaveData(GetDefaultData(), pair);
		await LogParameters(pair);
	}

	private async Task UpdateStopLoss(string pair)
	{
		var data = GetOrCreateData(pair);
		if (data.Trend == Trend.Flat)
		{
			await BotLog($"[{pair}] Тренд плоский");
			return;
		}
		
		var positions = await _binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync(pair);
		if (!positions.Success)
		{
			await BotLog($"Ошибка получения информации о позициях: {positions.Error}");
			return;
		}

		if (data.Trend == Trend.Up)
		{
			var longPositions = positions.Data
				.Where(p => p.PositionSide == PositionSide.Long && p.Quantity != 0)
				.ToList();
			
			var currentPrice = (await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(pair)).Data.MarkPrice;
			
			var breakEvenPrice = longPositions.Average(p => p.BreakEvenPrice);

			if (currentPrice > breakEvenPrice)
			{
				await BotLog(await BinanceHelper.SetStopLoss(_binanceClient, pair,
					CalculateStopLossPrice(false, breakEvenPrice, currentPrice)));
			}
		}
		else if (data.Trend == Trend.Down)
		{
			var shortsPositions = positions.Data
				.Where(p => p.PositionSide == PositionSide.Short && p.Quantity != 0)
				.ToList();
			
			var currentPrice = (await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(pair)).Data.MarkPrice;
			
			var breakEvenPrice = shortsPositions.Average(p => p.BreakEvenPrice);

			if (currentPrice < breakEvenPrice)
			{
				await BotLog(await BinanceHelper.SetStopLoss(_binanceClient, pair,
					CalculateStopLossPrice(true, breakEvenPrice, currentPrice)));
			}
		}
	}

	private async Task LogBalance()
	{
		var (usdt, orders, bnb) = await BinanceHelper.FetchBalances(_binanceClient);

		await BotLog(Invariant($"Текущий баланс: {usdt:F2} + {orders:F2} = {usdt + orders:F2} USDT | {bnb:F6} BNB"));
	}

	private async Task LogOrders()
	{
		var orders = await BinanceHelper.FetchOrdersPnl(_binanceClient, Pairs);

		var result = orders.Select(p => Invariant($"[{p.Key}]: {p.Value:F2}"));

		await BotLog(string.Join("\n", result));
	}

	private async Task LogParameters(string pair)
	{
		var data = GetOrCreateData(pair);
		var options = GetOrCreateOptions(pair);

		var result = new StringBuilder();

		result.AppendLine(pair);
		result.AppendLine($"Data: {FormatData(data)}");
		result.AppendLine($"Data: {FormatOptions(options)}");

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
			case "/params":
				foreach (var p in Pairs)
				{
					await LogParameters(p);
				}

				break;
			case "/orders":
				await LogOrders();
				break;
			case "/parameters" when await ValidatePair(pair):
				await LogParameters(pair!);
				break;
			case "/run" when await ValidatePair(pair):
				await RunStrategy(pair!);
				break;
			case "/run_all":
				foreach (var p in Pairs)
				{
					await RunStrategy(p);
				}

				break;
			case "/close" when await ValidatePair(pair):
				await CloseOrders(pair!);
				await LogBalance();
				break;
			case "/close_all":
				foreach (var p in Pairs)
				{
					await CloseOrders(p);
				}

				await LogBalance();

				break;
			case "/reset" when await ValidatePair(pair):
				await ResetStrategy(pair!);
				break;
			case "/reset_all":
				foreach (var p in Pairs)
				{
					await ResetStrategy(p);
				}

				break;
			case "/update_sl" when await ValidatePair(pair):
				await UpdateStopLoss(pair!);
				break;
			case "/update_sl_all":
				foreach (var p in Pairs)
				{
					await UpdateStopLoss(p);
				}

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
		return Invariant($"{data.Trend} | {data.TrendSteps} | High {data.LastHigh:F4} | Low {data.LastLow:F4}");
	}

	private string FormatOptions(SkisOptions options)
	{
		return Invariant(
			$"{options.Quantity} | {options.Leverage} | Start {options.StartDelta:F4} | Stop {options.StopDelta:F4}");
	}

	private SkisData GetOrCreateData(string pair)
	{
		var path = GetDataPath(pair);
		if (!File.Exists(path))
		{
			return GetDefaultData();
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
			return new SkisOptions(5, 10, 0.01m, 0.04m);
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

	private SkisData GetDefaultData()
	{
		return new SkisData(Trend.Flat, 0, 0, decimal.MaxValue);
	}

	private static decimal CalculateStopLossPrice(bool isShort, decimal breakEvenPrice, decimal currentPrice)
	{
		return isShort
			? breakEvenPrice - (breakEvenPrice - currentPrice) * StopLossProfitMultiplier
			: breakEvenPrice + (currentPrice - breakEvenPrice) * StopLossProfitMultiplier;
	}
}