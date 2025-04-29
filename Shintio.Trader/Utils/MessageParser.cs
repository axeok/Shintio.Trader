using System.Globalization;
using System.Text.RegularExpressions;
using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Services;
using Telegram.Bot;

namespace Shintio.Trader.Utils;

public class MessageParser
{
	private readonly IBinanceRestClient _binanceClient;
	private readonly ITelegramBotClient _telegramBot;
	private readonly ILogger<TelegramUserBotService> _logger;

	public MessageParser(IBinanceRestClient binanceClient, ITelegramBotClient telegramBot, ILogger<TelegramUserBotService> logger)
	{
		_binanceClient = binanceClient;
		_telegramBot = telegramBot;
		_logger = logger;
	}

	public async Task Parse(string message)
	{
		try
		{
			string? pair = null;
			foreach (var row in GetRows(message))
			{
				var lower = row.ToLowerInvariant();
				if (lower.Contains("m5") || lower.Contains("м5"))
				{
					var pairMatch = Regex.Match(message, @"[A-Z0-9]+/[A-Z0-9]+");
					if (pairMatch.Success)
					{
						pair = pairMatch.Groups[0].Value;
						break;
					}
				}
			}

			if (pair == null)
			{
				Log("Не нашёл пару");
				return;
			}

			pair = pair.Replace("/", "");

			var result = await _binanceClient.UsdFuturesApi.ExchangeData.GetMarkPriceAsync(pair);
			if (result?.Data == null)
			{
				Log($"На бирже нет пары {pair}");
				return;
			}

			if (message.StartsWith("Открытие:"))
			{
				await ParseOpen(pair, message);
				return;
			}

			if (message.StartsWith("Закрытие:"))
			{
				ParseClose(pair, message);
				return;
			}

			if (message.StartsWith("Обновление"))
			{
				ParseUpdate(pair, message);
				return;
			}

			Log("Не понял сообщение");
		}
		catch (Exception e)
		{
			Log(e.Message);
		}
	}

	private async Task ParseOpen(string pair, string message)
	{
		var balance = (await _binanceClient.UsdFuturesApi.Account.GetBalancesAsync())
			.Data
			.Where(b => b.Asset == "USDT")
			.Sum(b => b.WalletBalance);
		if (balance < 150)
		{
			Log($"[{pair}] Текущий баланс ниже порога: {balance}");

			return;
		}

		var leverageResult = await _binanceClient.UsdFuturesApi.Account.ChangeInitialLeverageAsync(pair, 20);
		if (!leverageResult.Success)
		{
			Log($"[{pair}] Ошибка установки плеча: {leverageResult.Error}");
			return;
		}

		if (message.StartsWith("Открытие: SHORT"))
		{
			var price = FetchPrice(message);
			if (price == null)
			{
				return;
			}

			var takeProfit = FetchPrice(message, "Тейк профит: ")!.Value;
			var stopLoss = FetchPrice(message, "Стоп лос: ")!.Value;

			var exchangeInfo = await _binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
			var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == pair);
			var quantityPrecision = symbolInfo.QuantityPrecision;
			var pricePrecision = symbolInfo.PricePrecision;

			takeProfit = Math.Round(takeProfit, pricePrecision);
			stopLoss = Math.Round(stopLoss, pricePrecision);

			var tickerData = await _binanceClient.UsdFuturesApi.ExchangeData.GetTickerAsync(pair);
			if (!tickerData.Success)
			{
				Log($"[{pair}] Ошибка получения цены: {tickerData.Error}");
				return;
			}

			var currentPrice = tickerData.Data.LastPrice;
			var usdtAmount = 15m;
			var quantity = Math.Round((usdtAmount * leverageResult.Data.Leverage) / currentPrice, quantityPrecision);

			var positionResult = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
				pair,
				OrderSide.Sell,
				FuturesOrderType.Market,
				quantity,
				positionSide: PositionSide.Short);

			if (positionResult.Success)
			{
				var takeProfitResult = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
					pair,
					OrderSide.Buy,
					FuturesOrderType.TakeProfitMarket,
					quantity,
					stopPrice: takeProfit,
					positionSide: PositionSide.Short,
					closePosition: true
				);

				if (!takeProfitResult.Success)
				{
					Log($"[{pair}] Ошибка установки тейк профита: {takeProfitResult.Error}");
				}

