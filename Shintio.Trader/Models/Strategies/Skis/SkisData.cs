using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;

namespace Shintio.Trader.Models.Strategies.Skis;

public record SkisData(Trend Trend, int TrendSteps, decimal LastHigh, decimal LastLow, decimal? StopLoss = null)
	: IStrategyData
{
	public static SkisData CreateDefault()
	{
		return new SkisData(Trend.Flat, 0, 0, decimal.MaxValue, null);
	}
}