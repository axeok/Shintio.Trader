using System.Text.Json;
using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Enums;

namespace Shintio.Trader.Services.Background;

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

		await Test2();
	}

	public override Task StopAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Stopping {Name}...", GetType().Name);

		return Task.CompletedTask;
	}

	// private record Order(bool IsShort, decimal Price, decimal Quantity, decimal Leverage)
	// {
	// 	public decimal TotalQuantity => Quantity * Leverage;
	//
	// 	public decimal CalculatePercent(decimal currentPrice)
	// 	{
	// 		return (currentPrice - Price) / Price * (IsShort ? -1 : 1) * Leverage;
	// 	}
	//
	// 	public decimal CalculateProfit(decimal currentPrice)
	// 	{
	// 		return Quantity * CalculatePercent(currentPrice);
	// 	}
	//
	// 	public decimal CalculateQuantity(decimal currentPrice)
	// 	{
	// 		return Quantity + CalculateProfit(currentPrice);
	// 	}
	//
	// 	public bool NeedToClosePercent(decimal currentPrice, decimal? takeProfit, decimal? stopLoss)
	// 	{
	// 		var percent = CalculatePercent(currentPrice);
	//
	// 		return (takeProfit != null && percent >= takeProfit) ||
	// 		       (stopLoss != null && percent <= -stopLoss);
	// 	}
	//
	// 	public bool NeedToCloseFlat(decimal currentPrice, decimal? takeProfit, decimal? stopLoss)
	// 	{
	// 		return (takeProfit != null && currentPrice >= takeProfit) ||
	// 		       (stopLoss != null && currentPrice <= stopLoss);
	// 	}
	// };

	private record Order(bool IsShort, decimal Price, decimal Quantity, decimal Leverage, decimal TakeProfitPrice, decimal StopLossPrice)
	{
		public decimal TotalQuantity => Quantity * Leverage;

		public decimal CalculatePercent(decimal currentPrice)
		{
			return (currentPrice - Price) / Price * (IsShort ? -1 : 1) * Leverage;
		}

		public decimal CalculateProfit(decimal currentPrice)
		{
			return Quantity * CalculatePercent(currentPrice);
		}

		public decimal CalculateQuantity(decimal currentPrice)
		{
			return Quantity + CalculateProfit(currentPrice);
		}

		public bool NeedToClosePercent(decimal currentPrice, decimal? takeProfit, decimal? stopLoss)
		{
			var percent = CalculatePercent(currentPrice);

			return (takeProfit != null && percent >= takeProfit) ||
			       (stopLoss != null && percent <= -stopLoss);
		}

		public bool NeedToCloseFlat(decimal currentPrice)
		{
			return IsShort
				? currentPrice <= TakeProfitPrice || currentPrice >= StopLossPrice
				: currentPrice >= TakeProfitPrice || currentPrice <= StopLossPrice;
		}
	};

	private void TryCompleteOrders(
		ref decimal balance,
		List<Order> orders,
		decimal currentPrice,
		decimal? takeProfit,
		decimal? stopLoss
	)
	{
		foreach (var order in orders.ToArray())
		{
			if (!order.NeedToClosePercent(currentPrice, takeProfit, stopLoss))
			{
				continue;
			}

			var percent = order.CalculatePercent(currentPrice);
			var profit = order.CalculateProfit(currentPrice);

			balance += order.CalculateQuantity(currentPrice);

			orders.Remove(order);

			if (profit > 0)
			{
				// _logger.LogInformation(
				// 	$"Close: {order} -> {currentPrice}: {Math.Round(percent * 100, 2)}% = ${profit}");
			}
		}
	}

	private void TryCompleteOrders(
		ref decimal balance,
		List<Order> orders,
		decimal currentPrice
	)
	{
		foreach (var order in orders.ToArray())
		{
			if (!order.NeedToCloseFlat(currentPrice))
			{
				continue;
			}

			var percent = order.CalculatePercent(currentPrice);
			var profit = order.CalculateProfit(currentPrice);

			balance += order.CalculateQuantity(currentPrice);

			orders.Remove(order);

			if (profit > 0)
			{
				// _logger.LogInformation(
				// 	$"Close: {order} -> {currentPrice}: {Math.Round(percent * 100, 2)}% = ${profit}");
			}
		}
	}

	private async Task Test2()
	{
		// _logger.LogInformation("Loading data...");
		//
		// var data = File.ReadAllBytes("BinanceData/SOLUSDT/OneMinute/all.bytes");
		// // var data = File.ReadAllBytes("BinanceData/OneSecond/all.bytes");
		// var db = new MemoryDatabase(data);
		// var count = db.KlineItemTable.Count;
		//
		// _logger.LogInformation($"Loaded {count} items");

		var allKlines = new List<IBinanceKline>();
		
		var step = TimeSpan.FromSeconds(1);
		var range = TimeSpan.FromDays(365);
		var interval = KlineInterval.OneSecond;
		var pair = CurrencyPair.SOL_USDT;
		
		var startTime = DateTime.UtcNow - range;
		var endTime = DateTime.UtcNow;

		var quantity = 1m;
		var leverage = 50m;
		var comissionPercent = 0.0005m;
		var averageCount = 60 * 60 * 24;
		var initalBalance = 20_000m;
		var orderStep = 60;
		var takeProfitMultiplier = 0.003m;
		var stopLossMultiplier = 0.2m;
		var logStep = (int)TimeSpan.FromDays(1).TotalSeconds;

		var balance = initalBalance;
		var comission = 0m;

		var longs = new List<Order>();
		var shorts = new List<Order>();

		var currentPrice = 0m;
		var i = -1;
		try
		{
			while (startTime < endTime)
			{
				var result = await _binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
					pair,
					interval,
					startTime,
					endTime,
					limit: 1000
				);

				if (!result.Data.Any())
					break;
				
				foreach (var item in result.Data)
				{
					i++;
					
					allKlines.Add(item);

					var average = allKlines.Skip(i - averageCount)
						.Take(averageCount)
						.Average(x => x.OpenPrice);

					currentPrice = item.OpenPrice;
					
					if (i % logStep == 0)
					{
						var ordersBalance = longs.Sum(o => o.CalculateQuantity(currentPrice))
						                    + shorts.Sum(o => o.CalculateQuantity(currentPrice));

						_logger.LogInformation(
							$"{item.OpenTime} - ${currentPrice} - ${average} - {longs.Count}L - {shorts.Count}S - ${comission:F2} - ${balance:F0} + ${ordersBalance:F0} = ${balance + ordersBalance:F0}");
					}

					var high = item.HighPrice;
					var low = item.LowPrice;

					TryCompleteOrders(ref balance, longs, currentPrice);
					TryCompleteOrders(ref balance, shorts, currentPrice);

					if (i % orderStep == 0)
					{
						var deposit = quantity * 2 + quantity * comissionPercent * 2 * leverage;
						var ordersSum = longs.Sum(o => o.TotalQuantity) + shorts.Sum(o => o.TotalQuantity);
						var outOfBalance = balance <= ordersSum;
						// if (outOfBalance)
						if (balance <= deposit)
						{
							// _logger.LogWarning($"[{i}/{range}] Out of balance: ${balance}");
							continue;
						}

						comission += comissionPercent * 2 * leverage;
						balance -= deposit;

						// var isUp = currentPrice > average;
						// var add = !isUp ? 0.5m : -0.5m;

						longs.Add(new Order(false, currentPrice, quantity, leverage,
							currentPrice + 0.05m,
							average - average * stopLossMultiplier));
						shorts.Add(new Order(true, currentPrice, quantity, leverage,
							currentPrice - 0.05m,
							average + average * stopLossMultiplier));

						// _logger.LogInformation($"[{i}/{range}] Current balance: ${balance}");
					}
				}
				
				startTime = result.Data.Last().OpenTime.Add(step);
			}
		}
		catch (Exception ex)
		{
		}

		var unclosedLongs = longs.Sum(o => o.Quantity);
		var unclosedShorts = shorts.Sum(o => o.Quantity);
		var unclosedBalance = (unclosedLongs + unclosedShorts);

		var closeBalance = 0m;
		foreach (var order in longs)
		{
			// _logger.LogInformation($"{currentPrice} -> {order} -> {profit} -> {order.Quantity + profit}");

			closeBalance += order.CalculateQuantity(currentPrice);
		}

		foreach (var order in shorts)
		{
			closeBalance += order.CalculateQuantity(currentPrice);
		}

		_logger.LogInformation(
			$"Unclosed orders: ${unclosedLongs}/{longs.Count} longs + ${unclosedShorts}/{shorts.Count} shorts -> ${unclosedBalance}");
		_logger.LogInformation(
			$"Total result: {balance} - {initalBalance} + {closeBalance} = {balance - initalBalance + closeBalance}");
	}

