using Microsoft.AspNetCore.Mvc;
using WeatherWebApp.Models;
using WeatherWebApp.Services;

namespace WeatherWebApp.Controllers;

public class AdminDevicesController : Controller
{
    private readonly IApiClient _api;
    public AdminDevicesController(IApiClient api) => _api = api;

    private string? Token => HttpContext.Session.GetString("Token");
    private bool IsAdmin => HttpContext.Session.GetString("Role") == "Admin";

    private IActionResult? Guard()
    {
        if (Token == null) return RedirectToAction("Login", "Account");
        if (!IsAdmin) return RedirectToAction("AccessDenied", "Account");
        return null;
    }

    // GET /AdminDevices
    public async Task<IActionResult> Index()
    {
        var guard = Guard();
        if (guard != null) return guard;

        var devices = await _api.GetDevicesAsync(Token!) ?? new();
        return View(devices);
    }

    // GET /AdminDevices/Create
    [HttpGet]
    public IActionResult Create()
    {
        var guard = Guard();
        if (guard != null) return guard;

        return View(new CreateDeviceViewModel());
    }

    // POST /AdminDevices/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDeviceViewModel model)
    {
        var guard = Guard();
        if (guard != null) return guard;

        if (!ModelState.IsValid) return View(model);

        var result = await _api.CreateDeviceAsync(Token!, model);
        if (result == null)
        {
            ViewBag.Error = "Não foi possível criar o dispositivo.";
            return View(model);
        }

        // Redireciona para a página que mostra a API Key (uma única vez)
        TempData["ApiKey"] = result.ApiKey;
        TempData["DeviceName"] = result.Name;
        TempData["DeviceLocation"] = result.Location;
        return RedirectToAction(nameof(Created), new { id = result.Id });
    }

    // GET /AdminDevices/Created/{id}
    // Mostra a API Key. Numa app real, o valor não viria por query string,
    // mas viria em TempData. Para simplificar, usamos TempData com a chave.
    [HttpGet]
    public IActionResult Created(int id)
    {
        var guard = Guard();
        if (guard != null) return guard;

        var apiKey = TempData["ApiKey"] as string;
        var name = TempData["DeviceName"] as string;
        var location = TempData["DeviceLocation"] as string;

        if (apiKey == null)
        {
            // Se o utilizador recarregar a página, a chave perdeu-se.
            TempData["Error"] = "A API Key só é mostrada uma vez. " +
                                "Se a perdeu, use a opção Regenerar.";
            return RedirectToAction(nameof(Index));
        }

        return View(new CreateDeviceResultViewModel
        {
            Id = id,
            Name = name ?? string.Empty,
            Location = location,
            ApiKey = apiKey
        });
    }

    // GET /AdminDevices/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var guard = Guard();
        if (guard != null) return guard;

        var device = await _api.GetDeviceDetailsAsync(Token!, id);
        if (device == null) return NotFound();

        return View(device);
    }

    // POST /AdminDevices/Activate/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        var guard = Guard();
        if (guard != null) return guard;

        await _api.ActivateDeviceAsync(Token!, id);
        TempData["Success"] = "Dispositivo ativado.";
        return RedirectToAction(nameof(Index));
    }

    // POST /AdminDevices/Deactivate/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var guard = Guard();
        if (guard != null) return guard;

        await _api.DeactivateDeviceAsync(Token!, id);
        TempData["Success"] = "Dispositivo desativado.";
        return RedirectToAction(nameof(Index));
    }

    // POST /AdminDevices/Delete/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var guard = Guard();
        if (guard != null) return guard;

        await _api.DeleteDeviceAsync(Token!, id);
        TempData["Success"] = "Dispositivo eliminado.";
        return RedirectToAction(nameof(Index));
    }

    // POST /AdminDevices/RequestReading/{id}
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReading(int id)
    {
        var guard = Guard();
        if (guard != null) return guard;

        var result = await _api.RequestReadingAsync(Token!, id);
        return View("RequestReading", result);
    }
}