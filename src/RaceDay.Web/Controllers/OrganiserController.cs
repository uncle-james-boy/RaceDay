using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("organiser")]
[RaceDayAuthorize("Organiser")]
public sealed class OrganiserController(RaceDayApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await LoadDashboardAsync(cancellationToken);
        model.ErrorMessage = TempData["OrganiserError"] as string ?? model.ErrorMessage;
        model.SuccessMessage = TempData["OrganiserSuccess"] as string;
        return View(model);
    }

    [HttpGet("events/create")]
    public IActionResult CreateEvent() => View("EventForm", new EventFormViewModel
    {
        Event = new EventEditModel { EventDate = DateTime.Today.AddDays(1) }
    });

    [HttpPost("events/create")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(EventEditModel.MaximumBannerFileSizeBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = EventEditModel.MaximumBannerFileSizeBytes)]
    public async Task<IActionResult> CreateEvent([Bind(Prefix = "Event")] EventEditModel model, CancellationToken cancellationToken)
    {
        ValidateBannerSize(model.BannerFile);
        ValidateEventType(model.EventType);
        if (!ModelState.IsValid) return View("EventForm", new EventFormViewModel { Event = model });

        var result = await api.PostJsonAsync<object, OrganiserEvent>("api/events", new
        {
            model.Name, model.Description, model.EventDate, model.Location, model.Distance, model.EventType
        }, cancellationToken);
        var createdEventId = result.Value?.EventID ?? 0;
        if ((int)result.StatusCode is >= 200 and < 300
            && createdEventId <= 0)
        {
            TempData["OrganiserError"] =
                "The API did not return a usable event ID. The event may already have been created; check your dashboard before submitting again.";
            return RedirectToAction(nameof(Index));
        }

        if (!result.IsSuccess)
        {
            return View("EventForm", new EventFormViewModel { Event = model, ErrorMessage = result.ErrorMessage });
        }

        if (model.BannerFile is not null)
        {
            var bannerResult = await UploadBannerAsync(createdEventId, model.BannerFile, cancellationToken);
            if (!bannerResult.IsSuccess)
            {
                TempData["OrganiserError"] =
                    $"The event was created, but its banner was not uploaded. {bannerResult.ErrorMessage ?? "Select the event from your dashboard and retry the upload."}";
                return RedirectToAction(nameof(EditEvent), new { eventId = createdEventId });
            }
        }

        TempData["OrganiserSuccess"] = "The event was created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("events/{eventId:int}/edit")]
    public async Task<IActionResult> EditEvent(int eventId, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(eventId, cancellationToken)) return NotFound();
        var result = await api.GetJsonAsync<ParticipantEventDetails>($"api/events/{eventId}", cancellationToken);
        if (!result.IsSuccess || result.Value is null) return ApiFailure(result.StatusCode);
        return View("EventForm", new EventFormViewModel
        {
            ErrorMessage = TempData["OrganiserError"] as string,
            SuccessMessage = TempData["OrganiserSuccess"] as string,
            Event = new EventEditModel
            {
                EventID = result.Value.EventID,
                Name = result.Value.Name,
                Description = result.Value.Description,
                EventDate = result.Value.EventDate,
                Location = result.Value.Location,
                Distance = result.Value.Distance,
                EventType = result.Value.EventType,
                EventBannerUrl = result.Value.EventBannerUrl
            }
        });
    }

    [HttpPost("events/{eventId:int}/edit")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(EventEditModel.MaximumBannerFileSizeBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = EventEditModel.MaximumBannerFileSizeBytes)]
    public async Task<IActionResult> EditEvent(int eventId, [Bind(Prefix = "Event")] EventEditModel model, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(eventId, cancellationToken)) return NotFound();
        ValidateBannerSize(model.BannerFile);
        ValidateEventType(model.EventType);
        if (!ModelState.IsValid) return View("EventForm", new EventFormViewModel { Event = model });

        var result = await api.PutJsonAsync<object, ParticipantEventDetails>($"api/events/{eventId}", new
        {
            model.Name, model.Description, model.EventDate, model.Location, model.Distance, model.EventType
        }, cancellationToken);
        if (!result.IsSuccess)
        {
            return View("EventForm", new EventFormViewModel { Event = model, ErrorMessage = result.ErrorMessage });
        }

        if (model.BannerFile is not null)
        {
            var bannerResult = await UploadBannerAsync(eventId, model.BannerFile, cancellationToken);
            if (!bannerResult.IsSuccess)
            {
                TempData["OrganiserError"] =
                    $"The event details were saved, but its banner was not uploaded. {bannerResult.ErrorMessage ?? "Select a valid image and retry."}";
                return RedirectToAction(nameof(EditEvent), new { eventId });
            }
        }

        TempData["OrganiserSuccess"] = "The event was updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("events/{eventId:int}/delete")]
    public async Task<IActionResult> DeleteEvent(int eventId, CancellationToken cancellationToken)
    {
        var owned = await GetOwnedEventAsync(eventId, cancellationToken);
        return owned is null ? NotFound() : View("DeleteEvent", new EventDeleteViewModel
        {
            EventID = owned.EventID,
            Name = owned.Name
        });
    }

    [HttpPost("events/{eventId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEventConfirmed(int eventId, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(eventId, cancellationToken)) return NotFound();
        var result = await api.DeleteAsync($"api/events/{eventId}", cancellationToken);
        if (!result.IsSuccess)
        {
            TempData["OrganiserError"] = result.ErrorMessage ?? "The event could not be deleted.";
            return RedirectToAction(nameof(Index));
        }

        TempData["OrganiserSuccess"] = "The event was deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("events/{eventId:int}/categories")]
    public async Task<IActionResult> Categories(int eventId, CancellationToken cancellationToken)
    {
        var model = await LoadCategoriesAsync(eventId, new CategoryEditModel { EventID = eventId }, cancellationToken);
        if (model is null) return NotFound();
        model.ErrorMessage ??= TempData["OrganiserError"] as string;
        model.SuccessMessage = TempData["OrganiserSuccess"] as string;
        return View("Categories", model);
    }

    [HttpPost("events/{eventId:int}/categories/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(int eventId, [Bind(Prefix = "Edit")] CategoryEditModel edit, CancellationToken cancellationToken)
    {
        if (!await OwnsEventAsync(eventId, cancellationToken)) return NotFound();
        edit.EventID = eventId;
        ValidateCategory(edit);
        if (!ModelState.IsValid)
        {
            var page = await LoadCategoriesAsync(eventId, edit, cancellationToken);
            return page is null ? NotFound() : View("Categories", page);
        }

        var result = await api.PostJsonAsync<object, OrganiserCategory>($"api/events/{eventId}/categories", new
        {
            edit.CategoryName, edit.MinAge, edit.MaxAge, edit.Distance
        }, cancellationToken);
        TempData[result.IsSuccess ? "OrganiserSuccess" : "OrganiserError"] = result.IsSuccess
            ? "The category was added."
            : result.ErrorMessage ?? "The category could not be added.";
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpGet("events/{eventId:int}/categories/{categoryId:int}/edit")]
    public async Task<IActionResult> EditCategory(int eventId, int categoryId, CancellationToken cancellationToken)
    {
        var page = await LoadCategoriesAsync(eventId, new CategoryEditModel { EventID = eventId }, cancellationToken);
        var category = page?.Categories.SingleOrDefault(item => item.CategoryID == categoryId);
        if (page is null || category is null) return NotFound();
        page.Edit = ToEditModel(category);
        return View("Categories", page);
    }

    [HttpPost("events/{eventId:int}/categories/{categoryId:int}/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int eventId, int categoryId, [Bind(Prefix = "Edit")] CategoryEditModel edit, CancellationToken cancellationToken)
    {
        var page = await LoadCategoriesAsync(eventId, edit, cancellationToken);
        if (page is null || page.Categories.All(item => item.CategoryID != categoryId)) return NotFound();
        edit.EventID = eventId;
        ValidateCategory(edit);
        if (!ModelState.IsValid) return View("Categories", page);

        var result = await api.PutJsonAsync<object, OrganiserCategory>($"api/categories/{categoryId}", new
        {
            edit.CategoryName, edit.MinAge, edit.MaxAge, edit.Distance
        }, cancellationToken);
        TempData[result.IsSuccess ? "OrganiserSuccess" : "OrganiserError"] = result.IsSuccess
            ? "The category was updated."
            : result.ErrorMessage ?? "The category could not be updated.";
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpPost("events/{eventId:int}/categories/{categoryId:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int eventId, int categoryId, CancellationToken cancellationToken)
    {
        var page = await LoadCategoriesAsync(eventId, new CategoryEditModel { EventID = eventId }, cancellationToken);
        if (page is null || page.Categories.All(item => item.CategoryID != categoryId)) return NotFound();
        var result = await api.DeleteAsync($"api/categories/{categoryId}", cancellationToken);
        TempData[result.IsSuccess ? "OrganiserSuccess" : "OrganiserError"] = result.IsSuccess
            ? "The category was deleted."
            : result.ErrorMessage ?? "The category could not be deleted.";
        return RedirectToAction(nameof(Categories), new { eventId });
    }

    [HttpGet("events/{eventId:int}/enrolments")]
    public async Task<IActionResult> Enrolments(int eventId, CancellationToken cancellationToken) =>
        await LoadRosterViewAsync(eventId, cancellationToken);

    [HttpGet("events/{eventId:int}/results")]
    public async Task<IActionResult> Results(int eventId, CancellationToken cancellationToken) =>
        await LoadRosterViewAsync(eventId, cancellationToken);

    [HttpPost("events/{eventId:int}/results")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateResult(
        int eventId,
        CreateResultModel edit,
        CancellationToken cancellationToken)
    {
        var page = await BuildRosterAsync(eventId, cancellationToken);
        if (page is null) return NotFound();
        if (page.Enrolments.All(item => item.EnrolmentID != edit.EnrolmentID))
        {
            TempData["OrganiserError"] = "Choose an enrolment from this event.";
            return RedirectToAction(nameof(Results), new { eventId });
        }

        if (!TimeSpan.TryParseExact(edit.FinishTime, "c", CultureInfo.InvariantCulture, out var finishTime)
            || finishTime <= TimeSpan.Zero)
        {
            ModelState.AddModelError(nameof(edit.FinishTime), "Enter a finish time greater than zero as HH:MM:SS.");
        }

        if (page.Results.Any(item => item.EnrolmentID == edit.EnrolmentID))
        {
            ModelState.AddModelError(string.Empty, "A result already exists for this participant.");
        }

        if (!ModelState.IsValid)
        {
            page.ErrorMessage = string.Join(" ", ModelState.Values.SelectMany(item => item.Errors).Select(item => item.ErrorMessage));
            return View("Roster", page);
        }

        var result = await api.PostJsonAsync<object, OrganiserRaceResult>("api/results", new
        {
            edit.EnrolmentID,
            FinishTime = finishTime,
            edit.Position
        }, cancellationToken);
        TempData[result.IsSuccess ? "OrganiserSuccess" : "OrganiserError"] = result.IsSuccess
            ? "The result was recorded and is now available to the Participant."
            : result.ErrorMessage ?? "The result could not be recorded.";
        return RedirectToAction(nameof(Results), new { eventId });
    }

    private async Task<IActionResult> LoadRosterViewAsync(int eventId, CancellationToken cancellationToken)
    {
        var model = await BuildRosterAsync(eventId, cancellationToken);
        if (model is null) return NotFound();
        model.ErrorMessage = TempData["OrganiserError"] as string;
        model.SuccessMessage = TempData["OrganiserSuccess"] as string;
        return View("Roster", model);
    }

    private async Task<OrganiserRosterViewModel?> BuildRosterAsync(int eventId, CancellationToken cancellationToken)
    {
        var ownedEvent = await GetOwnedEventAsync(eventId, cancellationToken);
        if (ownedEvent is null) return null;

        var enrolments = await api.GetJsonAsync<OrganiserEnrolment[]>(
            $"api/events/{eventId}/enrolments", cancellationToken);
        var results = await api.GetJsonAsync<OrganiserRaceResult[]>(
            $"api/events/{eventId}/results", cancellationToken);
        return new OrganiserRosterViewModel
        {
            EventID = eventId,
            EventName = ownedEvent.Name,
            Enrolments = enrolments.Value ?? [],
            Results = results.Value ?? [],
            ErrorMessage = !enrolments.IsSuccess ? enrolments.ErrorMessage
                : !results.IsSuccess ? results.ErrorMessage : null
        };
    }

    private async Task<OrganiserDashboardViewModel> LoadDashboardAsync(CancellationToken cancellationToken)
    {
        var result = await api.GetJsonAsync<OrganiserEvent[]>("api/events/mine", cancellationToken);
        return new OrganiserDashboardViewModel
        {
            Events = result.Value ?? [],
            ErrorMessage = result.IsSuccess ? null : result.ErrorMessage
        };
    }

    private async Task<OrganiserCategoriesViewModel?> LoadCategoriesAsync(
        int eventId,
        CategoryEditModel edit,
        CancellationToken cancellationToken)
    {
        var raceEvent = await GetOwnedEventAsync(eventId, cancellationToken);
        if (raceEvent is null) return null;
        var result = await api.GetJsonAsync<OrganiserCategory[]>($"api/events/{eventId}/categories", cancellationToken);
        return new OrganiserCategoriesViewModel
        {
            EventID = eventId,
            EventName = raceEvent.Name,
            Categories = result.Value ?? [],
            Edit = edit,
            ErrorMessage = result.IsSuccess ? null : result.ErrorMessage
        };
    }

    private async Task<OrganiserEvent?> GetOwnedEventAsync(int eventId, CancellationToken cancellationToken)
    {
        var owned = await api.GetJsonAsync<OrganiserEvent[]>("api/events/mine", cancellationToken);
        return owned.Value?.SingleOrDefault(item => item.EventID == eventId);
    }

    private async Task<bool> OwnsEventAsync(int eventId, CancellationToken cancellationToken) =>
        await GetOwnedEventAsync(eventId, cancellationToken) is not null;

    private async Task<ApiCallResult<EventBannerUploadResponse>> UploadBannerAsync(
        int eventId,
        IFormFile file,
        CancellationToken cancellationToken) =>
        await api.PostFileAsync<EventBannerUploadResponse>(
            $"api/events/{eventId}/banner", file, cancellationToken);

    private void ValidateBannerSize(IFormFile? file)
    {
        if (file is not null && (file.Length == 0 || file.Length > EventEditModel.MaximumBannerFileSizeBytes))
        {
            ModelState.AddModelError("Event.BannerFile", "Choose a non-empty JPEG, PNG, or WebP image no larger than 5 MB.");
        }
    }

    private IActionResult ApiFailure(System.Net.HttpStatusCode statusCode) =>
        statusCode == System.Net.HttpStatusCode.NotFound ? NotFound() : StatusCode(503);

    private void ValidateEventType(string eventType)
    {
        if (eventType is not "Run" and not "Walk" and not "Cycle")
        {
            ModelState.AddModelError("Event.EventType", "Choose Run, Walk, or Cycle.");
        }
    }

    private void ValidateCategory(CategoryEditModel edit)
    {
        if (edit.MinAge.HasValue && edit.MaxAge.HasValue && edit.MinAge > edit.MaxAge)
        {
            ModelState.AddModelError("Edit.MaxAge", "Maximum age must be at least the minimum age.");
        }
    }

    private static CategoryEditModel ToEditModel(OrganiserCategory category) => new()
    {
        CategoryID = category.CategoryID,
        EventID = category.EventID,
        CategoryName = category.CategoryName,
        MinAge = category.MinAge,
        MaxAge = category.MaxAge,
        Distance = category.Distance
    };
}

public sealed record ParticipantEventDetails(int EventID, string Name, string? Description,
    DateTime EventDate, string Location, decimal Distance, string EventType, string? EventBannerUrl);
