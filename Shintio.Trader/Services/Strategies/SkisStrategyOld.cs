using Shintio.Trader.Enums;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Models;
using Shintio.Trader.Models.Sandbox;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services.Strategies;

public class SkisStrategyOld : IStrategyOld
{
	private readonly decimal _quantity;
	private readonly decimal _leverage;
	public readonly decimal _trendStartDelta;
	public readonly decimal _trendEndDelta;
	private readonly QuantityMultiplier _quantityMultiplier;

	public SkisStrategyOld(
		decimal quantity = 1m,
		decimal leverage = 10m,
		decimal trendStartDelta = 0.005m,
		decimal trendEndDelta = 0.015m,
		QuantityMultiplier quantityMultiplier = QuantityMultiplier.HighQuad,
		TimeSpan? runStep = null,
		Trend trend = Trend.Flat,
		int trendSteps = 0,
		decimal lastHigh = 0,
		decimal lastLow = decimal.MaxValue
	)
	{
		_quantity = quantity;
		_leverage = leverage;
		_trendStartDelta = trendStartDelta;
		_trendEndDelta = trendEndDelta;
		_quantityMultiplier = quantityMultiplier;
		
		_trend = trend;
		_trendSteps = trendSteps;
		_lastHigh = lastHigh;
		_lastLow = lastLow;

		RunStep = (int)(runStep ?? TimeSpan.FromHours(1)).TotalSeconds;
	}

	public bool AutoProcessMarket => false;
	public int MaxHistoryCount => 1;

	public int RunStep { get; }
	public decimal InitialBalance { get; }

	public decimal _lastHigh;
	public decimal _lastLow;

	public Trend _trend;
	public int _trendSteps = 0;

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
		// if (account.Balance > account.MaxBalance)
		// {
		// 	var reserved = account.Balance - account.MaxBalance;
		// 	account.Balance -= reserved;
		// 	account.ReservedBalance += reserved;
		// }
		
		_lastHigh = Math.Max(_lastHigh, currentPrice);
		_lastLow = Math.Min(_lastLow, currentPrice);

		var deltaHigh = (currentPrice - _lastHigh) / _lastHigh * -1;
		var deltaLow = (currentPrice - _lastLow) / _lastLow;

		var (longs, shorts) = account.LongsAndShorts;

		switch (_trend)
		{
			case Trend.Up:
				if (deltaHigh >= _trendEndDelta)
				{
					_lastLow = currentPrice;
					_trend = Trend.Flat;
					CloseOrders(account, longs, currentPrice);
					_trendSteps = 0;
				}

				break;
			case Trend.Down:
				if (deltaLow >= _trendEndDelta)
				{
					_lastHigh = currentPrice;
					_trend = Trend.Flat;
					CloseOrders(account, shorts, currentPrice);
					_trendSteps = 0;
				}

				break;
		}

		if (_trend == Trend.Flat)
		{
			if (deltaHigh >= _trendStartDelta)
			{
				_trend = Trend.Down;
			}

			if (deltaLow >= _trendStartDelta)
			{
				_trend = Trend.Up;
			}
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
		
		// var leverage = _leverage;
		var leverage = Math.Clamp(Math.Floor(_leverage + (account.Balance / 100)), 10, 75);
		
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

	private void CloseOrders(TradeAccount account, SandboxOrder[] orders, decimal currentPrice)
	{
		foreach (var order in orders)
		{
			account.CloseOrder(order, currentPrice);
		}
	}

	public enum Trend
	{
		Up,
		Down,
		Flat,
	}
}