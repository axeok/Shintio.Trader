using Microsoft.Extensions.Logging;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Services;

public class StrategiesRunner
{
	public delegate TData RunCollectResultDelegate<TData, TManager>(TManager manager, decimal currentPrice, int step)
		where TManager : IStrategyManager;
	
	public delegate TData MultipairRunCollectResultDelegate<TData, TManager>(TManager manager, IReadOnlyDictionary<string, decimal> pairs, int step)
		where TManager : IMultipairStrategyManager;

	public static readonly TimeSpan DefaultChunkSize = TimeSpan.FromMinutes(15);
	public static readonly TimeSpan DefaultStepSize = TimeSpan.FromMinutes(15);

	private readonly ILogger<StrategiesRunner> _logger;
	private readonly SandboxService _sandbox;

	public StrategiesRunner(ILogger<StrategiesRunner> logger, SandboxService sandbox)
	{
		_logger = logger;
		_sandbox = sandbox;
	}

	// public IDictionary<TManager, IReadOnlyCollection<TData>> Run<TData, TManager>(
	// 	string pair,
	// 	DateTime start,
	// 	DateTime end,
	// 	TimeSpan stepSize,
	// 	int collectStep,
	// 	IReadOnlyCollection<TManager> managers,
	// 	RunCollectResultDelegate<TData, TManager> collectResult
	// )
	// 	where TManager : IStrategyManager
	// {
	// 	var totalSteps = (int)((end - start).TotalMinutes / stepSize.TotalMinutes);
	// 	var result = new Dictionary<TManager, IReadOnlyCollection<TData>>();
	//
	// 	foreach (var manager in managers)
	// 	{
	// 		result[manager] = new List<TData>(totalSteps);
	// 	}
	//
	// 	var step = 0;
	// 	foreach (var item in GetSteps(pair, start, end, stepSize))
	// 	{
	// 		var currentPrice = item.OpenPrice;
	//
	// 		foreach (var manager in managers)
	// 		{
	// 			manager.Run(currentPrice, step);
	//
	// 			if (step % collectStep == 0)
	// 			{
	// 				((List<TData>)result[manager]).Add(collectResult.Invoke(manager, currentPrice, step));
	// 			}
	// 		}
	//
	// 		step++;
	// 	}
	//
	// 	return result;
	// }

	public IDictionary<TManager, IReadOnlyCollection<TData>> RunParallel<TData, TManager>(
		string pair,
		DateTime start,
		DateTime end,
		TimeSpan stepSize,
		int collectStep,
		IReadOnlyCollection<TManager> managers,
		RunCollectResultDelegate<TData, TManager> collectResult
	)
		where TManager : IStrategyManager
	{
		var totalSteps = (int)((end - start).TotalMinutes / stepSize.TotalMinutes);
		var result = new Dictionary<TManager, IReadOnlyCollection<TData>>();

		foreach (var manager in managers)
		{
			result[manager] = new List<TData>(totalSteps);
		}

		var items = GetChunks(pair, start, end, stepSize)
			.ToArray();

		foreach (var chunk in managers.Chunk(120))
		{
			Parallel.ForEach(chunk, (manager) =>
			{
				var step = 0;
				foreach (var item in items)
				{
					var high = item.Max(i => i.HighPrice);
					var low = item.Min(i => i.LowPrice);
					var currentPrice = item.Last().OpenPrice;

					manager.ProcessMarket(high, low);

					if (
						manager.Account.Balance < 0 ||
						manager.Account.CalculateTotalCurrentQuantity(currentPrice) <= 10
					)
					{
						manager.Account.Balance = 0;
						manager.Account.Orders.Clear();
					}

					manager.Run(currentPrice, step);
						
					if (step % collectStep == 0)
					{
						((List<TData>)result[manager]).Add(collectResult.Invoke(manager, currentPrice, step));
					}

					step++;
				}
			});

			GC.Collect();
		}

		return result;
	}

