public static class IdGen
{
    private static readonly Random Random = new();
    public static long RandomLong() => Random.NextInt64();
}