using Xunit;

public sealed class CacheKeyTests
{
    private static readonly SearchRequest Morning = new(
        "CPH",
        "STOP-NORREPORT",
        "STOP-AIRPORT",
        DateTimeOffset.Parse("2026-10-02T06:00:00Z"),
        DateTimeOffset.Parse("2026-10-02T07:00:00Z"));

    [Fact]
    public void MorningAndEveningSearchesHaveDifferentKeys()
    {
        var evening = Morning with
        {
            Start = DateTimeOffset.Parse("2026-10-02T18:00:00Z"),
            End = DateTimeOffset.Parse("2026-10-02T19:00:00Z")
        };

        Assert.NotEqual(CacheKey.Build(Morning), CacheKey.Build(evening));
    }

    [Fact]
    public void ReversedOriginAndDestinationHaveDifferentKeys()
    {
        var reversed = Morning with
        {
            FromStopId = Morning.ToStopId,
            ToStopId = Morning.FromStopId
        };

        Assert.NotEqual(CacheKey.Build(Morning), CacheKey.Build(reversed));
    }

    [Fact]
    public void EquivalentInstantsHaveTheSameKey()
    {
        var offsetTime = Morning with
        {
            Start = DateTimeOffset.Parse("2026-10-02T08:00:00+02:00"),
            End = DateTimeOffset.Parse("2026-10-02T09:00:00+02:00")
        };

        Assert.Equal(CacheKey.Build(Morning), CacheKey.Build(offsetTime));
    }

    [Fact]
    public void SeparatorsInsideStopIdsDoNotCauseKeyCollisions()
    {
        var colonInOrigin = Morning with { FromStopId = "A:B", ToStopId = "C" };
        var colonInDestination = Morning with { FromStopId = "A", ToStopId = "B:C" };

        Assert.NotEqual(
            CacheKey.Build(colonInOrigin),
            CacheKey.Build(colonInDestination));
    }
}
