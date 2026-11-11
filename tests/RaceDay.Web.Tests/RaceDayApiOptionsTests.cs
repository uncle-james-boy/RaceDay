using RaceDay.Web.Services;

namespace RaceDay.Web.Tests;

public sealed class RaceDayApiOptionsTests
{
    [Theory]
    [InlineData("https://localhost:7107", "https://localhost:7107/")]
    [InlineData("http://raceday-api:8080/", "http://raceday-api:8080/")]
    public void GetBaseAddress_AcceptsHttpAddressesAndAddsTrailingSlash(string configuredUrl, string expectedUrl)
    {
        var options = new RaceDayApiOptions { BaseUrl = configuredUrl };

        Assert.Equal(new Uri(expectedUrl), options.GetBaseAddress());
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("ftp://localhost:21/")]
    [InlineData("https://user:password@localhost:7107/")]
    [InlineData("https://localhost:7107/?token=secret")]
    [InlineData("https://localhost:7107/#fragment")]
    public void TryGetBaseAddress_RejectsUnsafeOrUnsupportedValues(string configuredUrl)
    {
        var options = new RaceDayApiOptions { BaseUrl = configuredUrl };

        Assert.False(options.TryGetBaseAddress(out _));
    }
}
