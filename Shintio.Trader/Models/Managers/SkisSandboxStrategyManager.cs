using Shintio.Trader.Enums;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Services.Strategies;

namespace Shintio.Trader.Models.Managers;

public class SkisSandboxStrategyManager(
	decimal initialBalance,
	decimal commissionPercent,
	SkisData initialData,
	SkisOptions options,
	int processStep
)
	: SandboxStrategyManager<SkisStrategy, SkisData, SkisOptions, StrategyResult<SkisData>>(initialBalance,
		commissionPercent,
		initialData, options)
{
	private (decimal min, decimal max) _stopLossMinUnrealizedPnl = (40m, 200m);
	private (decimal min, decimal max) _stopLossProfitMultiplier = (0.3m, 0.9m);
	
	public override void ProcessMarket(decimal high, decimal low)
	{
		base.ProcessMarket(high, low);
		
		if (Data.StopLoss == null)
		{
			return;
		}

		if (!(
			    (Data.Trend == Trend.Up && low <= Data.StopLoss.Value) ||
			    (Data.Trend == Trend.Down && high >= Data.StopLoss.Value)
		    ))
		{
			return;
		}
		
		foreach (var order in Account.Orders.ToArray())
		{
			Account.CloseOrder(order, Data.StopLoss.Value);
		}

		Data = SkisData.CreateDefault();
	}

	public override void ProcessResult(StrategyResult<SkisData> result, decimal currentPrice, int step)
	{
		base.ProcessResult(result, currentPrice, step);

		if (Data.Trend == Trend.Up)
		{
			var orders = Account.Longs.ToArray();

			var unrealizedPnl = orders.Sum(o => o.CalculateProfitQuantity(currentPrice));
			if (unrealizedPnl >= _stopLossMinUnrealizedPnl.min)
			{
				var multiplier = Map(
					unrealizedPnl,
					_stopLossMinUnrealizedPnl.min, _stopLossMinUnrealizedPnl.max,
					_stopLossProfitMultiplier.min, _stopLossProfitMultiplier.max
				);
				// multiplier = 0.8m;
				
				var breakEvenPrice = Account.GetBreakEvenPriceForOrders(orders);

				Data = Data with { StopLoss = CalculateStopLossPrice(false, breakEvenPrice, currentPrice, multiplier) };
			}
		}
		else if (Data.Trend == Trend.Down)
		{
			var orders = Account.Shorts.ToArray();

			var unrealizedPnl = orders.Sum(o => o.CalculateProfitQuantity(currentPrice));
			if (unrealizedPnl >= _stopLossMinUnrealizedPnl.min)
			{
				var multiplier = Map(
					unrealizedPnl,
					_stopLossMinUnrealizedPnl.min, _stopLossMinUnrealizedPnl.max,
					_stopLossProfitMultiplier.min, _stopLossProfitMultiplier.max
				);
				// multiplier = 0.8m;
				
				var breakEvenPrice = Account.GetBreakEvenPriceForOrders(orders);

				Data = Data with { StopLoss = CalculateStopLossPrice(true, breakEvenPrice, currentPrice, multiplier) };
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