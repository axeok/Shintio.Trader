using Shintio.Trader.Models;
using Shintio.Trader.Models.Sandbox;

namespace Shintio.Trader.Utils;

public static class StrategyHelper
{
	public static bool ValidateBalanceLiquidation(
		TradeAccount account,
		decimal balanceToRemove,
		decimal currentPrice,
		decimal gapPercent = 0.1m
	)
	{
		var ordersBalance = account.Orders.Sum(o => o.CalculateCurrentQuantity(currentPrice));

		var balance = account.Balance + ordersBalance;

		return balance - (balance * gapPercent) - balanceToRemove > 0;
	}
	
	public static bool ValidateBalanceLiquidation(
		TradeMultipairAccount account,
		string pair,
		decimal balanceToRemove,
		decimal currentPrice,
		decimal gapPercent = 0.1m
	)
	{
		var ordersBalance = account.GetOrders(pair).Sum(o => o.CalculateCurrentQuantity(currentPrice));

		var balance = account.Balance + ordersBalance;

		return balance - (balance * gapPercent) - balanceToRemove > 0;
	}

	public static bool ValidateBalanceValue(TradeAccount account, decimal balanceToRemove, decimal value = 0)
	{
		return account.Balance - balanceToRemove > value;
	}

	public static string GetLogString(TradeAccount account, decimal currentPrice)
	{
		var (longs, shorts) = account.LongsAndShorts;

		var longsSum = longs.Sum(o => o.CalculateCurrentQuantity(currentPrice));
		var shortsSum = shorts.Sum(o => o.CalculateCurrentQuantity(currentPrice));

		return
			$"Commission: ${MoneyHelper.FormatMoney(account.PayedCommission)} | Longs: {longs.Length} - ${MoneyHelper.FormatMoney(longsSum)} | Shorts: {shorts.Length} - ${MoneyHelper.FormatMoney(shortsSum)}";
	}
}