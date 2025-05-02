using Shintio.Trader.Models;

namespace Shintio.Trader.Utils;

public static class StrategyHelper
{
	public static bool ValidateBalanceLiquidation(TradeAccount account, decimal balanceToRemove)
	{
		return account.Balance - balanceToRemove > account.Orders.Sum(order => order.TotalQuantity);
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