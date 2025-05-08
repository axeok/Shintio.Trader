using System.Diagnostics;
using System.Text.Json;
using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services;

public class SandboxService
{
	public static readonly DateTime StartTime = new(2025, 02, 1);
	// public static readonly DateTime EndTime = new(2024, 10, 17);
	public static readonly DateTime EndTime = new(2025, 05, 1);
	
	private static readonly TimeSpan FetchStep = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan ChunkStep = TimeSpan.FromDays(1); // do not change

	private static readonly string BasePath = "SandboxData";
	private static readonly string DateFormat = "yyyy\\/MM\\/dd";

	private readonly ILogger<SandboxService> _logger;
	private readonly BinanceService _binanceService;
	
	public SandboxService(ILogger<SandboxService> logger, BinanceService binanceService)
	{
		_logger = logger;
		_binanceService = binanceService;
	}

	public async IAsyncEnumerable<KlineItem> FetchKlineHistory(string pair)
	{
		var path = Path.Combine(BasePath, pair);
		
		var endTicks = EndTime.Ticks;
		var chunkTicks = ChunkStep.Ticks;
		for (var chunk = StartTime.Ticks; chunk < endTicks; chunk += chunkTicks)
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
		}
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