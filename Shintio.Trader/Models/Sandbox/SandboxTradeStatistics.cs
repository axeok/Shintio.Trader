namespace Shintio.Trader.Models.Sandbox;

public class SandboxTradeStatistics
{
	public Statistics Shorts = new();
	public Statistics Longs = new();

	public decimal WinrateCount => (decimal)(Shorts.WinsCount + Longs.WinsCount) /
	                               (Shorts.WinsCount + Shorts.LosesCount + Longs.WinsCount + Longs.LosesCount);
	
	public decimal WinrateSum => (Shorts.WinsSum + Longs.WinsSum) /
	                          (Shorts.WinsSum + Shorts.LosesSum + Longs.WinsSum + Longs.LosesSum);

	public class Statistics
	{
		public int TotalCount { get; set; } = 0;
		public decimal TotalSum { get; set; } = 0;
		
		public int WinsCount { get; set; } = 0;
		public int LosesCount { get; set; } = 0;

		public decimal WinsSum { get; set; } = 0;
		public decimal LosesSum { get; set; } = 0;
	}
}