namespace Shintio.Trader.Models.Sandbox;

public class TradeMultipairAccount
{
	public delegate bool ValidateBalanceDelegate(TradeMultipairAccount tradeAccount, string pair, decimal balanceToRemove, decimal currentPrice);

	private readonly ValidateBalanceDelegate _validateBalance;

	public TradeMultipairAccount(decimal initialBalance, decimal commissionPercent, ValidateBalanceDelegate validateBalance)
	{
		InitialBalance = initialBalance;
		Balance = initialBalance;
		MaxBalance = InitialBalance * 2;

		CommissionPercent = commissionPercent;

		_validateBalance = validateBalance;
	}

	public decimal LastCalculatedBalance { get; private set; } = 0;
	public decimal InitialBalance { get; }
	public decimal CommissionPercent { get; }
	public decimal Balance { get; set; }
	public decimal MaxBalance { get; set; }
	// public decimal ReservedBalance { get; set; } = 0;
	public decimal PayedCommission { get; set; } = 0;

	public Dictionary<string, List<SandboxOrder>> Orders { get; set; } = new();
	public Dictionary<string, List<SandboxOrder>> PendingOrders { get; } = new();

	public SandboxTradeStatistics Statistics { get; } = new();

	public IEnumerable<SandboxOrder> GetLongs(string pair) => GetOrders(pair).Where(o => !o.IsShort);
	public IEnumerable<SandboxOrder> GetShorts(string pair) => GetOrders(pair).Where(o => o.IsShort);

	public (SandboxOrder[] longs, SandboxOrder[] shorts) GetLongsAndShorts(string pair) => (GetLongs(pair).ToArray(), GetShorts(pair).ToArray());
	
	#region Orders

	public bool TryOpenOrder(
		string pair,
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
		if (!_validateBalance.Invoke(this, pair, balanceToRemove, price))
		{
			return false;
		}

		Balance -= balanceToRemove;
		PayedCommission += commission;

		AddOrder(pair, new SandboxOrder(isShort, price, quantity, leverage, takeProfit, stopLoss));

		return true;
	}

	public bool TryOpenLong(string pair, decimal price, decimal quantity, decimal leverage, decimal? takeProfit, decimal? stopLoss)
		=> TryOpenOrder(pair, false, price, quantity, leverage, takeProfit, stopLoss);

	public bool TryOpenShort(string pair, decimal price, decimal quantity, decimal leverage, decimal? takeProfit, decimal? stopLoss)
		=> TryOpenOrder(pair, true, price, quantity, leverage, takeProfit, stopLoss);