				var stopLossResult = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
					pair,
					OrderSide.Buy,
					FuturesOrderType.StopMarket,
					quantity,
					stopPrice: stopLoss,
					positionSide: PositionSide.Short,
					closePosition: true
				);

				if (!stopLossResult.Success)
				{
					Log($"[{pair}] Ошибка установки стоп-лосса: {stopLossResult.Error}");
				}
			}

			Log($"[{pair}] Открываю SHORT {price} -> {takeProfit} / {stopLoss}");

			return;
		}

		if (message.StartsWith("Открытие: LONG"))
		{
			var price = FetchPrice(message);
			if (price == null)
			{
				return;
			}

			var takeProfit = FetchPrice(message, "Тейк профит: ")!.Value;
			var stopLoss = FetchPrice(message, "Стоп лос: ")!.Value;

			var exchangeInfo = await _binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
			var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == pair);
			var quantityPrecision = symbolInfo.QuantityPrecision;
			var pricePrecision = symbolInfo.PricePrecision;

			takeProfit = Math.Round(takeProfit, pricePrecision);
			stopLoss = Math.Round(stopLoss, pricePrecision);

			var tickerData = await _binanceClient.UsdFuturesApi.ExchangeData.GetTickerAsync(pair);
			if (!tickerData.Success)
			{
				Log($"[{pair}] Ошибка получения цены: {tickerData.Error}");
				return;
			}

			var currentPrice = tickerData.Data.LastPrice;
			var usdtAmount = 15m;
			var quantity = Math.Round((usdtAmount * leverageResult.Data.Leverage) / currentPrice, quantityPrecision);

			var positionResult = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
				pair,
				OrderSide.Buy,
				FuturesOrderType.Market,
				quantity,
				positionSide: PositionSide.Long);

			if (positionResult.Success)
			{
				var takeProfitResult = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
					pair,
					OrderSide.Sell,
					FuturesOrderType.TakeProfitMarket,
					quantity,
					stopPrice: takeProfit,
					positionSide: PositionSide.Long,
					closePosition: true
				);

				if (!takeProfitResult.Success)
				{
					Log($"[{pair}] Ошибка установки тейк профита: {takeProfitResult.Error}");
				}

				var stopLossResult = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
					pair,
					OrderSide.Sell,
					FuturesOrderType.StopMarket,
					quantity,
					stopPrice: stopLoss,
					positionSide: PositionSide.Long,
					closePosition: true
				);

				if (!stopLossResult.Success)
				{
					Log($"[{pair}] Ошибка установки стоп-лосса: {stopLossResult.Error}");
				}
			}

			Log($"[{pair}] Открываю LONG {price} -> {takeProfit} / {stopLoss}");

			return;
		}

		Log($"[{pair}] Неизвестное открытие");
	}

	private void ParseClose(string pair, string message)
	{
	}

	private async Task ParseUpdate(string pair, string message)
	{
		var positions = await _binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync(pair);
		if (!positions.Success)
		{
			Log($"[{pair}] Ошибка получения позиции: {positions.Error}");
			return;
		}

		var position = positions.Data.FirstOrDefault(p => p.Quantity != 0);
		if (position == null)
		{
			Log($"[{pair}] Нет открытой позиции");
			return;
		}

		var openOrders = await _binanceClient.UsdFuturesApi.Trading.GetOpenOrdersAsync(pair);
		if (!openOrders.Success)
		{
			Log($"[{pair}] Ошибка получения ордеров: {openOrders.Error}");
			return;
		}

		var stopLoss = openOrders.Data.FirstOrDefault(o => o.Type == FuturesOrderType.StopMarket);
		if (stopLoss != null)
		{
			var cancelResult = await _binanceClient.UsdFuturesApi.Trading.CancelOrderAsync(pair, stopLoss.Id);
			if (!cancelResult.Success)
			{
				Log($"[{pair}] Ошибка отмены стоп-лосса: {cancelResult.Error}");
				return;
			}
		}
		else
		{
			Log($"[{pair}] Cтоп-лосс не был найден");
		}

		var exchangeInfo = await _binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
		var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == pair);
		var pricePrecision = symbolInfo.PricePrecision;
		
		var newStopLossPrice = Math.Round(position.BreakEvenPrice, pricePrecision);
		var orderSide = position.PositionSide == PositionSide.Long ? OrderSide.Sell : OrderSide.Buy;

		var newStopLoss = await _binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
			pair,
			orderSide,
			FuturesOrderType.StopMarket,
			Math.Abs(position.Quantity),
			stopPrice: newStopLossPrice,
			positionSide: position.PositionSide,
			closePosition: true
		);

		if (!newStopLoss.Success)
		{
			Log($"[{pair}] Ошибка установки нового стоп-лосса: {newStopLoss.Error}");
			return;
		}

		Log($"[{pair}] Стоп-лосс перемещен в безубыток ({newStopLossPrice})");
	}


	private void Log(string message)
	{
		_logger.LogInformation(message);

		try
		{
			_telegramBot.SendMessage(377218029, message)
				.GetAwaiter()
				.GetResult();
		}
		catch (Exception e)
		{
			_logger.LogError(e, e.Message);
			// ignored
		}
	}

	private static decimal? FetchPrice(string message, string filter = "Цена: ")
	{
		foreach (var row in GetRows(message))
		{
			if (!row.Contains(filter))
			{
				continue;
			}

			var split = row.Split(filter);
			if (split.Length < 2)
			{
				continue;
			}

			var value = split[1].Split(" ")[0];

			if (!decimal.TryParse(value, CultureInfo.InvariantCulture, out var price))
			{
				continue;
			}

			return price;
		}

		return null;
	}

	private static IEnumerable<string> GetRows(string message)
	{
		using var reader = new StringReader(message);
		string? line;
		do
		{
			line = reader.ReadLine();
			if (line != null)
			{
				yield return line;
			}
		} while (line != null);
	}
}