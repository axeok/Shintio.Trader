using Shintio.Trader.Models.Sandbox;

namespace Shintio.Trader.Interfaces;

public interface IStrategyManager
{
	public TradeAccount Account { get; }

	public void ProcessMarket(decimal high, decimal low);
	public void Run(decimal currentPrice, int step);
}