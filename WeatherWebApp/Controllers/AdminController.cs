using Microsoft.AspNetCore.Mvc;
using WeatherWebApp.Models;
using WeatherWebApp.Services;

namespace WeatherWebApp.Controllers;

public class AdminController : Controller
{
    private readonly IApiClient _api;
    public AdminController(IApiClient api) => _api = api;

    private string? Token => HttpContext.Session.GetString("Token");
    private bool IsAdmin => HttpContext.Session.GetString("Role") == "Admin";

    public async Task<IActionResult> Users()
    {
        if (Token == null) return RedirectToAction("Login", "Account");
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Account");

        var users = await _api.GetAllUsersAsync(Token) ?? new();
        return View(users);
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (Token == null) return RedirectToAction("Login", "Account");
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Account");
        return View(new CreateUserViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (Token == null) return RedirectToAction("Login", "Account");
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Account");

        if (!ModelState.IsValid) return View(model);

        var (success, message) = await _api.CreateUserAsync(Token, model);
        if (!success)
        {
            ViewBag.Error = message;
            return View(model);
        }

        TempData["Success"] = "Utilizador criado com sucesso";
        return RedirectToAction(nameof(Users));
    }
}