	public IDictionary<TManager, IReadOnlyCollection<TData>> MultipairRunParallel<TData, TManager>(
		DateTime start,
		DateTime end,
		TimeSpan stepSize,
		int collectStep,
		IReadOnlyCollection<TManager> managers,
		MultipairRunCollectResultDelegate<TData, TManager> collectResult
	)
		where TManager : IMultipairStrategyManager
	{
		var totalSteps = (int)((end - start).TotalMinutes / stepSize.TotalMinutes);
		var result = new Dictionary<TManager, IReadOnlyCollection<TData>>();

		foreach (var manager in managers)
		{
			result[manager] = new List<TData>(totalSteps);
		}

		var pairs = managers.First().Pairs;

		var allItems = pairs.ToDictionary(
			p => p,
			p => GetChunks(p, start, end, stepSize).ToArray()
		);

		foreach (var chunk in managers.Chunk(120))
		{
			Parallel.ForEach(chunk, (manager) =>
			{
				var totalCount = allItems.First().Value.Length;
				for (var step = 0; step < totalCount; step++)
				{
					var highsAndLows = new Dictionary<string, (decimal high, decimal low)>();
					var currentPrices = new Dictionary<string, decimal>();
					
					foreach (var (pair, items) in allItems)
					{
						var item = items[step];
						
						highsAndLows[pair] = (item.Max(i => i.HighPrice), item.Min(i => i.LowPrice));
						currentPrices[pair] = item.Last().ClosePrice;
					}
					
					manager.ProcessMarket(highsAndLows);

					// if (manager.Account.CalculateTotalCurrentQuantity(currentPrices) <= 10)
					// {
					// 	((List<TData>)result[manager]).Add(collectResult.Invoke(manager, currentPrices, step));
					// 	
					// 	manager.Account.Balance = 0;
					//
					// 	// Console.WriteLine(manager.Account.Orders["NEARUSDT"].Sum(o => o.TotalQuantity));
					//
					// 	foreach (var orders in manager.Account.Orders.Values)
					// 	{
					// 		orders.Clear();
					// 	}
					//
					// 	return;
					// }

					manager.Run(currentPrices, step);

					if (step % collectStep == 0)
					{
						((List<TData>)result[manager]).Add(collectResult.Invoke(manager, currentPrices, step));
					}
				}
			});

			GC.Collect();
		}

		return result;
	}

	// public void Run<TStrategy, TData, TOptions, TResult>(
	// 	string pair,
	// 	DateTime start,
	// 	DateTime end,
	// 	TimeSpan stepSize,
	// 	IDictionary<TOptions, TData> strategies,
	// 	IterateDelegate<IStrategy<TData, TOptions, TResult>, TResult> func
	// )
	// 	where TStrategy : IStrategy<TData, TOptions, TResult>
	// 	where TData : IStrategyData
	// 	where TOptions : IStrategyOptions
	// 	where TResult : StrategyResult<TData>
	// {
	// 	var allData = strategies
	// 		.Select((d, i) => new KeyValuePair<int, TData>(i, d))
	// 		.ToDictionary();
	// 	var count = allData.Keys.Count;
	//
	// 	var step = 0;
	// 	foreach (var item in GetSteps(pair, start, end, stepSize))
	// 	{
	// 		for (var i = 0; i < count; i++)
	// 		{
	// 			var oldData = allData[i];
	//
	// 			var result = func.Invoke(item, step, oldData);
	//
	// 			allData[i] = result.Data;
	// 		}
	//
	// 		step++;
	// 	}
	// }

	public IEnumerable<IReadOnlyCollection<KlineItem>> GetChunks(
		string pair,
		DateTime start,
		DateTime end,
		TimeSpan? chunkSize = null
	)
	{
		chunkSize ??= DefaultChunkSize;

		return _sandbox.GetRange(pair, start, end)
			.Chunk((int)chunkSize.Value.TotalMinutes);
	}

	public IEnumerable<KlineItem> GetSteps(
		string pair,
		DateTime start,
		DateTime end,
		TimeSpan? stepSize = null
	)
	{
		stepSize ??= DefaultStepSize;

		return _sandbox.GetRange(pair, start, end)
			.Step((int)stepSize.Value.TotalMinutes);
	}
}