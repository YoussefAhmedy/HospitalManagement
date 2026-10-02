using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using AutoMapper;
using Hospital.BLL.Helpers;
using Hospital.BLL.ModelVM;
using Hospital.BLL.Notifications;
using Hospital.BLL.Services.Abstraction;
using Hospital.DAL.Entities;
using HospitalManagement.Infrastructure;
using HospitalManagement.Services;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;

namespace HospitalManagement.Controllers;

public sealed class AccountController : Controller
{
    private readonly IMapper _mapper;
    private readonly IEmailQueue _emailQueue;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IAuditLogger _auditLogger;
    private readonly IProfileImageStorage _profileImages;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        IMapper mapper,
        IEmailQueue emailQueue,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger,
        IAuditLogger auditLogger,
        IProfileImageStorage profileImages,
        IWebHostEnvironment environment)
    {
        _mapper = mapper;
        _emailQueue = emailQueue;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _auditLogger = auditLogger;
        _profileImages = profileImages;
        _environment = environment;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        var model = new RegisterViewModel
        {
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            return View(model);
        }

        var email = model.Email.Trim();
        var patient = _mapper.Map<Patient>(model);
        patient.Email = email;
        patient.UserName = email;
        patient.CreatedAt = DateTime.UtcNow;

        var createResult = await _userManager.CreateAsync(patient, model.Password);
        if (!createResult.Succeeded)
        {
            AddIdentityErrors(createResult);
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            return View(model);
        }

        var roleResult = await _userManager.AddToRoleAsync(patient, Role.Patient.ToString());
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(patient);
            _logger.LogError("Patient role assignment failed after account creation.");
            ModelState.AddModelError(string.Empty, "We could not complete registration. Please try again.");
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            return View(model);
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(patient);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var confirmationUrl = Url.Action(
            nameof(ConfirmEmail), "Account", new { id = patient.Id, code = encodedToken }, Request.Scheme);
        var queued = confirmationUrl is not null && _emailQueue.TryEnqueue(
            email,
            "Confirm your CareAxis account",
            EmailTemplates.AccountConfirmation(patient.FirstName, confirmationUrl));

        TempData["RegistrationNotice"] = queued
            ? "Your account is ready. Check your email for a verification link; sign-in is enabled after verification."
            : "Your account was created, but email delivery is not configured right now. Contact your deployment administrator to complete verification.";
        TempData["RegistrationEmail"] = email;

        _logger.LogInformation("A patient account was created; verification is pending.");
        return RedirectToAction(nameof(Registered));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Registered() => View(new ForgetPasswordVM
    {
        Email = TempData["RegistrationEmail"] as string ?? string.Empty
    });

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ConfirmEmail(string? id, string? code)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code))
        {
            return BadRequest();
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return BadRequest();
        }

        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
        {
            return View("ConfirmationFailed");
        }

        await _auditLogger.RecordAsync("account.email-confirmed", user.Id, user.Id, "user", user.Id);
        TempData["StatusMessage"] = "Your email is verified. You can now sign in.";
        return RedirectToAction(nameof(LogIn));
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ResendConfirmation(ForgetPasswordVM model)
    {
        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (user is not null && !user.EmailConfirmed)
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
                var url = Url.Action(nameof(ConfirmEmail), "Account", new { id = user.Id, code = encodedToken }, Request.Scheme);
                if (url is not null)
                {
                    _emailQueue.TryEnqueue(user.Email!, "Confirm your CareAxis account",
                        EmailTemplates.AccountConfirmation(user.FirstName, url));
                }
            }
        }

        TempData["RegistrationNotice"] = "If verification is available for that account, a new link will be sent shortly.";
        TempData["RegistrationEmail"] = model.Email?.Trim() ?? string.Empty;
        return RedirectToAction(nameof(Registered));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> LogIn(string? returnUrl)
    {
        var model = new LogInViewModel { ReturnUrl = returnUrl };
        model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return View(model);
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> LogIn(LogInViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        var result = user is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded && user is not null)
        {
            await _auditLogger.RecordAsync("auth.login-succeeded", user.Id, user.Id, "user", user.Id);
            _logger.LogInformation("Authentication succeeded.");

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return LocalRedirect(model.ReturnUrl);
            }

            return await RedirectToRoleHomeAsync(user);
        }

        await _auditLogger.RecordAsync(
            result.IsLockedOut ? "auth.login-locked" : "auth.login-failed",
            user?.Id,
            user?.Id,
            user is null ? null : "user",
            user?.Id);
        ModelState.AddModelError(string.Empty, "Email or password was not accepted. Check your details and try again.");
        model.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> GoogleAuth(string? returnUrl)
    {
        var googleIsConfigured = (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .Any(scheme => scheme.Name == GoogleDefaults.AuthenticationScheme);
        if (!googleIsConfigured)
        {
            TempData["StatusMessage"] = "Google sign-in is not configured for this deployment.";
            return RedirectToAction(nameof(LogIn), new { returnUrl });
        }

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return RedirectToAction(nameof(ExternalAuth), new
        {
            returnUrl = safeReturnUrl,
            provider = GoogleDefaults.AuthenticationScheme
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalAuth(string? returnUrl, string? provider)
    {
        var providerIsEnabled = (await _signInManager.GetExternalAuthenticationSchemesAsync())
            .Any(scheme => scheme.Name == provider);
        if (!providerIsEnabled || string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        var callbackUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl = safeReturnUrl }, Request.Scheme);
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, callbackUrl);
        return new ChallengeResult(provider, properties);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl, string? remoteError)
    {
        if (!string.IsNullOrWhiteSpace(remoteError))
        {
            _logger.LogWarning("External authentication provider returned an error.");
            TempData["StatusMessage"] = "External sign-in could not be completed. Please try again.";
            return RedirectToAction(nameof(LogIn));
        }

        var externalInfo = await _signInManager.GetExternalLoginInfoAsync();
        if (externalInfo is null)
        {
            TempData["StatusMessage"] = "External sign-in could not be completed. Please try again.";
            return RedirectToAction(nameof(LogIn));
        }

        var signIn = await _signInManager.ExternalLoginSignInAsync(
            externalInfo.LoginProvider, externalInfo.ProviderKey, isPersistent: false, bypassTwoFactor: false);
        if (signIn.Succeeded)
        {
            var externalUser = await _userManager.FindByLoginAsync(externalInfo.LoginProvider, externalInfo.ProviderKey);
            if (externalUser is not null)
            {
                await _auditLogger.RecordAsync("auth.external-login-succeeded", externalUser.Id, externalUser.Id, "user", externalUser.Id);
            }
            return SafeLocalRedirect(returnUrl);
        }

        var email = externalInfo.Principal.FindFirstValue(ClaimTypes.Email);
        var verified = string.Equals(
            externalInfo.Principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        if (!verified || string.IsNullOrWhiteSpace(email) || !MailAddress.TryCreate(email, out _))
        {
            TempData["StatusMessage"] = "This provider did not verify an email address for the account.";
            return RedirectToAction(nameof(LogIn));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new Patient
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = externalInfo.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "Patient",
                LastName = externalInfo.Principal.FindFirstValue(ClaimTypes.Surname) ?? "",
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning("External sign-in account creation failed.");
                TempData["StatusMessage"] = "External sign-in could not be completed. Please try again.";
                return RedirectToAction(nameof(LogIn));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, Role.Patient.ToString());
            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                _logger.LogError("Patient role assignment failed after external account creation.");
                TempData["StatusMessage"] = "External sign-in could not be completed. Please try again.";
                return RedirectToAction(nameof(LogIn));
            }
        }
        else if (!await _userManager.IsInRoleAsync(user, Role.Patient.ToString()))
        {
            // Do not attach public social identities to staff or administrator accounts.
            TempData["StatusMessage"] = "This account cannot be linked through public sign-in.";
            return RedirectToAction(nameof(LogIn));
        }

        if (!await _userManager.IsEmailConfirmedAsync(user))
        {
            user.EmailConfirmed = true;
            var confirmationResult = await _userManager.UpdateAsync(user);
            if (!confirmationResult.Succeeded)
            {
                TempData["StatusMessage"] = "External sign-in could not be completed. Please try again.";
                return RedirectToAction(nameof(LogIn));
            }
        }

        var linkResult = await _userManager.AddLoginAsync(user, externalInfo);
        if (!linkResult.Succeeded)
        {
            _logger.LogWarning("External account linking failed.");
            TempData["StatusMessage"] = "External sign-in could not be completed. Please try again.";
            return RedirectToAction(nameof(LogIn));
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        await _auditLogger.RecordAsync("auth.external-account-created", user.Id, user.Id, "user", user.Id);
        return SafeLocalRedirect(returnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgetPassword() => View(new ForgetPasswordVM { Email = string.Empty });

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ForgetPassword(ForgetPasswordVM model)
    {
        if (ModelState.IsValid)
        {
            await QueuePasswordResetAsync(model.Email.Trim());
        }

        TempData["PasswordResetNotice"] = "If an eligible account exists for that address, password reset instructions will be sent shortly.";
        return RedirectToAction(nameof(ResetConfirmation));
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ResendPasswordReset(ForgetPasswordVM model)
    {
        if (ModelState.IsValid)
        {
            await QueuePasswordResetAsync(model.Email.Trim());
        }

        TempData["PasswordResetNotice"] = "If an eligible account exists for that address, password reset instructions will be sent shortly.";
        return RedirectToAction(nameof(ResetConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetConfirmation() => View(new ForgetPasswordVM { Email = string.Empty });

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public IActionResult ResetPassword(string? code, string? email)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email))
        {
            return BadRequest();
        }

        return View(new ResetPasswordVM { Code = code, Email = email });
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null)
        {
            return RedirectToAction(nameof(ResetConfirmation));
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
        }
        catch (FormatException)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or expired. Request a new one.");
            return View(model);
        }

        var result = await _userManager.ResetPasswordAsync(user, decodedToken, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or expired. Request a new one.");
            return View(model);
        }

        await _userManager.UpdateSecurityStampAsync(user);
        await _auditLogger.RecordAsync("auth.password-reset", user.Id, user.Id, "user", user.Id);
        TempData["StatusMessage"] = "Your password was updated. Sign in with your new password.";
        return RedirectToAction(nameof(LogIn));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> LogOut()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await _auditLogger.RecordAsync("auth.logout", userId, userId, "user", userId);
        }

        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        return View(_mapper.Map<EditProfileVm>(user));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> EditProfile(EditProfileVm model, IFormFile? imageFile, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View("Profile", model);
        }

        if (imageFile is not null && imageFile.Length > 0)
        {
            var storedImage = await _profileImages.SaveAsync(imageFile, cancellationToken);
            if (storedImage is null)
            {
                ModelState.AddModelError(nameof(imageFile), "Choose a valid JPG, PNG, or WebP image no larger than 5 MB.");
                return View("Profile", model);
            }
            user.Image = storedImage.FileName;
        }

        user.FirstName = model.FirstName.Trim();
        user.LastName = model.LastName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        user.DateOfBirth = model.DateOfBirth;
        user.Gender = model.Gender;

        if (!string.Equals(model.Email?.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var requestedEmail = model.Email.Trim();
            var existingUser = await _userManager.FindByEmailAsync(requestedEmail);
            if (existingUser is not null && existingUser.Id != user.Id)
            {
                ModelState.AddModelError(nameof(model.Email), "That email address is already in use.");
                return View("Profile", model);
            }

            var changeToken = await _userManager.GenerateChangeEmailTokenAsync(user, requestedEmail);
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(changeToken));
            var confirmationUrl = Url.Action(
                nameof(ConfirmEmailChange), "Account",
                new { newEmail = requestedEmail, code = encodedToken }, Request.Scheme);
            if (confirmationUrl is not null)
            {
                var queued = _emailQueue.TryEnqueue(requestedEmail, "Confirm your new CareAxis email",
                    EmailTemplates.EmailChangeConfirmation(user.FirstName, confirmationUrl));
                TempData["StatusMessage"] = queued
                    ? "Profile saved. Confirm the new email address using the message sent to it."
                    : "Profile saved, but email delivery is not configured. The email address was not changed.";
            }
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View("Profile", _mapper.Map<EditProfileVm>(user));
        }

        await _signInManager.RefreshSignInAsync(user);
        await _auditLogger.RecordAsync("account.profile-updated", user.Id, user.Id, "user", user.Id, cancellationToken);
        TempData["StatusMessage"] ??= "Your profile was updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> ProfileImage()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is not null && _profileImages.TryResolve(user.Image, out var image) && image is not null)
        {
            Response.Headers.CacheControl = "private, no-store";
            return PhysicalFile(image.PhysicalPath, image.ContentType);
        }

        var defaultImage = Path.Combine(_environment.WebRootPath, "Images", "default.jpg");
        return System.IO.File.Exists(defaultImage)
            ? PhysicalFile(defaultImage, "image/jpeg")
            : NotFound();
    }

    [HttpGet]
    [Authorize]
    public IActionResult ConfirmEmailChange(string? code, string? newEmail)
    {
        if (string.IsNullOrWhiteSpace(code) || !MailAddress.TryCreate(newEmail, out _))
        {
            return BadRequest();
        }

        return View(new EmailChangeConfirmationVm { Code = code, NewEmail = newEmail! });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> ConfirmEmailChange(EmailChangeConfirmationVm model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Code));
        }
        catch (FormatException)
        {
            return BadRequest();
        }

        var result = await _userManager.ChangeEmailAsync(user, model.NewEmail.Trim(), decodedToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "This email confirmation link is invalid or expired.");
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        await _auditLogger.RecordAsync("account.email-changed", user.Id, user.Id, "user", user.Id);
        TempData["StatusMessage"] = "Your email address was updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task QueuePasswordResetAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed)
        {
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { code = encodedToken, email = user.Email }, Request.Scheme);
        if (resetUrl is not null)
        {
            _emailQueue.TryEnqueue(user.Email!, "Reset your CareAxis password",
                EmailTemplates.PasswordReset(user.FirstName, resetUrl));
        }
    }

    private IActionResult SafeLocalRedirect(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Home");

    private async Task<IActionResult> RedirectToRoleHomeAsync(ApplicationUser user)
    {
        if (await _userManager.IsInRoleAsync(user, Role.Admin.ToString()))
        {
            return RedirectToAction("Index", "Admin");
        }
        if (await _userManager.IsInRoleAsync(user, Role.Doctor.ToString()))
        {
            return RedirectToAction("Index", "Doctor");
        }
        if (await _userManager.IsInRoleAsync(user, Role.Patient.ToString()))
        {
            return RedirectToAction("Index", "Patient");
        }
        return RedirectToAction("Index", "Home");
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
