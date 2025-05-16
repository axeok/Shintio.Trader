using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Strategies;

public class SkisStrategy2 : IStrategyOld
{
	private readonly decimal _quantity;
	private readonly decimal _leverage;
	private readonly decimal _maxDelta;
	private readonly decimal _minDelta;

	public SkisStrategy2(
		decimal quantity = 1m,
		decimal leverage = 10m,
		decimal maxDelta = 0.015m,
		decimal minDelta = 0.015m,
		decimal initialBalance = 2_000m,
		TimeSpan? runStep = null,
		TimeSpan? skipSteps = null
	)
	{
		_quantity = quantity;
		_leverage = leverage;
		_maxDelta = maxDelta;
		_minDelta = minDelta;

		InitialBalance = initialBalance;
		RunStep = (int)(runStep ?? TimeSpan.FromSeconds(10)).TotalSeconds;
		SkipSteps = (int)(skipSteps ?? TimeSpan.FromDays(30)).TotalSeconds;
	}

	public bool AutoProcessMarket => false;
	public int MaxHistoryCount => 1;

	public int RunStep { get; }
	public int OrderStep { get; } = 3;
	public int SkipSteps { get; }
	public decimal InitialBalance { get; }

	private decimal _lastHigh = 0m;
	private decimal _lastLow = decimal.MaxValue;

	private Trend _trend = Trend.Flat;

	public bool ValidateBalance(TradeAccount account, decimal balanceToRemove, decimal currentPrice)
	{
		return StrategyHelper.ValidateBalanceLiquidation(account, balanceToRemove, currentPrice);
		// return StrategyHelper.ValidateBalanceValue(account, balanceToRemove);
	}

	public string GetLogString(
		TradeAccount account,
		decimal currentPrice,
		IReadOnlyCollection<KlineItem> history,
		int i
	)
	{
		return StrategyHelper.GetLogString(account, currentPrice);
	}

	public void Run(TradeAccount account, decimal currentPrice, IReadOnlyCollection<KlineItem> history, int i)
	{
		_lastHigh = Math.Max(_lastHigh, currentPrice);
		_lastLow = Math.Min(_lastLow, currentPrice);

		var deltaHigh = (currentPrice - _lastHigh) / _lastHigh * -1;
		var deltaLow = (currentPrice - _lastLow) / _lastLow;

		if (deltaHigh >= _maxDelta && _trend != Trend.Down)
		{
			_lastLow = currentPrice;
			_trend = Trend.Down;
		}

		if (deltaLow >= _maxDelta && _trend != Trend.Up)
		{
			_lastHigh = currentPrice;
			_trend = Trend.Up;
		}

		if (i <= SkipSteps)
		{
			return;
		}

		var (longs, shorts) = account.LongsAndShorts;
		
		switch (_trend)
		{
			case Trend.Up:
				CloseOrders(account, shorts, currentPrice);
				// Console.WriteLine($"Long {currentPrice}");
				if (deltaLow >= _minDelta && (i / RunStep) % OrderStep == 0)
				{
					account.TryOpenLong(currentPrice, _quantity, _leverage, null, null);
				}

				break;
			case Trend.Down:
				CloseOrders(account, longs, currentPrice);
				// Console.WriteLine($"Short {currentPrice}");

				if (deltaHigh >= _minDelta && (i / RunStep) % OrderStep == 0)
				{
					account.TryOpenShort(currentPrice, _quantity, _leverage, null, null);
				}

				break;
			case Trend.Flat:
				// CloseOrders(account, longs, currentPrice);
				// CloseOrders(account, shorts, currentPrice);
				break;
		}
	}

	private void CloseOrders(TradeAccount account, SandboxOrder[] orders, decimal currentPrice)
	{
		foreach (var order in orders)
		{
			account.CloseOrder(order, currentPrice);
		}
	}

	private enum Trend
	{
		Up,
		Down,
		Flat,
	}
}