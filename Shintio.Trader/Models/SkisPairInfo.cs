using Shintio.Trader.Models.Strategies.Skis;

namespace Shintio.Trader.Models;

public class SkisPairInfo
{
	public SkisPairInfo(
		SkisData data,
		SkisOptions options,
		decimal stopLossMinUnrealizedPnlMin,
		decimal stopLossMinUnrealizedPnlMax,
		decimal stopLossProfitMultiplierMin,
		decimal stopLossProfitMultiplierMax
	)
	{
		Data = data;
		Options = options;
		StopLossMinUnrealizedPnlMin = stopLossMinUnrealizedPnlMin;
		StopLossMinUnrealizedPnlMax = stopLossMinUnrealizedPnlMax;
		StopLossProfitMultiplierMin = stopLossProfitMultiplierMin;
		StopLossProfitMultiplierMax = stopLossProfitMultiplierMax;
	}

	public SkisData Data { get; set; }
	public SkisOptions Options { get; set; }
	public decimal StopLossMinUnrealizedPnlMin { get; set; }
	public decimal StopLossMinUnrealizedPnlMax { get; set; }
	public decimal StopLossProfitMultiplierMin { get; set; }
	public decimal StopLossProfitMultiplierMax { get; set; }
}