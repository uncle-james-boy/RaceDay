using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using RaceDay.Api.Services;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class EventBannerUploadTests
{
    [Fact]
    public void ImageValidatorAcceptsDeclaredJpegAndWebPSignatures()
    {
        Assert.True(ImageContentValidator.TryValidate("runner.jpg", "image/jpeg", JpegImage(),
            out var jpegExtension, out var jpegType));
        Assert.Equal(".jpg", jpegExtension);
        Assert.Equal("image/jpeg", jpegType);

        Assert.True(ImageContentValidator.TryValidate("runner.webp", "image/webp", WebPImage(),
            out var webpExtension, out var webpType));
        Assert.Equal(".webp", webpExtension);
        Assert.Equal("image/webp", webpType);
    }

    [Fact]
    public async Task UploadRequiresAnOrganiserWhoOwnsTheEvent()
    {
        Assert.True(ImageContentValidator.TryValidate("banner.png", "image/png", PngImage(),
            out _, out _));
        await using var fixture = await ApiFixture.CreateAsync();
        var eventId = await fixture.AddEventAsync(organiserId: 7);

        using var anonymous = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png");
        using var participant = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Participant");
        using var otherOrganiser = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 8, "Organiser");
        using var owner = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Organiser");
        using var ownerJpeg = await fixture.UploadAsync(eventId, JpegImage(), "banner.jpg", "image/jpeg", 7, "Organiser");
        using var ownerWebp = await fixture.UploadAsync(eventId, WebPImage(), "banner.webp", "image/webp", 7, "Organiser");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, participant.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, otherOrganiser.StatusCode);
        Assert.True(owner.StatusCode == HttpStatusCode.OK, await owner.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, ownerJpeg.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerWebp.StatusCode);

        var saved = await fixture.GetEventAsync(eventId);
        var savedUrl = Assert.IsType<string>(saved.EventBannerUrl);
        Assert.StartsWith(FakeBlobStorage.EventBannerBaseUri.AbsoluteUri, savedUrl, StringComparison.Ordinal);
        Assert.True(savedUrl.Length <= 500);
        Assert.All(fixture.Storage.Uploads, upload => Assert.Equal(RaceDayBlobContainer.EventBanners, upload.Container));
        Assert.Equal(3, fixture.Storage.Uploads.Count);
    }

    [Fact]
    public async Task UploadRequiresMatchingSupportedFileTypeAndValidContent()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        var eventId = await fixture.AddEventAsync(organiserId: 7);
        var corruptPng = PngImage();
        corruptPng[29] ^= 0x01;
        var invalidPixelPayload = PngImageWithInvalidCompressedPixels();
        var malformedWebP = WebPImage();
        malformedWebP[4] ^= 0x01;

        using var wrongExtension = await fixture.UploadAsync(eventId, PngImage(), "banner.jpg", "image/jpeg", 7, "Organiser");
        using var wrongMime = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/webp", 7, "Organiser");
        using var malformed = await fixture.UploadAsync(eventId, PngImage()[..20], "banner.png", "image/png", 7, "Organiser");
        using var corrupt = await fixture.UploadAsync(eventId, corruptPng, "banner.png", "image/png", 7, "Organiser");
        using var malformedPixels = await fixture.UploadAsync(eventId, invalidPixelPayload, "banner.png", "image/png", 7, "Organiser");
        using var malformedJpeg = await fixture.UploadAsync(eventId, JpegImage()[..20], "banner.jpg", "image/jpeg", 7, "Organiser");
        using var malformedWebPResponse = await fixture.UploadAsync(eventId, malformedWebP, "banner.webp", "image/webp", 7, "Organiser");
        using var unsupported = await fixture.UploadAsync(eventId, [1, 2, 3, 4], "banner.gif", "image/gif", 7, "Organiser");

        Assert.Equal(HttpStatusCode.BadRequest, wrongExtension.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, wrongMime.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, corrupt.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedPixels.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedJpeg.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformedWebPResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Empty(fixture.Storage.Uploads);
    }

    [Fact]
    public async Task UploadAcceptsAFileAtTheFiveMegabyteLimit()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        var eventId = await fixture.AddEventAsync(organiserId: 7);
        var image = PngImage(exactLength: (int)EventBannersController.MaximumFileSizeBytes);

        using var response = await fixture.UploadAsync(eventId, image, "banner.png", "image/png", 7, "Organiser");

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Assert.Single(fixture.Storage.Uploads);
        Assert.Equal(EventBannersController.MaximumFileSizeBytes, fixture.Storage.Uploads[0].Length);
    }

    [Fact]
    public async Task UploadRejectsAnOversizedFileBeforeStorage()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        var eventId = await fixture.AddEventAsync(organiserId: 7);
        var image = PngImage(exactLength: (int)EventBannersController.MaximumFileSizeBytes + 1);

        using var response = await fixture.UploadAsync(eventId, image, "banner.png", "image/png", 7, "Organiser");

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge);
        Assert.Empty(fixture.Storage.Uploads);
    }

    [Fact]
    public async Task ReplacementDeletesOnlyAConfirmedOldBannerAfterDatabaseSave()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        var oldBlobName = $"{Guid.NewGuid():N}.png";
        var eventId = await fixture.AddEventAsync(organiserId: 7,
            oldBannerUrl: new Uri(FakeBlobStorage.EventBannerBaseUri, oldBlobName).AbsoluteUri);

        using var response = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Organiser");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(oldBlobName, fixture.Storage.DeletedBlobNames);
        Assert.Single(fixture.Storage.Uploads);

        await fixture.SetBannerUrlAsync(eventId, "https://images.example.test/previous.png");
        fixture.Storage.DeletedBlobNames.Clear();
        using var externalUrlReplacement = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Organiser");

        Assert.Equal(HttpStatusCode.OK, externalUrlReplacement.StatusCode);
        Assert.Empty(fixture.Storage.DeletedBlobNames);
    }

    [Fact]
    public async Task StorageFailureLeavesTheCurrentBannerUntouched()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        const string oldUrl = "https://images.example.test/old.png";
        var eventId = await fixture.AddEventAsync(organiserId: 7, oldBannerUrl: oldUrl);
        fixture.Storage.FailUploads = true;

        using var response = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Organiser");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(oldUrl, (await fixture.GetEventAsync(eventId)).EventBannerUrl);
        Assert.Empty(fixture.Storage.DeletedBlobNames);
    }

    [Fact]
    public async Task DatabaseFailureCleansNewBlobAndPreservesPreviousBanner()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        const string oldUrl = "https://images.example.test/old.png";
        var eventId = await fixture.AddEventAsync(organiserId: 7, oldBannerUrl: oldUrl);
        fixture.SaveInterceptor.FailNextSave = true;

        using var response = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Organiser");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(oldUrl, (await fixture.GetEventAsync(eventId)).EventBannerUrl);
        Assert.Single(fixture.Storage.Uploads);
        Assert.Contains(fixture.Storage.Uploads[0].BlobName, fixture.Storage.DeletedBlobNames);
    }

    [Fact]
    public async Task TooLongStorageUrlIsNotSavedAndNewBlobIsRemoved()
    {
        await using var fixture = await ApiFixture.CreateAsync();
        const string oldUrl = "https://images.example.test/old.png";
        var eventId = await fixture.AddEventAsync(organiserId: 7, oldBannerUrl: oldUrl);
        fixture.Storage.UploadUriOverride = new Uri($"https://raceday.blob.core.windows.net/event-banners/{new string('a', 510)}");

        using var response = await fixture.UploadAsync(eventId, PngImage(), "banner.png", "image/png", 7, "Organiser");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal(oldUrl, (await fixture.GetEventAsync(eventId)).EventBannerUrl);
        Assert.Contains(fixture.Storage.Uploads.Single().BlobName, fixture.Storage.DeletedBlobNames);
    }

    private static byte[] PngImage(int? exactLength = null)
    {
        var validPng = EncodeImage(SKEncodedImageFormat.Png);
        if (!exactLength.HasValue) return validPng;

        var textLength = exactLength.Value - validPng.Length - 12;
        if (textLength < 5) throw new ArgumentOutOfRangeException(nameof(exactLength));
        var text = new byte[textLength];
        Array.Fill(text, (byte)'x');
        Encoding.ASCII.GetBytes("note\0", text);

        using var png = new MemoryStream(exactLength.Value);
        png.Write(validPng.AsSpan(0, validPng.Length - 12));
        WritePngChunk(png, "tEXt", text);
        png.Write(validPng.AsSpan(validPng.Length - 12));
        return png.ToArray();
    }

    private static byte[] JpegImage() => EncodeImage(SKEncodedImageFormat.Jpeg);

    private static byte[] WebPImage() => EncodeImage(SKEncodedImageFormat.Webp);

    private static byte[] EncodeImage(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        bitmap.Erase(SKColors.Crimson);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100)
            ?? throw new InvalidOperationException($"Skia could not encode the {format} test image.");
        return data.ToArray();
    }

    private static byte[] PngImageWithInvalidCompressedPixels()
    {
        var png = PngImage();
        var offset = 8;
        var foundImageData = false;
        while (offset <= png.Length - 12)
        {
            var dataLength = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            var dataOffset = offset + 8;
            var chunkEnd = dataOffset + dataLength + 4;
            var chunkType = png.AsSpan(offset + 4, 4);
            if (chunkType.SequenceEqual("IDAT"u8))
            {
                png.AsSpan(dataOffset, dataLength).Fill(0);
                var crcInput = png.AsSpan(offset + 4, 4 + dataLength);
                BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(dataOffset + dataLength, 4), Crc32(crcInput));
                foundImageData = true;
            }

            offset = chunkEnd;
            if (chunkType.SequenceEqual("IEND"u8)) break;
        }

        if (!foundImageData) throw new InvalidOperationException("The test PNG has no IDAT chunk.");
        return png;
    }

    private static void WritePngChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crcInput = new byte[typeBytes.Length + data.Length];
        typeBytes.CopyTo(crcInput, 0);
        data.CopyTo(crcInput, typeBytes.Length);
        BinaryPrimitives.WriteUInt32BigEndian(length, Crc32(crcInput));
        output.Write(length);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) == 0 ? crc >> 1 : 0xEDB88320u ^ (crc >> 1);
            }
        }
        return ~crc;
    }

    private sealed class ApiFixture : IAsyncDisposable
    {
        private ApiFixture(WebApplication app, HttpClient client,
            FakeBlobStorage storage, SaveFailureInterceptor saveInterceptor)
        {
            App = app;
            Client = client;
            Storage = storage;
            SaveInterceptor = saveInterceptor;
        }

        private WebApplication App { get; }
        private HttpClient Client { get; }
        public FakeBlobStorage Storage { get; }
        public SaveFailureInterceptor SaveInterceptor { get; }

        public static async Task<ApiFixture> CreateAsync()
        {
            var databaseName = Guid.NewGuid().ToString("N");
            var storage = new FakeBlobStorage();
            var interceptor = new SaveFailureInterceptor();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddControllers().AddApplicationPart(typeof(EventBannersController).Assembly);
            builder.Services.AddAuthorization();
            builder.Services.AddDbContext<RaceDayDbContext>((services, options) =>
                options.UseInMemoryDatabase(databaseName)
                    .AddInterceptors(services.GetRequiredService<SaveFailureInterceptor>()));
            builder.Services.AddSingleton(interceptor);
            builder.Services.AddSingleton(storage);
            builder.Services.AddSingleton<IBlobStorageService>(storage);
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddSession(options =>
            {
                options.Cookie.Name = ".RaceDay.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

            var app = builder.Build();
            app.UseSession();
            app.Use(async (context, next) =>
            {
                if (int.TryParse(context.Request.Headers["X-Test-User"], out var userId)
                    && context.Request.Headers.TryGetValue("X-Test-Role", out var role))
                {
                    context.Session.SetInt32(SessionKeys.UserId, userId);
                    context.Session.SetString(SessionKeys.Role, role.ToString());
                }
                await next();
            });
            app.UseAuthorization();
            app.MapControllers();
            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new ApiFixture(app, new HttpClient { BaseAddress = new Uri(address) }, storage, interceptor);
        }

        public async Task<int> AddEventAsync(int organiserId, string? oldBannerUrl = null)
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            var raceEvent = new RaceEvent
            {
                OrganiserID = organiserId,
                Name = "Harbour Run",
                EventDate = DateTime.UtcNow.AddDays(30),
                Location = "Cape Town",
                Distance = 10,
                EventType = "Run",
                EventBannerUrl = oldBannerUrl
            };
            db.Events.Add(raceEvent);
            await db.SaveChangesAsync();
            return raceEvent.EventID;
        }

        public async Task SetBannerUrlAsync(int eventId, string? bannerUrl)
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            var raceEvent = await db.Events.SingleAsync(item => item.EventID == eventId);
            raceEvent.EventBannerUrl = bannerUrl;
            await db.SaveChangesAsync();
        }

        public async Task<EventSnapshot> GetEventAsync(int eventId)
        {
            await using var scope = App.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            var raceEvent = await db.Events.AsNoTracking().SingleAsync(item => item.EventID == eventId);
            return new EventSnapshot(raceEvent.EventBannerUrl);
        }

        public async Task<HttpResponseMessage> UploadAsync(
            int eventId, byte[] image, string fileName, string contentType,
            int? userId = null, string? role = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/banner");
            if (userId.HasValue)
            {
                request.Headers.Add("X-Test-User", userId.Value.ToString());
                request.Headers.Add("X-Test-Role", role ?? "");
            }

            using var multipart = new MultipartFormDataContent();
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            multipart.Add(file, "file", fileName);
            request.Content = multipart;
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }

    private sealed record EventSnapshot(string? EventBannerUrl);

    private sealed class FakeBlobStorage : IBlobStorageService
    {
        public static readonly Uri EventBannerBaseUri = new("https://raceday.blob.core.windows.net/event-banners/");
        public List<UploadRecord> Uploads { get; } = [];
        public List<string> DeletedBlobNames { get; } = [];
        public bool FailUploads { get; set; }
        public Uri? UploadUriOverride { get; set; }

        public async Task<Uri> UploadAsync(RaceDayBlobContainer container, string blobName, Stream content,
            string contentType, CancellationToken cancellationToken = default)
        {
            if (FailUploads) throw new IOException("Simulated storage failure.");
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Uploads.Add(new UploadRecord(container, blobName, buffer.Length, contentType));
            return UploadUriOverride ?? new Uri(EventBannerBaseUri, blobName);
        }

        public Task<StoredBlob?> DownloadAsync(RaceDayBlobContainer container, string blobName,
            CancellationToken cancellationToken = default) => Task.FromResult<StoredBlob?>(null);

        public Task<bool> DeleteIfExistsAsync(RaceDayBlobContainer container, string blobName,
            CancellationToken cancellationToken = default)
        {
            DeletedBlobNames.Add(blobName);
            return Task.FromResult(true);
        }

        public bool TryGetBlobNameFromUri(RaceDayBlobContainer container, Uri blobUri, out string blobName)
        {
            var prefix = EventBannerBaseUri.AbsoluteUri;
            if (container == RaceDayBlobContainer.EventBanners
                && blobUri.AbsoluteUri.StartsWith(prefix, StringComparison.Ordinal)
                && !blobUri.AbsoluteUri[prefix.Length..].Contains('/'))
            {
                blobName = blobUri.AbsoluteUri[prefix.Length..];
                return true;
            }

            blobName = string.Empty;
            return false;
        }
    }

    private sealed record UploadRecord(RaceDayBlobContainer Container, string BlobName, long Length, string ContentType);

    private sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        public bool FailNextSave { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (FailNextSave)
            {
                FailNextSave = false;
                throw new DbUpdateException("Simulated database failure.");
            }

            return ValueTask.FromResult(result);
        }
    }
}
