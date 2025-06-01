using Shintio.Trader.Interfaces;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Models.Managers;

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