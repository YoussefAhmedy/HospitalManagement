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
        // A signed-in user belongs in their role workspace, not on the public overview.
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
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

    /// <summary>
    /// Sign-in guide for a demonstration deployment. Deliberately public: the accounts it lists
    /// are synthetic, share one published password, and exist only when demo seeding is enabled.
    /// Hosted environments that hold real data must leave <c>Demo:Seed</c> disabled, in which case
    /// these accounts do not exist.
    /// </summary>
    [HttpGet]
    public IActionResult Demo() => View();

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
