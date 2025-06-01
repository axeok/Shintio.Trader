using Shintio.Trader.Models.Sandbox;

namespace Shintio.Trader.Interfaces;

public interface IMultipairStrategyManager
{
	public TradeMultipairAccount Account { get; }
	
	public IReadOnlyCollection<string> Pairs { get; }

	public void ProcessMarket(IReadOnlyDictionary<string, (decimal high, decimal low)> pairs);
	public void Run(IReadOnlyDictionary<string, decimal> pairs, int step);
}