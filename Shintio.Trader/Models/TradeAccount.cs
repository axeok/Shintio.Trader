namespace Shintio.Trader.Models;

public class TradeAccount
{
	public delegate bool ValidateBalanceDelegate(TradeAccount tradeAccount, decimal balanceToRemove, decimal currentPrice);

	private readonly ValidateBalanceDelegate _validateBalance;

	public TradeAccount(decimal initialBalance, decimal commissionPercent, ValidateBalanceDelegate validateBalance)
	{
		InitialBalance = initialBalance;
		Balance = initialBalance;
		MaxBalance = InitialBalance * 2;

		CommissionPercent = commissionPercent;

		_validateBalance = validateBalance;
	}

	public decimal InitialBalance { get; }
	public decimal CommissionPercent { get; }
	public decimal Balance { get; set; }
	public decimal MaxBalance { get; set; }
	// public decimal ReservedBalance { get; set; } = 0;
	public decimal PayedCommission { get; set; } = 0;

	public List<Order> Orders { get; set; } = new();
	public List<Order> PendingOrders { get; } = new();

	public TradeStatistics Statistics { get; } = new();

	public IEnumerable<Order> Longs => Orders.Where(o => !o.IsShort);
	public IEnumerable<Order> Shorts => Orders.Where(o => o.IsShort);

	public (Order[] longs, Order[] shorts) LongsAndShorts => (Longs.ToArray(), Shorts.ToArray());
	
	#region Orders

	public bool TryOpenOrder(
		bool isShort,
		decimal price,
		decimal quantity,
		decimal leverage,
		decimal? takeProfit,
		decimal? stopLoss
	)
	{
		var commission = quantity * 2 * CommissionPercent * leverage;
		var balanceToRemove = quantity + commission;
		if (!_validateBalance.Invoke(this, balanceToRemove, price))
		{
			return false;
		}

		Balance -= balanceToRemove;
		PayedCommission += commission;

		AddOrder(new Order(isShort, price, quantity, leverage, takeProfit, stopLoss));

		return true;
	}

	public bool TryOpenLong(decimal price, decimal quantity, decimal leverage, decimal? takeProfit, decimal? stopLoss)
		=> TryOpenOrder(false, price, quantity, leverage, takeProfit, stopLoss);

	public bool TryOpenShort(decimal price, decimal quantity, decimal leverage, decimal? takeProfit, decimal? stopLoss)
		=> TryOpenOrder(true, price, quantity, leverage, takeProfit, stopLoss);

	public decimal CloseOrder(Order order, decimal currentPrice)
	{
		var quantity = order.CalculateCurrentQuantity(currentPrice);

		Orders.Remove(order);

		Balance += quantity;

		if (quantity > order.TotalQuantity)
		{
			if (order.IsShort)
			{
				Statistics.Shorts.WinsCount++;
				Statistics.Shorts.WinsSum += quantity;
			}
			else
			{
				Statistics.Longs.WinsCount++;
				Statistics.Longs.WinsSum += quantity;
			}
		}
		else
		{
			if (order.IsShort)
			{
				Statistics.Shorts.LosesCount++;
				Statistics.Shorts.LosesSum += quantity;
			}
			else
			{
				Statistics.Longs.LosesCount++;
				Statistics.Longs.LosesSum += quantity;
			}
		}

		return quantity;
	}

	#endregion

	#region PendingOrders

	public void AddPendingOrder(Order order)
	{
		PendingOrders.Add(order);
	}

	public void AddPendingLong(
		decimal price,
		decimal quantity,
		decimal leverage,
		decimal? takeProfit,
		decimal? stopLoss
	)
		=> AddPendingOrder(new Order(false, price, quantity, leverage, takeProfit, stopLoss));

	public void AddPendingShort(
		decimal price,
		decimal quantity,
		decimal leverage,
		decimal? takeProfit,
		decimal? stopLoss
	)
		=> AddPendingOrder(new Order(true, price, quantity, leverage, takeProfit, stopLoss));

	public void AddOrder(Order order)
	{
		Orders.Add(order);

		if (order.IsShort)
		{
			Statistics.Shorts.TotalCount++;
			Statistics.Shorts.TotalSum += order.TotalQuantity;
		}
		else
		{
			Statistics.Longs.TotalCount++;
			Statistics.Longs.TotalSum += order.TotalQuantity;
		}
	}

	#endregion

	#region Market

	public decimal TryCloseTakeProfitAndStopLoss(
		decimal currentPrice,
		decimal? takeProfitPercent = null,
		decimal? stopLossPercent = null
	)
	{
		var result = 0m;

		foreach (var order in Orders.ToArray())
		{
			if (
				!order.NeedToClosePrice(currentPrice) &&
				!order.NeedToClosePercent(currentPrice, takeProfitPercent, stopLossPercent)
			)
			{
				continue;
			}

			result += CloseOrder(order, currentPrice);
		}

		return result;
	}

	public decimal TryOpenPendingOrders(decimal currentPrice)
	{
		var result = 0m;

		foreach (var order in PendingOrders.ToArray())
		{
			if (
				!order.NeedToOpenPrice(currentPrice) ||
				!TryOpenOrder(order.IsShort, order.Price, order.Quantity, order.Leverage, order.TakeProfitPrice,
					order.StopLossPrice)
			)
			{
				continue;
			}

			PendingOrders.Remove(order);
			result += order.Quantity;
		}

		return result;
	}

	public decimal ProcessMarket(decimal currentPrice)
	{
		return TryOpenPendingOrders(currentPrice) + TryCloseTakeProfitAndStopLoss(currentPrice);
	}

	#endregion

	#region Utils

	public decimal CalculateOrdersCurrentQuantity(decimal currentPrice)
	{
		return Orders.Sum(o => o.CalculateCurrentQuantity(currentPrice));
	}

	public decimal CalculateTotalCurrentQuantity(decimal currentPrice)
	{
		return Balance + CalculateOrdersCurrentQuantity(currentPrice);
	}

	// public decimal CalculateFullBalance(decimal currentPrice)
	// {
	// 	return ReservedBalance + CalculateTotalCurrentQuantity(currentPrice);
	// }

	#endregion
}