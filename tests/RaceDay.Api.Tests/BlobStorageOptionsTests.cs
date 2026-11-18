using RaceDay.Api.Services;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class BlobStorageOptionsTests
{
    [Fact]
    public void DefaultsUseSeparateRaceDayContainers()
    {
        var options = new BlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true" };

        options.Validate();

        Assert.Equal("event-banners", options.EventBannersContainerName);
        Assert.Equal("profile-pictures", options.ProfilePicturesContainerName);
    }

    [Fact]
    public void ValidateRequiresConnectionString()
    {
        var options = new BlobStorageOptions();

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("BlobStorage:ConnectionString", exception.Message);
    }

    [Theory]
    [InlineData("Event-Banners")]
    [InlineData("ab")]
    [InlineData("event--banners")]
    [InlineData("event_banners")]
    public void ValidateRejectsInvalidContainerNames(string name)
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            EventBannersContainerName = name
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ValidateRequiresDifferentContainers()
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ProfilePicturesContainerName = "event-banners"
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    public void ValidateRejectsUnsafePublicBannerBaseUrl()
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            EventBannersPublicBaseUrl = "http://user:password@localhost:10000/account?token=x"
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("EventBannersPublicBaseUrl", exception.Message);
    }

    [Fact]
    public void PublicBannerUriIsAcceptedForOwnedBlobCleanup()
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite",
            EventBannersPublicBaseUrl = "http://localhost:10000/devstoreaccount1"
        };
        var service = new AzureBlobStorageService(
            new BlobServiceClient(options.ConnectionString), Options.Create(options));
        var uri = new Uri("http://localhost:10000/devstoreaccount1/event-banners/0123456789abcdef0123456789abcdef.png");

        var accepted = service.TryGetBlobNameFromUri(RaceDayBlobContainer.EventBanners, uri, out var blobName);

        Assert.True(accepted);
        Assert.Equal("0123456789abcdef0123456789abcdef.png", blobName);
    }

    [Theory]
    [InlineData("http://attacker.example/devstoreaccount1/event-banners/0123456789abcdef0123456789abcdef.png")]
    [InlineData("http://localhost:10000/devstoreaccount1/profile-pictures/0123456789abcdef0123456789abcdef.png")]
    [InlineData("http://localhost:10000/devstoreaccount1/event-banners/0123456789abcdef0123456789abcdef.png?download=1")]
    public void PublicBannerUriRejectsUntrustedHostOrPath(string value)
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://azurite",
            EventBannersPublicBaseUrl = "http://localhost:10000/devstoreaccount1"
        };
        var service = new AzureBlobStorageService(
            new BlobServiceClient(options.ConnectionString), Options.Create(options));

        var accepted = service.TryGetBlobNameFromUri(
            RaceDayBlobContainer.EventBanners, new Uri(value), out var blobName);

        Assert.False(accepted);
        Assert.Equal(string.Empty, blobName);
    }
}
