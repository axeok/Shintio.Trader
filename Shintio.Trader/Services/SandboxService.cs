using System.Diagnostics;
using System.Text.Json;
using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services;

public class SandboxService
{
	private static readonly TimeSpan FetchStep = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan ChunkStep = TimeSpan.FromDays(1); // do not change

	private static readonly string BasePath = "SandboxData";
	private static readonly string DateFormat = "yyyy\\/MM\\/dd";
	
	private static readonly string CachePath = Path.Combine(BasePath, "_cache");
	private static readonly string CacheDateFormat = "yyyy.MM.dd";

	private readonly ILogger<SandboxService> _logger;
	private readonly BinanceService _binanceService;
	
	public SandboxService(ILogger<SandboxService> logger, BinanceService binanceService)
	{
		_logger = logger;
		_binanceService = binanceService;
	}

	public async IAsyncEnumerable<KlineItem> FetchKlineHistory(string pair, DateTime startTime, DateTime endTime)
	{
		var totalSeconds = (int)(endTime - startTime).TotalSeconds;
		var cachePath = Path.Combine(CachePath, pair,
			$"{startTime.ToString(CacheDateFormat)}_{endTime.ToString(CacheDateFormat)}.bytes");
		if (File.Exists(cachePath))
		{
			var data = await LoadDayItems(cachePath);
			if (data.Count == totalSeconds)
			{
				foreach (var item in data)
				{
					yield return item;
				}

				yield break;
			}
		}
		
		var path = Path.Combine(BasePath, pair);

		var allItems = new List<KlineItem>(totalSeconds);
		
		var endTicks = endTime.Ticks;
		var chunkTicks = ChunkStep.Ticks;
		for (var chunk = startTime.Ticks; chunk < endTicks; chunk += chunkTicks)
		{
			var chunkStart = new DateTime(chunk);
			var chunkFileName = Path.Combine(path, $"{chunkStart.ToString(DateFormat)}.bytes");
			Directory.CreateDirectory(Path.GetDirectoryName(chunkFileName)!);

			if (File.Exists(chunkFileName))
			{
				var data = await LoadDayItems(chunkFileName);
				if (data.Count == (int)ChunkStep.TotalSeconds)
				{
					foreach (var item in data)
					{
						yield return item;
					}
			
					allItems.AddRange(data);

					continue;
				}
			}

			var items = new List<KlineItem>(ChunkStep.Seconds);

			_logger.LogInformation($"[{pair}] Fetching history for {chunkStart.Date} chunk...");
			
			var chunkEndTicks = (chunkStart + ChunkStep).Ticks;
			var stepTicks = FetchStep.Ticks;
			for (var step = chunkStart.Ticks; step < chunkEndTicks; step += stepTicks)
			{
				var stepStart = new DateTime(step);
				
				var limit = (int)FetchStep.TotalSeconds;

				var history = _binanceService.FetchKlineHistory(
					pair,
					KlineInterval.OneSecond,
					stepStart,
					stepStart + FetchStep,
					limit
				);
				await foreach (var item in history)
				{
					items.Add(item);
					yield return item;
				}
			}

			_logger.LogInformation($"[{pair}] Saving {items.Count} items for {chunkStart.Date} chunk...");
			await SaveDayItems(chunkFileName, items);
			
			allItems.AddRange(items);
		}
		
		// _logger.LogInformation($"[{pair}] Saving cache for {startTime} - {endTime}...");
		//
		// var builder = new DatabaseBuilder();
		// builder.Append(allItems);
		//
		// Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
		// await File.WriteAllBytesAsync(cachePath, builder.Build());
	}
	
	private async Task<IReadOnlyCollection<KlineItem>> LoadDayItems(string fileName)
	{
		var data = await File.ReadAllBytesAsync(fileName);

		var db = new MemoryDatabase(data);

		return db.KlineItemTable.All;
	}

	private async Task SaveDayItems(string fileName, IReadOnlyCollection<KlineItem> items)
	{
		var builder = new DatabaseBuilder();
		builder.Append(items);

		var data = builder.Build();
		await File.WriteAllBytesAsync(fileName, data);
	}
}