// 	private async Task Test()
// 	{
// 		// await FetchData();
// 		
// 		_logger.LogInformation("Loading data...");
// 		
// 		var data = File.ReadAllBytes("BinanceData/SOLUSDT/OneMinute/all.bytes");
// 		// var data = File.ReadAllBytes("BinanceData/OneSecond/all.bytes");
// 		var db = new MemoryDatabase(data);
// 		var count = db.KlineItemTable.Count;
// 		
// 		_logger.LogInformation($"Loaded {count} items");
// 		
// 		_logger.LogInformation(db.KlineItemTable.All.First.ToString());
// 		_logger.LogInformation(db.KlineItemTable.All.Last.ToString());
// 		
// 		var quantity = 1m;
// 		var leverage = 10m;
// 		var comissionPercent = 0.0005m;
// 		var timeStep = 1;
// 		var orderStep = 60;
// 		var maxDelta = 0.2m;
// 		var takeProfit = 0.05m;
// 		var stopLoss = 0.1m;
// 		var initalBalance = 2_000m;
// 		var logStep = (int)TimeSpan.FromDays(1).TotalMinutes;
// 		var orderDelta = 1m;
// 		var averageCount = 25;
// 		
// 		var balance = initalBalance;
// 		
// 		var longs = new List<Order>();
// 		var shorts = new List<Order>();
// 		
// 		var currentPrice = 0m;
// 		
// 		var lastHigh = 0m;
// 		var lastLow = decimal.MaxValue;
//
// 		var average = 0m;
// 		
// 		var range = count;
// 		// var range = TimeSpan.FromDays(7 * 4).TotalMinutes;
// 		for (var i = 0; i < range; i++)
// 		{
// 			if (i % logStep == 0)
// 			{
// 				var ordersBalance = longs.Sum(o => o.CalculateQuantity(currentPrice))
// 				                    + shorts.Sum(o => o.CalculateQuantity(currentPrice));
// 				
// 				_logger.LogInformation(
// 					$"[{i / logStep}/{(int)(range / logStep)}] {db.KlineItemTable.All[i].OpenTime} - ${currentPrice} - ${balance:F0} + ${ordersBalance:F0} = ${balance + ordersBalance:F0}");
// 			}
// 			
// 			var item = db.KlineItemTable.All[i];
//
// 			average = db.KlineItemTable.All.Skip(i - averageCount)
// 				.Take(averageCount)
// 				.Average(x => x.OpenPrice);
// 			
// 			currentPrice = item.OpenPrice;
//
// 			var isUp = currentPrice > average;
// 			
// 			lastHigh = Math.Max(lastHigh, currentPrice);
// 			lastLow = Math.Min(lastLow, currentPrice);
// 		
// 			var deltaHigh = (currentPrice - lastHigh) / lastHigh * -1;
// 			var deltaLow = (currentPrice - lastLow) / lastLow;
//
// 			// if (i >= 1000)
// 			// {
// 			// 	_logger.LogInformation($"{currentPrice} {lastHigh} {deltaHigh}");
// 			// 	return;
// 			// }
//
// 			if (deltaHigh >= maxDelta)
// 			{
// 				lastHigh = currentPrice;
//
// 				TryCompleteOrders(ref balance, longs, currentPrice, takeProfit, null);
// 			}
//
// 			TryCompleteOrders(ref balance, longs, currentPrice, null, stopLoss);
//
// 			if (deltaLow >= maxDelta)
// 			{
// 				lastLow = currentPrice;
//
// 				TryCompleteOrders(ref balance, shorts, currentPrice, takeProfit, null);
// 			}
//
// 			TryCompleteOrders(ref balance, shorts, currentPrice, null, stopLoss);
// 		
// 		
// 			// var quantity = 1.2m;
// 			// var quantity = balance * 0.01m;
//
// 			if (i % orderStep == 0)
// 			{
// 				var deposit = quantity * 2 + quantity * comissionPercent * 2 * leverage;
// 				var ordersSum = longs.Sum(o => o.TotalQuantity) + shorts.Sum(o => o.TotalQuantity);
// 				var outOfBalance = balance <= ordersSum;
// 				// if (outOfBalance)
// 				if (balance <= deposit)
// 				{
// 					// _logger.LogWarning($"[{i}/{range}] Out of balance: ${balance}");
// 					continue;
// 				}
//
// 				balance -= deposit;
//
// 				if (isUp)
// 				{
// 					longs.Add(new Order(false, currentPrice, quantity * 2, leverage));
// 					// shorts.Add(new Order(true, currentPrice, quantity - orderDelta, leverage));
// 				}
// 				else
// 				{
// 					// longs.Add(new Order(false, currentPrice, quantity - orderDelta, leverage));
// 					shorts.Add(new Order(true, currentPrice, quantity * 2, leverage));
// 				}
//
// 				// _logger.LogInformation($"[{i}/{range}] Current balance: ${balance}");
// 			}
// 		}
// 		
// 		var unclosedLongs = longs.Sum(o => o.Quantity);
// 		var unclosedShorts = shorts.Sum(o => o.Quantity);
// 		var unclosedBalance = (unclosedLongs + unclosedShorts);
// 		
// 		var closeBalance = 0m;
// 		foreach (var order in longs)
// 		{
// 			// _logger.LogInformation($"{currentPrice} -> {order} -> {profit} -> {order.Quantity + profit}");
// 		
// 			closeBalance += order.CalculateQuantity(currentPrice);
// 		}
// 		
// 		foreach (var order in shorts)
// 		{
// 			closeBalance += order.CalculateQuantity(currentPrice);
// 		}
// 		
// 		_logger.LogInformation(
// 			$"Unclosed orders: ${unclosedLongs}/{longs.Count} longs + ${unclosedShorts}/{shorts.Count} shorts -> ${unclosedBalance}");
// 		_logger.LogInformation(
// 			$"Total result: {balance} - {initalBalance} + {closeBalance} = {balance - initalBalance + closeBalance}");
//
// 		// var result = new List<KlineItem>();
// 		//
// 		// try
// 		// {
// 		// 	foreach (var file in Directory.GetFiles("BinanceData/OneSecond")
// 		// 		         .Select(f => (Id: int.Parse(Path.GetFileNameWithoutExtension(f)), f))
// 		// 		         .OrderBy(t => t.Id)
// 		// 		         .Select(t => t.f))
// 		// 	{
// 		// 		result.AddRange(JsonSerializer.Deserialize<List<KlineItem>>(File.ReadAllText(file)));
// 		// 	}
// 		// }
// 		// catch (Exception ex)
// 		// {
// 		// 	
// 		// }
// 		//
// 		// var builder = new DatabaseBuilder();
// 		// builder.Append(result);
// 		//
// 		// File.WriteAllBytes("BinanceData/OneSecond/all.bytes", builder.Build());
//
// 		// var result = await _binanceClient.UsdFuturesApi.Account.GetBalancesAsync();
// 		//
// 		// foreach (var balance in result.Data)
// 		// {
// 		// 	Console.WriteLine($"{balance.Asset} - {balance.WalletBalance}");
// 		// }
//
// 		// var parser = new MessageParser(_binanceClient, _telegramUserBotService._bot, _logger);
// 		//
// 		// var message = """
// 		// 	ОбновлениеAGLD/USDT M5
// 		// 	
// 		// 	Переместите стоп лос в безубыток!
// 		// 	
// 		// 	Время: 2025-04-28 19:03:21 GMT+0
// 		// 	""";
// 		//
// 		// await parser.Parse(message);
// // 		
// // 		await Task.Delay(10000);
//
// 		// var message = """
// 		// 	ОбновлениеEDU/USDT M5
// 		// 	
// 		// 	Переместите стоп лос в безубыток!
// 		// 	
// 		// 	Время: 2025-04-28 19:03:21 GMT+0
// 		// 	""";
// 		//
// 		// await parser.Parse(message);
//
// 		// var messages = JsonSerializer.Deserialize<string[]>(File.ReadAllText("messages.json"))!
// 		// 	.TakeLast(2)
// 		// 	.ToArray();
// 		//
// 		// foreach (var message in messages)
// 		// {
// 		// 	await parser.Parse(message);
// 		// }
// 	}

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
	private async Task FetchData()
	{
		var step = TimeSpan.FromMinutes(1);
		var range = TimeSpan.FromDays(365);
		var interval = KlineInterval.OneMinute;
		var pair = CurrencyPair.SOL_USDT;
		
	    var startTime = DateTime.UtcNow - range;
	    var endTime = DateTime.UtcNow;
	    var allKlines = new List<IBinanceKline>();
	
	    var path = Path.Combine("BinanceData", pair, interval.ToString());
	
	    Directory.CreateDirectory(path);
	
	    var i = 0;
	    
	    try
	    {
	        while (startTime < endTime)
	        {
	            var result = await _binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
	                CurrencyPair.SOL_USDT,
	                interval,
	                startTime,
	                endTime,
	                limit: 1000
	            );
	
	            if (!result.Data.Any())
	                break;
	
	            File.WriteAllText(Path.Combine(path, $"{i++}.json"), JsonSerializer.Serialize(result.Data));
	            allKlines.AddRange(result.Data);
	
	            startTime = result.Data.Last().OpenTime.Add(step);
	
	            Console.WriteLine($"{startTime} - {endTime - startTime}");
	        }
	    }
	    catch (Exception ex)
	    {
	        _logger.LogError(ex, "{Name}", ex.Message);
	    }
	
	    // Console.WriteLine(string.Join("\n", result.Data.Select(k => $"{k.OpenTime} - {k.CloseTime}: {GetAveragePrice(k)}")));
	
	    // var builder = new DatabaseBuilder();
	    //
	    // var items = allKlines.Select(k => new KlineItem
	    // {
	    //     OpenTime = k.OpenTime,
	    //     CloseTime = k.CloseTime,
	    //     OpenPrice = k.OpenPrice,
	    //     ClosePrice = k.ClosePrice,
	    //     LowPrice = k.LowPrice,
	    //     HighPrice = k.HighPrice,
	    //     Volume = k.Volume,
	    //     BuyVolume = k.TakerBuyBaseVolume,
	    // }).ToArray();
	    //
	    // builder.Append(items);
	    //
	    // var data = builder.Build();
	
	    File.WriteAllText("Data/all.json", JsonSerializer.Serialize(allKlines));
	    
	    Console.WriteLine($"Data saved! Count: {allKlines.Count}");
	}
	//
	// private decimal GetAveragePrice(KlineItem kline)
	// {
	//     return (kline.ClosePrice + kline.OpenPrice + kline.HighPrice + kline.LowPrice) / 4;
	// }
}