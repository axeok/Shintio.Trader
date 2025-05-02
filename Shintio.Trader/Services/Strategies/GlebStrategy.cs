using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Strategies;

public class GlebStrategy : IStrategy
{
	public decimal InitialBalance => 20_000m;

	private decimal Quantity { get; } = 1m;
	private decimal Leverage { get; } = 50m;

	private decimal TakeProfitMultiplier { get; } = 0.003m;
	private decimal StopLossMultiplier { get; } = 0.2m;

	private int AverageCount { get; } = 60 * 60 * 24;
	private int OrderStep { get; } = 60;

	public bool ValidateBalance(TradeAccount account, decimal balanceToRemove)
	{
		return StrategyHelper.ValidateBalanceValue(account, balanceToRemove);
	}

	public string GetLogString(TradeAccount account, decimal currentPrice, List<KlineItem> history, int i)
	{
		return StrategyHelper.GetLogString(account, currentPrice);
	}

	public void Run(TradeAccount account, decimal currentPrice, List<KlineItem> history, int i)
	{
		var average = history.Skip(i - AverageCount)
			.Take(AverageCount)
			.Average(x => x.OpenPrice);

		if (i % OrderStep == 0)
		{
			account.TryOpenLong(
				currentPrice,
				Quantity,
				Leverage,
				currentPrice + 0.05m,
				average - average * StopLossMultiplier
			);

			account.TryOpenShort(
				currentPrice,
				Quantity,
				Leverage,
				currentPrice - 0.05m,
				average + average * StopLossMultiplier
			);
		}
	}
}