using Shintio.Trader.Models;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Interfaces;

public interface IStrategy
{
	public virtual bool AutoProcessMarket => true;
	public virtual int RunStep => 1;
	public virtual int MaxHistoryCount => (int)TimeSpan.FromHours(6).TotalSeconds;
	
	public decimal InitialBalance { get; }

	public bool ValidateBalance(TradeAccount account, decimal balanceToRemove, decimal currentPrice);
	public string GetLogString(TradeAccount account, decimal currentPrice, IReadOnlyCollection<KlineItem> history, int i);
	
	public void Run(TradeAccount account, decimal currentPrice, IReadOnlyCollection<KlineItem> history, int i);
	
}