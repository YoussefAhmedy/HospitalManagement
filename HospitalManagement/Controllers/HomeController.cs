using System.Diagnostics;
using Hospital.BLL.Helpers;
using Hospital.BLL.ModelVM;
using Hospital.BLL.Services.Abstraction;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagement.Controllers;

public sealed class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IDoctorService _doctorService;

    public HomeController(ILogger<HomeController> logger, IDoctorService doctorService)
    {
        _logger = logger;
        _doctorService = doctorService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User.IsInRole(Role.Admin.ToString()))
        {
            return RedirectToAction("Index", "Admin");
        }

        return View(new MainVm
        {
            Doctors = _doctorService.GetPublicDoctorProfiles(maximumResults: 6)
        });
    }

    [HttpGet]
    public IActionResult AboutUs() => RedirectToAction(nameof(Index));

    [HttpGet]
    public IActionResult Privacy() => View();

    [HttpGet]
    public IActionResult Terms() => View();

    [HttpGet]
    public IActionResult Security() => View();

    [HttpGet]
    public IActionResult Support() => View();

    [HttpGet]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        ViewData["RequestId"] = requestId;
        _logger.LogWarning("An unhandled request reached the error page. Trace identifier: {TraceIdentifier}", requestId);
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        return View();
    }
}
