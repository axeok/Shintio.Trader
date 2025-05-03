using System.Text.Json;
using Binance.Net.Enums;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Services;

public class SandboxService
{
	public static readonly DateTime StartTime = new(2024, 10, 15);
	// public static readonly DateTime EndTime = new(2024, 10, 17);
	public static readonly DateTime EndTime = new(2025, 4, 15);
	public static readonly TimeSpan Step = TimeSpan.FromMinutes(15);

	private static readonly string BasePath = "SandboxData";
	private static readonly string DateFormat = "yyyy-MM-dd-HH-mm";

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
		Directory.CreateDirectory(path);

		var endTicks = EndTime.Ticks;
		var stepTicks = Step.Ticks;
		for (var i = StartTime.Ticks; i < endTicks; i += stepTicks)
		{
			var start = new DateTime(i);
			var fileName = Path.Combine(path, $"{start.ToString(DateFormat)}.json");

			if (File.Exists(fileName))
			{
				var data = JsonSerializer.Deserialize<KlineItem[]>(await File.ReadAllTextAsync(fileName));
				if (data != null)
				{
					foreach (var item in data)
					{
						yield return item;
					}

					continue;
				}
			}

			var limit = (int)Step.TotalSeconds;
			var items = new List<KlineItem>(limit);

			var history = _binanceService.FetchKlineHistory(
				pair,
				KlineInterval.OneSecond,
				start,
				start + Step,
				limit
			);
			await foreach (var item in history)
			{
				items.Add(item);
				yield return item;
			}

			await File.WriteAllTextAsync(fileName, JsonSerializer.Serialize(items));
		}
	}
}