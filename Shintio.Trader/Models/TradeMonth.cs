namespace Shintio.Trader.Models;

public record TradeMonth(int Year, int Month) : IComparable<TradeMonth>
{
	public DateTime Start => new(Year, Month, 1);
	public DateTime End => new(Year, Month, Days, 23, 59, 59);

	public int Days => DateTime.DaysInMonth(Year, Month);
	public int Minutes => Days * 1440; // 24 * 60

	public TradeMonth Prev => FromDateTime(Start.AddMonths(-1));
	public TradeMonth Next => FromDateTime(Start.AddMonths(1));

	public static TradeMonth FromDateTime(DateTime dateTime)
	{
		return new TradeMonth(dateTime.Year, dateTime.Month);
	}

	public static IReadOnlyCollection<TradeMonth> FromRange(DateTime from, DateTime to)
	{
		var result = new List<TradeMonth>();

		var current = FromDateTime(from);
		var end = FromDateTime(to);

		while (current.CompareTo(end) <= 0)
		{
			result.Add(current);
			current = current.Next;
		}

		return result;
	}

	public int CompareTo(TradeMonth? other)
	{
		if (other == null)
		{
			return 1;
		}

		var yearComparison = Year.CompareTo(other.Year);
		return yearComparison != 0 ? yearComparison : Month.CompareTo(other.Month);
	}

	public override string ToString() => $"{Year:D4}-{Month:D2}";
}