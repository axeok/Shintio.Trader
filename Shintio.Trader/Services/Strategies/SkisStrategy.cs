using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Strategies;

public class SkisStrategy : IStrategy
{
	private readonly decimal _quantity;
	private readonly decimal _leverage;
	private readonly decimal _maxDelta;
	private readonly decimal _minDelta;
	private readonly QuantityMultiplier _quantityMultiplier;

	public SkisStrategy(
		decimal quantity = 1m,
		decimal leverage = 10m,
		decimal maxDelta = 0.015m,
		decimal minDelta = 0.005m,
		decimal initialBalance = 2_000m,
		QuantityMultiplier quantityMultiplier = QuantityMultiplier.None,
		TimeSpan? runStep = null
	)
	{
		_quantity = quantity;
		_leverage = leverage;
		_maxDelta = maxDelta;
		_minDelta = minDelta;
		_quantityMultiplier = quantityMultiplier;

		InitialBalance = initialBalance;
		RunStep = (int)(runStep ?? TimeSpan.FromSeconds(60)).TotalSeconds;
	}

	public bool AutoProcessMarket => false;
	public int MaxHistoryCount => 1;

	public int RunStep { get; }
	public decimal InitialBalance { get; }

	private decimal _lastHigh = 0m;
	private decimal _lastLow = decimal.MaxValue;

	private Trend _trend = Trend.Flat;
	private int _trendSteps = 0;

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

		var (longs, shorts) = account.LongsAndShorts;

		if (deltaHigh >= _maxDelta && _trend != Trend.Down)
		{
			_lastLow = currentPrice;
			_trend = Trend.Flat;
			CloseOrders(account, longs, currentPrice);
			_trendSteps = 0;
		}

		if (deltaLow >= _maxDelta && _trend != Trend.Up)
		{
			_lastHigh = currentPrice;
			_trend = Trend.Flat;
			CloseOrders(account, shorts, currentPrice);
			_trendSteps = 0;
		}

		if (deltaHigh >= _minDelta && _trend == Trend.Flat)
		{
			_trend = Trend.Down;
		}

		if (deltaLow >= _minDelta && _trend == Trend.Flat)
		{
			_trend = Trend.Up;
		}

		_trendSteps++;

		var quantity = _quantityMultiplier switch
		{
			QuantityMultiplier.None => _quantity,
			QuantityMultiplier.Low => _quantity - (_trendSteps / 10m),
			QuantityMultiplier.LowQuad => _quantity - ((_trendSteps / 10m) * (_trendSteps / 10m)),
			QuantityMultiplier.High => _quantity + (_trendSteps / 10m),
			QuantityMultiplier.HighQuad => _quantity + ((_trendSteps / 10m) * (_trendSteps / 10m)),
		};
		
		var leverage = Math.Min(75, Math.Floor(_leverage + (account.Balance / 500)));
		
		switch (_trend)
		{
			case Trend.Up:
				// Console.WriteLine($"Long {currentPrice}");
				if (quantity >= 1)
				{
					account.TryOpenLong(currentPrice, quantity, leverage, null, null);
				}

				break;
			case Trend.Down:
				// Console.WriteLine($"Short {currentPrice}");
				if (quantity >= 1)
				{
					account.TryOpenShort(currentPrice, quantity, leverage, null, null);
				}

				break;
			case Trend.Flat:
				// CloseOrders(account, longs, currentPrice);
				// CloseOrders(account, shorts, currentPrice);
				break;
		}
	}

	private void CloseOrders(TradeAccount account, Order[] orders, decimal currentPrice)
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