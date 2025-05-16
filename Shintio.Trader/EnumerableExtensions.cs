namespace Shintio.Trader;

public static class EnumerableExtensions
{
	public static IEnumerable<T> Step<T>(this IEnumerable<T> source, int step)
	{
		return source.Where((x, i) => i % step == 0);
	}
}