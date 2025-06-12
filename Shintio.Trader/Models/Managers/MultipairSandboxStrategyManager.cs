using Shintio.Trader.Interfaces;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Models.Strategies.Skis;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Models.Managers;

public class MultipairSandboxStrategyManager<TStrategy> : IMultipairStrategyManager
	where TStrategy : IStrategy<SkisData, SkisOptions, StrategyResult<SkisData>>
{
	public MultipairSandboxStrategyManager(
		decimal initialBalance,
		decimal commissionPercent,
		Dictionary<string, SkisPairInfo> pairInfo
	)
	{
		Account = new TradeMultipairAccount(initialBalance, commissionPercent, ValidateBalance);
		Strategy = Activator.CreateInstance<TStrategy>();

		PairsInfo = pairInfo;
	}

	public TradeMultipairAccount Account { get; }
	public IReadOnlyCollection<string> Pairs => PairsInfo.Keys;
	public TStrategy Strategy { get; }
	public Dictionary<string, SkisPairInfo> PairsInfo { get; }

	public virtual void ProcessMarket(IReadOnlyDictionary<string, (decimal high, decimal low)> pairs)
	{
	}

	public virtual void Run(IReadOnlyDictionary<string, decimal> pairs, int step)
	{
		foreach (var (pair, currentPrice) in pairs)
		{
			ProcessResult(Strategy.Run(currentPrice, Account.Balance, PairsInfo[pair].Data, PairsInfo[pair].Options), pair, currentPrice, step);
		}
	}

	public virtual void ProcessResult(StrategyResult<SkisData> result, string pair, decimal currentPrice, int step)
	{
		PairsInfo[pair].Data = result.Data;

		if (result.CloseLongs)
		{
			foreach (var order in Account.GetLongs(pair).ToArray())
			{
				PairsInfo[pair].TotalPnl += Account.CloseOrder(pair, order, currentPrice);
			}
		}

		if (result.CloseShorts)
		{
			foreach (var order in Account.GetShorts(pair).ToArray())
			{
				PairsInfo[pair].TotalPnl += Account.CloseOrder(pair, order, currentPrice);
			}
		}

		foreach (var order in result.OrdersToOpen)
		{
			Account.TryOpenOrder(pair, order.IsShort, currentPrice, order.Quantity, order.Leverage, null, null);
		}
	}

	protected virtual bool NeedToProcessOrders(int step)
	{
		return true;
	}

	private bool ValidateBalance(
		TradeMultipairAccount tradeAccount,
		string pair,
		decimal balanceToRemove,
		decimal currentPrice
	)
	{
		// return true;
		return tradeAccount.Balance > balanceToRemove &&
		       StrategyHelper.ValidateBalanceLiquidation(tradeAccount, pair, balanceToRemove, currentPrice);
	}
}