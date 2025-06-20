﻿using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Shintio.Trader.Models;
using static System.FormattableString;

namespace Shintio.Trader.Utils;

public static class BinanceHelper
{
	public static async Task<(decimal usdt, decimal orders, decimal bnb)> FetchBalances(
		IBinanceRestClient binanceClient
	)
	{
		var balances = (await binanceClient.UsdFuturesApi.Account.GetBalancesAsync())
			.Data
			.ToArray();

		var positions = await binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync();

		var walletBalance = balances
			.Where(b => b.Asset == "USDT")
			.Sum(b => b.WalletBalance);

		var positionsMargin = positions.Data
			.Where(p => p.Quantity != 0)
			.Sum(p => Math.Abs(p.Quantity * p.EntryPrice) / p.Leverage);

		var positionsBalance = positions.Data
			.Where(p => p.Quantity != 0)
			.Sum(p => Math.Abs(p.Quantity * p.MarkPrice) / p.Leverage);

		return (
			walletBalance - positionsMargin,
			positionsBalance,
			balances.Where(b => b.Asset == "BNB")
				.Sum(b => b.WalletBalance)
		);
	}

	public static async Task<decimal> FetchUsdtBalance(IBinanceRestClient binanceClient)
	{
		return (await binanceClient.UsdFuturesApi.Account.GetBalancesAsync())
			.Data
			.Where(b => b.Asset == "USDT")
			.Sum(b => b.WalletBalance);
	}

	public static async Task<decimal> FetchBnbBalance(IBinanceRestClient binanceClient)
	{
		return (await binanceClient.UsdFuturesApi.Account.GetBalancesAsync())
			.Data
			.Where(b => b.Asset == "BNB")
			.Sum(b => b.WalletBalance);
	}

	public static async Task<IDictionary<string, decimal>> FetchOrdersPnl(
		IBinanceRestClient binanceClient,
		IReadOnlyCollection<string> pairs
	)
	{
		var positions = (await binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync())
			.Data
			.ToArray();

		return pairs.ToDictionary(
			pair => pair,
			pair => positions
				.Where(p => p.Symbol == pair)
				.Sum(p => p.UnrealizedPnl)
		);
	}

	public static async Task<decimal> FetchOrdersQuantity(
		IBinanceRestClient binanceClient,
		string pair
	)
	{
		var positions = (await binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync())
			.Data
			.ToArray();

		return positions.Where(p => p.Symbol == pair).Sum(p => p.Quantity);
	}

	public static async Task<string> TryPlaceOrder(
		IBinanceRestClient binanceClient,
		string pair,
		decimal currentPrice,
		int quantityPrecision,
		StrategyOrder order
	)
	{
		var leverageResult =
			await binanceClient.UsdFuturesApi.Account.ChangeInitialLeverageAsync(pair, (int)order.Leverage);
		if (!leverageResult.Success)
		{
			return $"Ошибка установки плеча: {leverageResult.Error}";
		}

		var quantity = Math.Round((order.Quantity * leverageResult.Data.Leverage) / currentPrice, quantityPrecision);

		var positionResult = await binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
			pair,
			order.IsShort ? OrderSide.Sell : OrderSide.Buy,
			FuturesOrderType.Market,
			quantity,
			positionSide: order.IsShort ? PositionSide.Short : PositionSide.Long
		);

		if (!positionResult.Success)
		{
			return $"Ошибка открытия ордера {order}: {positionResult.Error}";
		}

