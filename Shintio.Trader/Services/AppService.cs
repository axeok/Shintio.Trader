using System.IO.Compression;
using System.Text.Json;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Shintio.Trader.Enums;
using Shintio.Trader.Tables;
using Shintio.Trader.Utils;

namespace Shintio.Trader.Services;

public class AppService : BackgroundService
{
	private static readonly string FileName = "kline-5min.bytes";

	private readonly IHostApplicationLifetime _lifetime;
	private readonly ILogger<AppService> _logger;
	private readonly IBinanceRestClient _binanceClient;
	private readonly TelegramUserBotService _telegramUserBotService;

	public AppService(
		IHostApplicationLifetime lifetime,
		ILogger<AppService> logger,
		IBinanceRestClient binanceClient,
		TelegramUserBotService telegramUserBotService
	)
	{
		_lifetime = lifetime;
		_logger = logger;

		_binanceClient = binanceClient;
		_telegramUserBotService = telegramUserBotService;
	}

	public override Task StartAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Starting {Name}...", GetType().Name);

		return base.StartAsync(cancellationToken);
	}

	protected override async Task ExecuteAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Executing {Name}...", GetType().Name);

		await Test();
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping {Name}...", GetType().Name);

		return Task.CompletedTask;
	}

	private async Task Test()
	{
		// var result = await _binanceClient.UsdFuturesApi.Account.GetBalancesAsync();
		//
		// foreach (var balance in result.Data)
		// {
		// 	Console.WriteLine($"{balance.Asset} - {balance.WalletBalance}");
		// }

		// var parser = new MessageParser(_binanceClient, _telegramUserBotService._bot, _logger);
		//
		// var message = """
		// 	ОбновлениеAGLD/USDT M5
		// 	
		// 	Переместите стоп лос в безубыток!
		// 	
		// 	Время: 2025-04-28 19:03:21 GMT+0
		// 	""";
		//
		// await parser.Parse(message);
// 		
// 		await Task.Delay(10000);

		// var message = """
		// 	ОбновлениеEDU/USDT M5
		// 	
		// 	Переместите стоп лос в безубыток!
		// 	
		// 	Время: 2025-04-28 19:03:21 GMT+0
		// 	""";
		//
		// await parser.Parse(message);

		// var messages = JsonSerializer.Deserialize<string[]>(File.ReadAllText("messages.json"))!
		// 	.TakeLast(2)
		// 	.ToArray();
		//
		// foreach (var message in messages)
		// {
		// 	await parser.Parse(message);
		// }
	}

	// private async Task Test()
	// {
	//     // await Train();
	//     // var data = File.ReadAllBytes(FileName);
	//     //
	//     // var db = new MemoryDatabase(data);
	//     // var builder = db.ToDatabaseBuilder();
	//
	//     // await using var compressedFileStream = File.Create($"{FileName}.compressed");
	//     // await using var compressor = new DeflateStream(compressedFileStream, CompressionMode.Compress);
	//     // builder.WriteToStream(compressor);
	//
	//     await FetchData();
	//     // return;
	//     // var data = File.ReadAllBytes(FileName);
	//     //
	//     // var db = new MemoryDatabase(data);
	//     //
	//     // Console.WriteLine(string.Join("\n", db.KlineItemTable.All.Select(k => $"{k.OpenTime} - {k.CloseTime}: {GetAveragePrice(k)}")));   
	// }
	//
	// private async Task Train()
	// {
	//     var data = File.ReadAllBytes(FileName);
	//     
	//     var db = new MemoryDatabase(data);
	//     
	//     _logger.LogInformation("Data loaded");
	//
	//     var predictor = new KlinePredictor();
	//     predictor.Train(db.KlineItemTable.All.ToArray());
	// }
	//
	// private async Task FetchData()
	// {
	//     var step = TimeSpan.FromMinutes(5);
	//     var startTime = DateTime.UtcNow - TimeSpan.FromDays(365 * 5);
	//     var endTime = DateTime.UtcNow;
	//     var allKlines = new List<IBinanceKline>();
	//
	//     Directory.CreateDirectory("Data");
	//
	//     var i = 0;
	//     
	//     try
	//     {
	//         while (startTime < endTime)
	//         {
	//             var result = await _binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
	//                 CurrencyPair.ETH_USDT,
	//                 KlineInterval.FiveMinutes,
	//                 startTime,
	//                 endTime,
	//                 limit: 1000
	//             );
	//
	//             if (!result.Data.Any())
	//                 break;
	//             
	//             // File.WriteAllText($"Data/{i++}.json", JsonSerializer.Serialize(result.Data));
	//             allKlines.AddRange(result.Data);
	//
	//             startTime = result.Data.Last().OpenTime.Add(step);
	//
	//             Console.WriteLine($"{startTime} - {endTime - startTime}");
	//         }
	//     }
	//     catch (Exception ex)
	//     {
	//         _logger.LogError(ex, "{Name}", ex.Message);
	//     }
	//
	//     // Console.WriteLine(string.Join("\n", result.Data.Select(k => $"{k.OpenTime} - {k.CloseTime}: {GetAveragePrice(k)}")));
	//
	//     // var builder = new DatabaseBuilder();
	//     //
	//     // var items = allKlines.Select(k => new KlineItem
	//     // {
	//     //     OpenTime = k.OpenTime,
	//     //     CloseTime = k.CloseTime,
	//     //     OpenPrice = k.OpenPrice,
	//     //     ClosePrice = k.ClosePrice,
	//     //     LowPrice = k.LowPrice,
	//     //     HighPrice = k.HighPrice,
	//     //     Volume = k.Volume,
	//     //     BuyVolume = k.TakerBuyBaseVolume,
	//     // }).ToArray();
	//     //
	//     // builder.Append(items);
	//     //
	//     // var data = builder.Build();
	//
	//     File.WriteAllText("Data/all.json", JsonSerializer.Serialize(allKlines));
	//     
	//     Console.WriteLine($"Data saved! Count: {allKlines.Count}");
	// }
	//
	// private decimal GetAveragePrice(KlineItem kline)
	// {
	//     return (kline.ClosePrice + kline.OpenPrice + kline.HighPrice + kline.LowPrice) / 4;
	// }
}