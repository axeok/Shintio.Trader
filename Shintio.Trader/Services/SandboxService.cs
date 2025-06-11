using System.Collections.Concurrent;
using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Models;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Services;

public class SandboxService
{
	private ConcurrentDictionary<string, IReadOnlyCollection<KlineItem>> _cache = new();
	
	private static readonly string BasePath = "SandboxData";
	private static readonly string DateFormat = "yyyy.MM";

	private readonly ILogger<SandboxService> _logger;
	private readonly BinanceService _binanceService;

	public SandboxService(ILogger<SandboxService> logger, BinanceService binanceService)
	{
		_logger = logger;
		_binanceService = binanceService;
	}
	
	public IReadOnlyCollection<KlineItem> GetRange(
		string pair,
		DateTime start,
		DateTime end
	)
	{
		var range = TradeMonth.FromRange(start, end);

		var skip = (int)(start - range.First().Start).TotalMinutes;
		var take = (int)(end - start).TotalMinutes;

		return range
			.SelectMany(m => GetMonth(pair, m).GetAwaiter().GetResult())
			.Skip(skip)
			.Take(take)
			.ToArray();
	}

	public async Task<IReadOnlyCollection<KlineItem>> GetMonth(string pair, TradeMonth month)
	{
		var monthName = month.Start.ToString(DateFormat);
		var path = Path.Combine(BasePath, pair, monthName);
		var minutes = month.Minutes;
		var items = new List<KlineItem>(minutes);

		if (File.Exists(path))
		{
			var data = await LoadItems(path);
			if (data.Count == minutes)
			{
				return data;
			}
			
			items.AddRange(data);
		}

		var start = month.Start.AddMinutes(items.Count);
		var end = month.End;

		if (start > end)
		{
			return items;
		}
		
		_logger.LogInformation($"[{pair}] Fetching history for {monthName} month...");
		
		var history = _binanceService.FetchKlineHistory(
			pair,
			KlineInterval.OneMinute,
			start,
			end
		);
		
		items.AddRange(await history.ToArrayAsync());

		_logger.LogInformation($"[{pair}] Saving {items.Count} items for {monthName} month...");
		
		await SaveItems(path, items);
		
		return items;
	}

	public async IAsyncEnumerable<KlineItem> FetchKlineHistory(string pair, DateTime startTime, DateTime endTime)
	{
		yield return new KlineItem
		{
			ClosePrice = 0,
			CloseTime = DateTime.Now,
			HighPrice = 0,
			LowPrice = 0,
			OpenPrice = 0,
			OpenTime = DateTime.Now,
			TakerBuyBaseVolume = 0,
			TradeCount = 0,
			Volume = 0,
		};
		// var totalMinutes = (int)(endTime - startTime).TotalMinutes;
		//
		// var path = Path.Combine(BasePath, pair);
		//
		// var allItems = new List<KlineItem>(totalMinutes);
		//
		// var endTicks = endTime.Ticks;
		// var chunkTicks = ChunkStep.Ticks;
		// for (var chunk = startTime.Ticks; chunk < endTicks; chunk += chunkTicks)
		// {
		// 	var chunkStart = new DateTime(chunk);
		// 	var chunkFileName = Path.Combine(path, $"{chunkStart.ToString(DateFormat)}.bytes");
		// 	Directory.CreateDirectory(Path.GetDirectoryName(chunkFileName)!);
		//
		// 	if (File.Exists(chunkFileName))
		// 	{
		// 		var data = await LoadDayItems(chunkFileName);
		// 		if (data.Count == (int)ChunkStep.TotalSeconds)
		// 		{
		// 			foreach (var item in data)
		// 			{
		// 				yield return item;
		// 			}
		//
		// 			allItems.AddRange(data);
		//
		// 			continue;
		// 		}
		// 	}
		//
		// 	var items = new List<KlineItem>(ChunkStep.Seconds);
		//
		// 	_logger.LogInformation($"[{pair}] Fetching history for {chunkStart.Date} chunk...");
		//
		// 	var chunkEndTicks = (chunkStart + ChunkStep).Ticks;
		// 	var stepTicks = FetchStep.Ticks;
		// 	for (var step = chunkStart.Ticks; step < chunkEndTicks; step += stepTicks)
		// 	{
		// 		var stepStart = new DateTime(step);
		//
		// 		var history = _binanceService.FetchKlineHistory(
		// 			pair,
		// 			KlineInterval.OneMinute,
		// 			stepStart,
		// 			stepStart + FetchStep
		// 		);
		// 		await foreach (var item in history)
		// 		{
		// 			items.Add(item);
		// 			yield return item;
		// 		}
		// 	}
		//
		// 	_logger.LogInformation($"[{pair}] Saving {items.Count} items for {chunkStart.Date} chunk...");
		// 	await SaveDayItems(chunkFileName, items);
		//
		// 	allItems.AddRange(items);
		// }
		//
		// // _logger.LogInformation($"[{pair}] Saving cache for {startTime} - {endTime}...");
		// //
		// // var builder = new DatabaseBuilder();
		// // builder.Append(allItems);
		// //
		// // Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
		// // await File.WriteAllBytesAsync(cachePath, builder.Build());
	}

	private async Task<IReadOnlyCollection<KlineItem>> LoadItems(string fileName)
	{
		if (_cache.TryGetValue(fileName, out var result))
		{
			return result;
		}
		
		var data = await File.ReadAllBytesAsync(fileName);

		var db = new MemoryDatabase(data);

		return _cache[fileName] = db.KlineItemTable.All;
	}

	private async Task SaveItems(string fileName, IReadOnlyCollection<KlineItem> items)
	{
		var builder = new DatabaseBuilder();
		builder.Append(items);

		var data = builder.Build();
		
		Directory.CreateDirectory(Path.GetDirectoryName(fileName)!);
		await File.WriteAllBytesAsync(fileName, data);
	}
}