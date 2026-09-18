namespace Sensus.Extensions
{
    public static class IListExtensions
    {
        public static void AddRange<T>(this IList<T> target, IList<T> source)
        {
            foreach (var item in source)
            {
                target.Add(item);
            }
        }
    }
}
