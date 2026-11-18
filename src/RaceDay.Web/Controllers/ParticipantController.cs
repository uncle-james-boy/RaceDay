using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("participant")]
[RaceDayAuthorize("Participant")]
public sealed class ParticipantController(RaceDayApiClient api) : Controller
{
    public const long MaximumProfilePictureSizeBytes = 2L * 1024 * 1024;
    private const long MultipartOverheadBytes = 64L * 1024;
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        string? eventType,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken)
    {
        var model = new ParticipantHomeViewModel
        {
            Search = search,
            EventType = eventType,
            FromDate = fromDate ?? DateOnly.FromDateTime(DateTime.Today),
            ToDate = toDate
        };
        var result = await api.GetEventsAsync(
            model.Search, model.EventType, model.FromDate, model.ToDate, cancellationToken);
        if (result.IsSuccess)
        {
            model.Events = result.Value ?? [];
        }
        else
        {
            model.ErrorMessage = result.ErrorMessage;
        }

        return View(model);
    }

    [HttpGet("events/{eventId:int}")]
    public async Task<IActionResult> Details(int eventId, CancellationToken cancellationToken)
    {
        var eventResult = await api.GetJsonAsync<EventCard>($"api/events/{eventId}", cancellationToken);
        if (!eventResult.IsSuccess || eventResult.Value is null)
        {
            return eventResult.StatusCode == System.Net.HttpStatusCode.NotFound
                ? NotFound()
                : View("Details", new EventDetailsViewModel { ErrorMessage = eventResult.ErrorMessage });
        }

        var model = new EventDetailsViewModel
        {
            Event = eventResult.Value,
            SuccessMessage = TempData["ParticipantSuccess"] as string,
            ErrorMessage = TempData["ParticipantError"] as string
        };
        var categories = await api.GetJsonAsync<EventCategory[]>(
            $"api/events/{eventId}/categories", cancellationToken);
        var routes = await api.GetJsonAsync<EventRoute[]>(
            $"api/events/{eventId}/routes", cancellationToken);
        if (categories.IsSuccess)
        {
            model.Categories = categories.Value ?? [];
        }
        else
        {
            model.ErrorMessage ??= categories.ErrorMessage;
        }

        if (routes.IsSuccess)
        {
            model.Routes = routes.Value ?? [];
        }
        else
        {
            model.ErrorMessage ??= routes.ErrorMessage;
        }

        return View("Details", model);
    }

    [HttpPost("enrol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enrol(EnrolParticipantModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ParticipantError"] = "Choose a valid category before entering the event.";
            return RedirectToAction(nameof(Details), new { eventId = model.EventID });
        }

        var result = await api.PostJsonAsync<object, ParticipantEnrolment>(
            "api/enrolments",
            new { EventID = model.EventID, CategoryID = model.CategoryID },
            cancellationToken);
        if (!result.IsSuccess)
        {
            TempData["ParticipantError"] = result.ErrorMessage ?? "Your entry could not be saved.";
            return RedirectToAction(nameof(Details), new { eventId = model.EventID });
        }

        TempData["ParticipantSuccess"] = "Your event entry was submitted.";
        return RedirectToAction(nameof(MyEvents));
    }

    [HttpGet("my-events")]
    public async Task<IActionResult> MyEvents(CancellationToken cancellationToken)
    {
        var model = new ParticipantListViewModel<ParticipantEnrolment>
        {
            ErrorMessage = TempData["ParticipantError"] as string,
            SuccessMessage = TempData["ParticipantSuccess"] as string
        };
        var result = await api.GetJsonAsync<ParticipantEnrolment[]>("api/enrolments", cancellationToken);
        if (result.IsSuccess)
        {
            model.Items = result.Value ?? [];
        }
        else
        {
            model.ErrorMessage = result.ErrorMessage;
        }

        return View(model);
    }

    [HttpGet("my-results")]
    public async Task<IActionResult> MyResults(CancellationToken cancellationToken)
    {
        var model = new ParticipantListViewModel<ParticipantRaceResult>();
        var result = await api.GetJsonAsync<ParticipantRaceResult[]>("api/results", cancellationToken);
        if (result.IsSuccess)
        {
            model.Items = result.Value ?? [];
        }
        else
        {
            model.ErrorMessage = result.ErrorMessage;
        }

        return View(model);
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var result = await api.GetJsonAsync<ParticipantProfile>("api/users/profile", cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            return View(new ParticipantProfileViewModel { ErrorMessage = result.ErrorMessage });
        }

        return View(new ParticipantProfileViewModel
        {
            Profile = result.Value,
            Edit = new UpdateParticipantProfileModel
            {
                FullName = result.Value.FullName
            },
            SuccessMessage = TempData["ParticipantSuccess"] as string,
            ErrorMessage = TempData["ParticipantError"] as string
        });
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaximumProfilePictureSizeBytes + MultipartOverheadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaximumProfilePictureSizeBytes)]
    public async Task<IActionResult> UpdateProfile(
        [Bind(Prefix = "Edit")] UpdateParticipantProfileModel edit,
        CancellationToken cancellationToken)
    {
        if (edit.ProfilePictureFile is { } file
            && (file.Length == 0 || file.Length > MaximumProfilePictureSizeBytes))
        {
            ModelState.AddModelError("Edit.ProfilePictureFile",
                "Choose a non-empty JPEG, PNG, or WebP image no larger than 2 MB.");
        }

        if (!ModelState.IsValid)
        {
            return await ProfileForEditAsync(edit, cancellationToken);
        }

        var result = await api.PutJsonAsync<object, ParticipantProfile>(
            "api/users/profile", new { edit.FullName }, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            var page = await LoadProfilePageAsync(edit, cancellationToken);
            page.ErrorMessage = result.ErrorMessage ?? "Your profile could not be updated.";
            return View("Profile", page);
        }

        if (edit.ProfilePictureFile is not null)
        {
            var pictureResult = await api.PostFileAsync<ProfilePictureUploadResponse>(
                "api/users/profile/picture", edit.ProfilePictureFile, cancellationToken);
            if (!pictureResult.IsSuccess)
            {
                TempData["ParticipantError"] =
                    $"Your profile details were saved, but the picture upload failed. {pictureResult.ErrorMessage ?? "Choose a valid image and try again."}";
                return RedirectToAction(nameof(Profile));
            }
        }

        TempData["ParticipantSuccess"] = "Your profile was updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("profile/picture")]
    public async Task<IActionResult> ProfilePicture(CancellationToken cancellationToken)
    {
        var result = await api.GetFileAsync("api/users/profile/picture", cancellationToken);
        if (!result.IsSuccess)
        {
            return result.StatusCode == System.Net.HttpStatusCode.NotFound
                ? NotFound()
                : StatusCode(StatusCodes.Status502BadGateway);
        }

        Response.Headers.CacheControl = "no-store, private";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Content!, result.ContentType!);
    }

    private async Task<IActionResult> ProfileForEditAsync(
        UpdateParticipantProfileModel edit,
        CancellationToken cancellationToken) =>
        View("Profile", await LoadProfilePageAsync(edit, cancellationToken));

    private async Task<ParticipantProfileViewModel> LoadProfilePageAsync(
        UpdateParticipantProfileModel edit,
        CancellationToken cancellationToken)
    {
        var result = await api.GetJsonAsync<ParticipantProfile>("api/users/profile", cancellationToken);
        return new ParticipantProfileViewModel
        {
            Profile = result.Value,
            Edit = edit,
            SuccessMessage = TempData["ParticipantSuccess"] as string,
            ErrorMessage = result.IsSuccess ? TempData["ParticipantError"] as string : result.ErrorMessage
        };
    }
}
