using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Strategies;

public class GlebStrategy : IStrategy
{
	public int RunStep => 60;
	public decimal InitialBalance => 20_000m;

	private decimal Quantity { get; } = 1m;
	private decimal Leverage { get; } = 50m;

	private decimal TakeProfitMultiplier { get; } = 0.003m;
	private decimal StopLossMultiplier { get; } = 0.2m;

	private int AverageCount { get; } = 60 * 60 * 24;

	public bool ValidateBalance(TradeAccount account, decimal balanceToRemove, decimal currentPrice)
	{
		return StrategyHelper.ValidateBalanceValue(account, balanceToRemove);
	}

	public string GetLogString(TradeAccount account, decimal currentPrice, IReadOnlyCollection<KlineItem> history, int i)
	{
		return StrategyHelper.GetLogString(account, currentPrice);
	}

	public void Run(TradeAccount account, decimal currentPrice, IReadOnlyCollection<KlineItem> history, int i)
	{
		var average = history.Skip(i - AverageCount)
			.Take(AverageCount)
			.Average(x => x.OpenPrice);

		account.TryOpenLong(
			currentPrice,
			Quantity,
			Leverage,
			null,
			null
		);

		account.TryOpenShort(
			currentPrice,
			Quantity,
			Leverage,
			null,
			null
		);
	}
}