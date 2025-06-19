using Shintio.Trader.Enums;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Services.Strategies;

namespace Shintio.Trader.Models.Managers;

public class SkisMultipairSandboxStrategyManager(
	decimal initialBalance,
	decimal commissionPercent,
	Dictionary<string, SkisPairInfo> pairsInfo,
	int processStep
)
	: MultipairSandboxStrategyManager<SkisStrategy>(initialBalance, commissionPercent, pairsInfo)
{
	
	public override void ProcessMarket(IReadOnlyDictionary<string, (decimal high, decimal low)> pairs)
	{
		base.ProcessMarket(pairs);

		foreach (var (pair, (high, low)) in pairs)
		{
			var data = PairsInfo[pair].Data;
			
			if (data.StopLoss == null)
			{
				continue;
			}

			if (!(
				    (data.Trend == Trend.Up && low <= data.StopLoss.Value) ||
				    (data.Trend == Trend.Down && high >= data.StopLoss.Value)
			    ))
			{
				continue;
			}

			foreach (var order in Account.GetOrders(pair).ToArray())
			{
				PairsInfo[pair].TotalPnl += Account.CloseOrder(pair, order, data.StopLoss.Value);
			}

			PairsInfo[pair].Data = SkisData.CreateDefault();
		}
	}

	public override void ProcessResult(StrategyResult<SkisData> result, string pair, decimal currentPrice, int step)
	{
		base.ProcessResult(result, pair, currentPrice, step);
		
		var info = PairsInfo[pair];
		var data = info.Data;
		
		if (data.Trend == Trend.Up)
		{
			var orders = Account.GetLongs(pair).ToArray();

			var unrealizedPnl = orders.Sum(o => o.CalculateProfitQuantity(currentPrice));
			if (unrealizedPnl >= info.StopLossMinUnrealizedPnlMin)
			{
				var multiplier = Map(
					unrealizedPnl,
					info.StopLossMinUnrealizedPnlMin, info.StopLossMinUnrealizedPnlMax,
					info.StopLossProfitMultiplierMin, info.StopLossProfitMultiplierMax
				);
				// multiplier = 0.8m;

				var breakEvenPrice = Account.GetBreakEvenPriceForOrders(orders);
				
				PairsInfo[pair].Data = data with
				{
					StopLoss = CalculateStopLossPrice(false, breakEvenPrice, currentPrice, multiplier)
				};
			}
		}
		else if (data.Trend == Trend.Down)
		{
			var orders = Account.GetShorts(pair).ToArray();

			var unrealizedPnl = orders.Sum(o => o.CalculateProfitQuantity(currentPrice));
			if (unrealizedPnl >= info.StopLossMinUnrealizedPnlMin)
			{
				var multiplier = Map(
					unrealizedPnl,
					info.StopLossMinUnrealizedPnlMin, info.StopLossMinUnrealizedPnlMax,
					info.StopLossProfitMultiplierMin, info.StopLossProfitMultiplierMax
				);
				// multiplier = 0.8m;

				var breakEvenPrice = Account.GetBreakEvenPriceForOrders(orders);
				
				PairsInfo[pair].Data = data with
				{
					StopLoss = CalculateStopLossPrice(true, breakEvenPrice, currentPrice, multiplier)
				};
			}
		}
	}

	protected override bool NeedToProcessOrders(int step)
	{
		return step % processStep == 0;
	}

	private static bool NeedToCloseStopLoss(bool isShort, decimal stopLoss, decimal high, decimal low)
	{
		return isShort ? high >= stopLoss : low <= stopLoss;
	}
	
	private static decimal CalculateStopLossPrice(
		bool isShort,
		decimal breakEvenPrice,
		decimal currentPrice,
		decimal multiplier
	)
	{
		return isShort
			? breakEvenPrice - (breakEvenPrice - currentPrice) * multiplier
			: breakEvenPrice + (currentPrice - breakEvenPrice) * multiplier;
	}

	private static decimal Map(
		decimal value,
		decimal fromSource,
		decimal toSource,
		decimal fromTarget,
		decimal toTarget
	)
	{
		return Math.Clamp(
			(value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget,
			fromTarget,
			toTarget
		);
	}
}