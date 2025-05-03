namespace Shintio.Trader.Utils;

public static class MoneyHelper
{
	public static decimal GetPercent(decimal currentPrice, decimal oldPrice, decimal leverage = 1)
	{
		return ((currentPrice - oldPrice) / oldPrice) * leverage;
	}

	public static string FormatMoney(decimal value, string format = "00000")
	{
		return value.ToString("F0");
	}
}