		return
			$"Открыл {(order.IsShort ? "шорт" : "лонг")} в количестве {quantity:F4} на сумму {order.Quantity:F2} с плечом x{order.Leverage:F0}";
	}

	public static async Task<string> CloseAllShorts(
		IBinanceRestClient binanceClient,
		string pair
	)
	{
		var positions = await binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync(pair);
		if (!positions.Success)
		{
			return $"Ошибка получения информации о позициях: {positions.Error}";
		}

		var shortPositions = positions.Data
			.Where(p => p.PositionSide == PositionSide.Short && p.Quantity != 0)
			.ToList();

		if (!shortPositions.Any())
		{
			return "Нет открытых шортов для закрытия";
		}

		var totalQuantity = Math.Abs(shortPositions.Sum(p => p.Quantity));
		var totalProfit = shortPositions.Sum(p => p.UnrealizedPnl);

		var orderResult = await binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
			pair,
			OrderSide.Buy,
			FuturesOrderType.Market,
			totalQuantity,
			positionSide: PositionSide.Short
		);

		return orderResult.Success
			? $"Закрыл шорты на сумму {totalQuantity:F4}. Профит: {totalProfit:F2} USDT"
			: $"Ошибка закрытия шортов: {orderResult.Error}";
	}

	public static async Task<string> CloseAllLongs(
		IBinanceRestClient binanceClient,
		string pair
	)
	{
		var positions = await binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync(pair);
		if (!positions.Success)
		{
			return $"Ошибка получения информации о позициях: {positions.Error}";
		}

		var longPositions = positions.Data
			.Where(p => p.PositionSide == PositionSide.Long && p.Quantity != 0)
			.ToList();

		if (!longPositions.Any())
		{
			return "Нет открытых лонгов для закрытия";
		}

		var totalQuantity = Math.Abs(longPositions.Sum(p => p.Quantity));
		var totalProfit = longPositions.Sum(p => p.UnrealizedPnl);
		var orderResult = await binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
			pair,
			OrderSide.Sell,
			FuturesOrderType.Market,
			totalQuantity,
			positionSide: PositionSide.Long
		);

		return orderResult.Success
			? $"Закрыл лонгов на сумму {totalQuantity:F4}. Профит: {totalProfit:F2} USDT"
			: $"Ошибка закрытия лонг позиций: {orderResult.Error}";
	}

	public static async Task<string> SetStopLoss(IBinanceRestClient binanceClient, string pair, decimal price)
	{
		var positions = await binanceClient.UsdFuturesApi.Account.GetPositionInformationAsync(pair);
		if (!positions.Success)
		{
			return $"[{pair}] Ошибка получения позиции: {positions.Error}";
		}

		var position = positions.Data.FirstOrDefault(p => p.Quantity != 0);
		if (position == null)
		{
			return $"[{pair}] Нет открытой позиции";
		}

		var openOrders = await binanceClient.UsdFuturesApi.Trading.GetOpenOrdersAsync(pair);
		if (!openOrders.Success)
		{
			return $"[{pair}] Ошибка получения ордеров: {openOrders.Error}";
		}

		var stopLoss = openOrders.Data.FirstOrDefault(o => o.Type == FuturesOrderType.StopMarket);
		if (stopLoss != null)
		{
			var cancelResult = await binanceClient.UsdFuturesApi.Trading.CancelOrderAsync(pair, stopLoss.Id);
			if (!cancelResult.Success)
			{
				return $"[{pair}] Ошибка отмены стоп-лосса: {cancelResult.Error}";
			}
		}

		var exchangeInfo = await binanceClient.UsdFuturesApi.ExchangeData.GetExchangeInfoAsync();
		var symbolInfo = exchangeInfo.Data.Symbols.First(s => s.Name == pair);
		var pricePrecision = symbolInfo.PricePrecision;

		var newStopLossPrice = Math.Round(price, pricePrecision);
		var orderSide = position.PositionSide == PositionSide.Long ? OrderSide.Sell : OrderSide.Buy;

		var newStopLoss = await binanceClient.UsdFuturesApi.Trading.PlaceOrderAsync(
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
			return $"[{pair}] Ошибка установки нового стоп-лосса: {newStopLoss.Error}";
		}

		return Invariant($"[{pair}] Стоп-лосс установлен {stopLoss?.StopPrice ?? 0:F4} -> {newStopLossPrice:F4}");
	}
}