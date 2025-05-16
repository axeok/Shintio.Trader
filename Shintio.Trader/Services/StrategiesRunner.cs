using Microsoft.Extensions.Logging;
using Shintio.Trader.Interfaces;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Services;

public class StrategiesRunner
{
	public delegate TData RunCollectResultDelegate<TData, TManager>(TManager manager, decimal currentPrice, int step)
		where TManager : IStrategyManager;

	public static readonly TimeSpan DefaultChunkSize = TimeSpan.FromMinutes(15);
	public static readonly TimeSpan DefaultStepSize = TimeSpan.FromMinutes(15);

	private readonly ILogger<StrategiesRunner> _logger;
	private readonly SandboxService _sandbox;

	public StrategiesRunner(ILogger<StrategiesRunner> logger, SandboxService sandbox)
	{
		_logger = logger;
		_sandbox = sandbox;
	}

	public IDictionary<TManager, IReadOnlyCollection<TData>> Run<TData, TManager>(
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

		var step = 0;
		foreach (var item in GetSteps(pair, start, end, stepSize))
		{
			var currentPrice = item.OpenPrice;

			foreach (var manager in managers)
			{
				manager.Run(currentPrice, step);

				if (step % collectStep == 0)
				{
					((List<TData>)result[manager]).Add(collectResult.Invoke(manager, currentPrice, step));
				}
			}

			step++;
		}

		return result;
	}

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

		var items = GetSteps(pair, start, end, stepSize)
			.ToArray();

		Parallel.ForEach(managers, (manager) =>
		{
			var step = 0;
			foreach (var item in items)
			{
				var currentPrice = item.OpenPrice;

				manager.Run(currentPrice, step);

				if (step % collectStep == 0)
				{
					((List<TData>)result[manager]).Add(collectResult.Invoke(manager, currentPrice, step));
				}

				step++;
			}
		});

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