using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

[Route("participant")]
[RaceDayAuthorize("Participant")]
public sealed class ParticipantController(RaceDayApiClient api) : Controller
{
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
                FullName = result.Value.FullName,
                ProfilePictureUrl = result.Value.ProfilePictureUrl
            },
            SuccessMessage = TempData["ParticipantSuccess"] as string
        });
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(
        [Bind(Prefix = "Edit")] UpdateParticipantProfileModel edit,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ProfileForEditAsync(edit, cancellationToken);
        }

        var result = await api.PutJsonAsync<UpdateParticipantProfileModel, ParticipantProfile>(
            "api/users/profile", edit, cancellationToken);
        if (!result.IsSuccess || result.Value is null)
        {
            var page = await LoadProfilePageAsync(edit, cancellationToken);
            page.ErrorMessage = result.ErrorMessage ?? "Your profile could not be updated.";
            return View("Profile", page);
        }

        TempData["ParticipantSuccess"] = "Your profile was updated.";
        return RedirectToAction(nameof(Profile));
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
            ErrorMessage = result.IsSuccess ? null : result.ErrorMessage
        };
    }
}
