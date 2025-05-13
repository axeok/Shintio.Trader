using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using Shintio.Trader.Models;

namespace Shintio.Trader.Utils;

public static class BinanceHelper
{
	public static async Task<(decimal usdt, decimal orders, decimal bnb)> FetchBalances(IBinanceRestClient binanceClient)
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

	public static async Task<string> TryPlaceOrder(
		IBinanceRestClient binanceClient,
		string pair,
		decimal currentPrice,
		int quantityPrecision,
		SkisOrder order
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

		return $"Открыл {(order.IsShort ? "шорт" : "лонг")} в количестве {quantity:F4} на сумму {order.Quantity:F2} с плечом x{order.Leverage:F0}";
	}
}