using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceDay.Api.Authentication;
using RaceDay.Api.Controllers;
using RaceDay.Api.Data;
using RaceDay.Api.Data.Entities;
using RaceDay.Api.Services;
using SkiaSharp;
using Xunit;

namespace RaceDay.Api.Tests;

public sealed class ProfileTests
{
    [Fact]
    public async Task GetProfileUsesTheSessionUserAndDoesNotReturnTheBlobIdentifier()
    {
        await using var db = CreateDbContext();
        var current = CreateUser("current@example.com");
        var other = CreateUser("other@example.com");
        current.ProfilePictureUrl = NewBlobName();
        db.Users.AddRange(current, other);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new FakeBlobStorage(), current.UserID);

        var result = await controller.GetProfile(CancellationToken.None);

        var response = Assert.IsType<UserProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(current.UserID, response.UserID);
        Assert.True(response.HasProfilePicture);
        Assert.DoesNotContain("ProfilePictureUrl", typeof(UserProfileResponse).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain(current.ProfilePictureUrl!, System.Text.Json.JsonSerializer.Serialize(response));
        Assert.DoesNotContain("PasswordHash", typeof(UserProfileResponse).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task ProfileUpdateChangesOnlyNameAndKeepsThePrivatePictureReference()
    {
        await using var db = CreateDbContext();
        var current = CreateUser("current@example.com");
        var other = CreateUser("other@example.com");
        var blobName = NewBlobName();
        current.ProfilePictureUrl = blobName;
        db.Users.AddRange(current, other);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new FakeBlobStorage(), current.UserID);

        var result = await controller.UpdateProfile(new UpdateProfileRequest { FullName = "  New Name  " }, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("New Name", current.FullName);
        Assert.Equal(blobName, current.ProfilePictureUrl);
        Assert.Equal("Alex Runner", other.FullName);
        Assert.DoesNotContain("ProfilePictureUrl", typeof(UpdateProfileRequest).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public async Task UploadStoresOnlyTheGeneratedPrivateBlobNameForTheSessionUser()
    {
        await using var db = CreateDbContext();
        var current = CreateUser("current@example.com");
        var other = CreateUser("other@example.com");
        db.Users.AddRange(current, other);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        var controller = CreateController(db, storage, current.UserID);
        var file = ImageFile(Encode(SKEncodedImageFormat.Png), "profile.png", "image/png");

        var result = await controller.UploadProfilePicture(file, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result.Result).StatusCode);
        Assert.Single(storage.Uploads);
        var upload = storage.Uploads.Single();
        Assert.Equal(RaceDayBlobContainer.ProfilePictures, upload.Container);
        Assert.Matches("^[a-f0-9]{32}\\.png$", upload.BlobName);
        Assert.Equal(upload.BlobName, current.ProfilePictureUrl);
        Assert.Null(other.ProfilePictureUrl);
        Assert.DoesNotContain("blob.core.windows.net", System.Text.Json.JsonSerializer.Serialize(
            Assert.IsType<OkObjectResult>(result.Result).Value));
    }

    [Theory]
    [InlineData("profile.jpg", "image/jpeg", SKEncodedImageFormat.Jpeg)]
    [InlineData("profile.png", "image/png", SKEncodedImageFormat.Png)]
    [InlineData("profile.webp", "image/webp", SKEncodedImageFormat.Webp)]
    public async Task UploadAcceptsSupportedImageContent(string name, string contentType, SKEncodedImageFormat format)
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        var controller = CreateController(db, storage, user.UserID);

        var result = await controller.UploadProfilePicture(
            ImageFile(Encode(format), name, contentType), CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result.Result).StatusCode);
        Assert.Single(storage.Uploads);
    }

    [Fact]
    public async Task UploadRejectsInvalidContentBeforeCallingStorage()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        var controller = CreateController(db, storage, user.UserID);

        var result = await controller.UploadProfilePicture(
            ImageFile(Encoding.ASCII.GetBytes("not an image"), "profile.png", "image/png"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(storage.Uploads);
        Assert.Null(user.ProfilePictureUrl);
    }

    [Fact]
    public async Task UploadRejectsAFileOverTwoMegabytesBeforeCallingStorage()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        var controller = CreateController(db, storage, user.UserID);
        var file = ImageFile(Encode(SKEncodedImageFormat.Png), "profile.png", "image/png");
        file = new FormFile(file.OpenReadStream(), 0, UsersController.MaximumProfilePictureSizeBytes + 1,
            "file", file.FileName) { Headers = file.Headers };

        var result = await controller.UploadProfilePicture(file, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task UploadStorageFailureKeepsTheExistingPictureReference()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        var oldBlobName = NewBlobName();
        user.ProfilePictureUrl = oldBlobName;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage { FailUpload = true };
        var controller = CreateController(db, storage, user.UserID);

        var result = await controller.UploadProfilePicture(
            ImageFile(Encode(SKEncodedImageFormat.Png), "profile.png", "image/png"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status502BadGateway, Assert.IsType<ObjectResult>(result.Result).StatusCode);
        Assert.Equal(oldBlobName, user.ProfilePictureUrl);
    }

    [Fact]
    public async Task ReplacingPrivatePictureDeletesOnlyARecognizedOldBlobAfterSaving()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        var oldBlobName = NewBlobName();
        user.ProfilePictureUrl = oldBlobName;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        storage.Blobs[oldBlobName] = (Encode(SKEncodedImageFormat.Png), "image/png");
        var controller = CreateController(db, storage, user.UserID);

        var result = await controller.UploadProfilePicture(
            ImageFile(Encode(SKEncodedImageFormat.Webp), "new.webp", "image/webp"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotEqual(oldBlobName, user.ProfilePictureUrl);
        Assert.Contains(oldBlobName, storage.DeletedNames);
    }

    [Fact]
    public async Task RetrievalStreamsOnlyTheSignedInUsersPrivateBlob()
    {
        await using var db = CreateDbContext();
        var current = CreateUser("current@example.com");
        var other = CreateUser("other@example.com");
        current.ProfilePictureUrl = NewBlobName();
        other.ProfilePictureUrl = NewBlobName();
        db.Users.AddRange(current, other);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        storage.Blobs[current.ProfilePictureUrl] = (Encode(SKEncodedImageFormat.Png), "image/png");
        storage.Blobs[other.ProfilePictureUrl] = (Encode(SKEncodedImageFormat.Jpeg), "image/jpeg");
        var controller = CreateController(db, storage, current.UserID);

        var result = await controller.GetProfilePicture(CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", file.ContentType);
        Assert.Equal(RaceDayBlobContainer.ProfilePictures, storage.LastDownloadedContainer);
        Assert.Equal(current.ProfilePictureUrl, storage.LastDownloadedName);
        Assert.Equal("no-store, private", controller.Response.Headers.CacheControl);
        Assert.Equal("nosniff", controller.Response.Headers.XContentTypeOptions);
    }

    [Fact]
    public async Task RetrievalReturnsNotFoundForMissingOrLegacyPublicUrlReferences()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        user.ProfilePictureUrl = "https://public.example.test/old-picture.jpg";
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var storage = new FakeBlobStorage();
        var controller = CreateController(db, storage, user.UserID);

        var result = await controller.GetProfilePicture(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(storage.LastDownloadedName);
    }

    [Fact]
    public async Task RetrievalReturnsNotFoundWhenPrivateBlobIsMissing()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("runner@example.com");
        user.ProfilePictureUrl = NewBlobName();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var controller = CreateController(db, new FakeBlobStorage(), user.UserID);

        var result = await controller.GetProfilePicture(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ProfilePictureActionsRequireTheExistingSessionAuthorizationFilter()
    {
        var typeAuthorization = Assert.Single(typeof(UsersController)
            .GetCustomAttributes(typeof(SessionAuthorizeAttribute), inherit: true));
        Assert.IsType<SessionAuthorizeAttribute>(typeAuthorization);
        Assert.Contains(typeof(UsersController).GetMethods(), method =>
            method.Name == nameof(UsersController.UploadProfilePicture)
            && method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute), true).Length == 1);
        Assert.Contains(typeof(UsersController).GetMethods(), method =>
            method.Name == nameof(UsersController.GetProfilePicture)
            && method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute), true).Length == 1);

        var actionContext = new ActionContext(
            new DefaultHttpContext { Session = new TestSession() }, new RouteData(), new ActionDescriptor());
        var authorizationContext = new AuthorizationFilterContext(actionContext, []);
        new SessionAuthorizeAttribute().OnAuthorization(authorizationContext);
        Assert.IsType<UnauthorizedResult>(authorizationContext.Result);
    }

    private static RaceDayDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RaceDayDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User CreateUser(string email) => new()
    {
        FullName = "Alex Runner", Email = email, PasswordHash = "hashed-password", Role = UserRoles.Participant
    };

    private static UsersController CreateController(RaceDayDbContext db, IBlobStorageService storage, int userId)
    {
        var session = new TestSession();
        session.SetInt32(SessionKeys.UserId, userId);
        session.SetString(SessionKeys.Role, UserRoles.Participant);
        var controller = new UsersController(db, storage, NullLogger<UsersController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Session = session }
            }
        };
        return controller;
    }

