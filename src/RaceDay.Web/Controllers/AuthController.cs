using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Models;
using RaceDay.Web.Security;
using RaceDay.Web.Services;

namespace RaceDay.Web.Controllers;

public sealed class AuthController(RaceDayApiClient api, ApiSessionContext session) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (model.Role is not "Organiser" and not "Participant")
        {
            ModelState.AddModelError(nameof(model.Role), "Choose Organiser or Participant.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await api.RegisterAsync(new
        {
            model.FullName,
            model.Email,
            model.Password,
            model.Role
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Registration could not be completed.");
            return View(model);
        }

        return RedirectToAction(nameof(Login), new { registered = true });
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool registered = false, bool loggedOut = false, bool sessionExpired = false)
    {
        ViewData["Registered"] = registered;
        ViewData["LoggedOut"] = loggedOut;
        ViewData["SessionExpired"] = sessionExpired;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await api.LoginAsync(new { model.Email, model.Password }, cancellationToken);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Email or password is incorrect.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        if (result.User?.Role == "Participant")
        {
            return RedirectToAction("Index", "Participant");
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RaceDayAuthorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        try
        {
            var result = await api.LogoutAsync(cancellationToken);
            if (!result.IsSuccess)
            {
                TempData["AuthMessage"] = "You have been signed out of this website. The API could not confirm the sign-out.";
            }
        }
        catch (HttpRequestException)
        {
            session.Clear();
            TempData["AuthMessage"] = "You have been signed out of this website. The API was unavailable to confirm the sign-out.";
        }

        return RedirectToAction(nameof(Login), new { loggedOut = true });
    }
}
