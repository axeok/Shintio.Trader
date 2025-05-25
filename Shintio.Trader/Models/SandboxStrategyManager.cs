using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Services.Strategies;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Models;

public class SandboxStrategyManager<TStrategy, TData, TOptions, TResult> : IStrategyManager
	where TStrategy : IStrategy<TData, TOptions, TResult>
	where TData : IStrategyData
	where TOptions : IStrategyOptions
	where TResult : StrategyResult<TData>
{
	public SandboxStrategyManager(
		decimal initialBalance,
		decimal commissionPercent,
		TData initialData,
		TOptions options
	)
	{
		Account = new TradeAccount(initialBalance, commissionPercent, ValidateBalance);
		Strategy = Activator.CreateInstance<TStrategy>();

		Data = initialData;
		Options = options;
	}

	public TradeAccount Account { get; }
	public TStrategy Strategy { get; }
	public TData Data { get; set; }
	public TOptions Options { get; set; }

	public virtual void ProcessMarket(decimal high, decimal low)
	{
	}

	public virtual void Run(decimal currentPrice, int step)
	{
		ProcessResult(Strategy.Run(currentPrice, Account.Balance, Data, Options), currentPrice, step);
	}

	public virtual void ProcessResult(TResult result, decimal currentPrice, int step)
	{
		Data = result.Data;

		if (result.CloseLongs)
		{
			foreach (var order in Account.Longs.ToArray())
			{
				Account.CloseOrder(order, currentPrice);
			}
		}

		if (result.CloseShorts)
		{
			foreach (var order in Account.Shorts.ToArray())
			{
				Account.CloseOrder(order, currentPrice);
			}
		}

		foreach (var order in result.OrdersToOpen)
		{
			Account.TryOpenOrder(order.IsShort, currentPrice, order.Quantity, order.Leverage, null, null);
		}
	}

	protected virtual bool NeedToProcessOrders(int step)
	{
		return true;
	}

	private bool ValidateBalance(TradeAccount tradeAccount, decimal balanceToRemove, decimal currentPrice)
	{
		return StrategyHelper.ValidateBalanceLiquidation(tradeAccount, balanceToRemove, currentPrice);
	}
}

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