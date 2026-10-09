using Microsoft.AspNetCore.Mvc;
using WeatherWebApp.Models;
using WeatherWebApp.Services;

namespace WeatherWebApp.Controllers;

public class AccountController : Controller
{
    private readonly IApiClient _api;
    public AccountController(IApiClient api) => _api = api;

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        var token = await _api.LoginInternalAsync(model.Username, model.Password);
        if (token == null)
        {
            ViewBag.Error = "Credenciais inválidas";
            return View(model);
        }

        HttpContext.Session.SetString("Token", token);
        HttpContext.Session.SetString("Username", model.Username);

        // Lê a role diretamente do token JWT
        var role = JwtHelper.GetClaim(token, "role") ?? "User";
        HttpContext.Session.SetString("Role", role);

        return RedirectToAction("Index", "Temperature");
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login");
    }

    public IActionResult AccessDenied() => View();
}