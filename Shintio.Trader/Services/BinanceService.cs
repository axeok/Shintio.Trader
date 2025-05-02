using Binance.Net.Enums;
using Binance.Net.Interfaces;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Logging;
using Shintio.Trader.Tables;

namespace Shintio.Trader.Services;

public class BinanceService
{
	private readonly ILogger<BinanceService> _logger;

	public BinanceService(ILogger<BinanceService> logger, IBinanceRestClient client)
	{
		_logger = logger;

		Client = client;
	}

	public IBinanceRestClient Client { get; }

	public async IAsyncEnumerable<KlineItem> FetchKlineHistory(
		string pair,
		KlineInterval interval,
		DateTime from,
		DateTime? to = null,
		int limit = 1000
	)
	{
		var step = TimeSpan.FromSeconds((int)interval);
		
	    var startTime = from;
	    var endTime = to ?? DateTime.UtcNow;
	    
	    while (startTime < endTime)
	    {
		    WebCallResult<IEnumerable<IBinanceKline>> result;
		    try
		    {
			    result = await Client.SpotApi.ExchangeData.GetKlinesAsync(
				    pair,
				    interval,
				    startTime,
				    endTime,
				    limit: limit
			    );
		    }
		    catch (Exception ex)
		    {
			    _logger.LogError(ex, "{Name}", ex.Message);

			    continue;
		    }

		    if (!result.Data.Any())
		    {
			    break;
		    }

		    foreach (var kline in result.Data)
		    {
			    yield return new KlineItem
			    {
				    OpenTime = kline.OpenTime,
				    CloseTime = kline.CloseTime,
				    OpenPrice = kline.OpenPrice,
				    ClosePrice = kline.ClosePrice,
				    LowPrice = kline.LowPrice,
				    HighPrice = kline.HighPrice,
				    Volume = kline.Volume,
				    TradeCount = kline.TradeCount,
				    TakerBuyBaseVolume = kline.TakerBuyBaseVolume,
			    };
		    }

		    startTime = result.Data.Last().OpenTime.Add(step);
	    }
	}
}