    private static IFormFile ImageFile(byte[] bytes, string fileName, string contentType)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", fileName)
        {
            Headers = new HeaderDictionary { ["Content-Type"] = contentType }
        };
    }

    private static byte[] Encode(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(2, 2);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private static string NewBlobName() => $"{Guid.NewGuid():N}.png";

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
    }

    private sealed class FakeBlobStorage : IBlobStorageService
    {
        public List<(RaceDayBlobContainer Container, string BlobName, long Length, string ContentType)> Uploads { get; } = [];
        public Dictionary<string, (byte[] Bytes, string ContentType)> Blobs { get; } = new();
        public List<string> DeletedNames { get; } = [];
        public bool FailUpload { get; init; }
        public RaceDayBlobContainer? LastDownloadedContainer { get; private set; }
        public string? LastDownloadedName { get; private set; }

        public async Task<Uri> UploadAsync(RaceDayBlobContainer container, string blobName, Stream content,
            string contentType, CancellationToken cancellationToken = default)
        {
            if (FailUpload) throw new HttpRequestException("storage unavailable");
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Uploads.Add((container, blobName, buffer.Length, contentType));
            Blobs[blobName] = (buffer.ToArray(), contentType);
            return new Uri($"https://storage.example.test/{container}/{blobName}");
        }

        public Task<StoredBlob?> DownloadAsync(RaceDayBlobContainer container, string blobName,
            CancellationToken cancellationToken = default)
        {
            LastDownloadedContainer = container;
            LastDownloadedName = blobName;
            return Task.FromResult<StoredBlob?>(Blobs.TryGetValue(blobName, out var value)
                ? new StoredBlob(new MemoryStream(value.Bytes, writable: false), value.ContentType)
                : null);
        }

        public Task<bool> DeleteIfExistsAsync(RaceDayBlobContainer container, string blobName,
            CancellationToken cancellationToken = default)
        {
            DeletedNames.Add(blobName);
            Blobs.Remove(blobName);
            return Task.FromResult(true);
        }

        public bool TryGetBlobNameFromUri(RaceDayBlobContainer container, Uri blobUri, out string blobName)
        {
            blobName = string.Empty;
            return false;
        }
    }
}
