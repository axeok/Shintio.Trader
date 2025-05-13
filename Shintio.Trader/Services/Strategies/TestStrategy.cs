using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Strategies;

public class TestStrategy : IStrategy
{
	public decimal InitialBalance => 10_000m;
	
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
	}
}