	public decimal CloseOrder(string pair, SandboxOrder order, decimal currentPrice)
	{
		var quantity = order.CalculateCurrentQuantity(currentPrice);

		GetOrders(pair).Remove(order);

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

	public void AddPendingOrder(string pair, SandboxOrder order)
	{
		GetPendingOrders(pair).Add(order);
	}

	public void AddPendingLong(
		string pair,
		decimal price,
		decimal quantity,
		decimal leverage,
		decimal? takeProfit,
		decimal? stopLoss
	)
		=> AddPendingOrder(pair, new SandboxOrder(false, price, quantity, leverage, takeProfit, stopLoss));

	public void AddPendingShort(
		string pair,
		decimal price,
		decimal quantity,
		decimal leverage,
		decimal? takeProfit,
		decimal? stopLoss
	)
		=> AddPendingOrder(pair, new SandboxOrder(true, price, quantity, leverage, takeProfit, stopLoss));

	public void AddOrder(string pair, SandboxOrder order)
	{
		GetOrders(pair).Add(order);

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
		string pair,
		decimal currentPrice,
		decimal? takeProfitPercent = null,
		decimal? stopLossPercent = null
	)
	{
		var result = 0m;

		foreach (var order in GetOrders(pair).ToArray())
		{
			if (
				!order.NeedToClosePrice(currentPrice) &&
				!order.NeedToClosePercent(currentPrice, takeProfitPercent, stopLossPercent)
			)
			{
				continue;
			}

			result += CloseOrder(pair, order, currentPrice);
		}

		return result;
	}

	public decimal TryOpenPendingOrders(string pair, decimal currentPrice)
	{
		var result = 0m;
		
		var pendingOrders = GetPendingOrders(pair);

		foreach (var order in pendingOrders.ToArray())
		{
			if (
				!order.NeedToOpenPrice(currentPrice) ||
				!TryOpenOrder(
					pair,
					order.IsShort,
					order.Price,
					order.Quantity,
					order.Leverage,
					order.TakeProfitPrice,
					order.StopLossPrice
				)
			)
			{
				continue;
			}

			pendingOrders.Remove(order);
			result += order.Quantity;
		}

		return result;
	}

	public decimal ProcessMarket(string pair, decimal currentPrice)
	{
		return TryOpenPendingOrders(pair, currentPrice) + TryCloseTakeProfitAndStopLoss(pair, currentPrice);
	}

	#endregion

	#region Utils

	public decimal CalculateOrdersCurrentPnL(string pair, decimal currentPrice)
	{
		return GetOrders(pair).Sum(o => o.CalculateProfitQuantity(currentPrice));
	}

	public decimal CalculateOrdersCurrentQuantity(string pair, decimal currentPrice)
	{
		return GetOrders(pair).Sum(o => o.CalculateCurrentQuantity(currentPrice));
	}

	public decimal CalculateTotalCurrentQuantity(string pair, decimal currentPrice)
	{
		return LastCalculatedBalance = Balance + CalculateOrdersCurrentQuantity(pair, currentPrice);
	}

	public decimal CalculateOrdersCurrentPnL(IReadOnlyDictionary<string, decimal> pairs)
	{
		return pairs.Sum(p => GetOrders(p.Key).Sum(o => o.CalculateProfitQuantity(p.Value)));
	}

	public decimal CalculateOrdersCurrentQuantity(IReadOnlyDictionary<string, decimal> pairs)
	{
		return pairs.Sum(p => GetOrders(p.Key).Sum(o => o.CalculateCurrentQuantity(p.Value)));
	}

	public decimal CalculateTotalCurrentQuantity(IReadOnlyDictionary<string, decimal> pairs)
	{
		return Balance + CalculateOrdersCurrentQuantity(pairs);
	}

	public decimal GetBreakEvenPriceForOrders(IReadOnlyCollection<SandboxOrder> orders)
	{
		var totalQuantityWithLeverage = 0m;
		var weightedSum = 0m;
		var totalCommission = 0m;

		foreach (var order in orders)
		{
			var quantityWithLeverage = order.Quantity * order.Leverage;
			totalQuantityWithLeverage += quantityWithLeverage;
			weightedSum += order.Price * quantityWithLeverage;
			totalCommission += order.TotalQuantity * 2 * CommissionPercent * order.Leverage;
		}

		if (totalQuantityWithLeverage == 0)
		{
			return 0;
		}

		var isShort = orders.FirstOrDefault()?.IsShort ?? false;
		var avgPrice = weightedSum / totalQuantityWithLeverage;

		var breakEvenPrice = isShort
			? avgPrice + (totalCommission / totalQuantityWithLeverage)
			: avgPrice - (totalCommission / totalQuantityWithLeverage);

		return breakEvenPrice;
	}

	// public decimal CalculateFullBalance(decimal currentPrice)
	// {
	// 	return ReservedBalance + CalculateTotalCurrentQuantity(currentPrice);
	// }

	#endregion

	public List<SandboxOrder> GetOrders(string pair)
	{
		if (Orders.TryGetValue(pair, out var orders))
		{
			return orders;
		}

		Orders[pair] = new List<SandboxOrder>();
		
		return Orders[pair];
	}

	public List<SandboxOrder> GetPendingOrders(string pair)
	{
		if (PendingOrders.TryGetValue(pair, out var orders))
		{
			return orders;
		}

		PendingOrders[pair] = new List<SandboxOrder>();
		
		return Orders[pair];
	}
}