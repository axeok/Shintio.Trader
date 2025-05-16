namespace Shintio.Trader.Interfaces;

public interface IStrategyManager
{
	public void Run(decimal currentPrice, int step);
}