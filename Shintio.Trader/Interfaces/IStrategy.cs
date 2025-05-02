using Shintio.Trader.Models;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Interfaces;

public interface IStrategy
{
	public decimal InitialBalance { get; }

	public bool ValidateBalance(TradeAccount account, decimal balanceToRemove);
	public string GetLogString(TradeAccount account, decimal currentPrice, List<KlineItem> history, int i);
	
	public void Run(TradeAccount account, decimal currentPrice, List<KlineItem> history, int i);
	
}