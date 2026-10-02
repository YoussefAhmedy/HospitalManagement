using System.Net;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Dashboard;
using Hospital.BLL.Helpers;
using Hospital.BLL.Mappers;
using Hospital.BLL.Services.Abstraction;
using Hospital.BLL.Services.Implementation;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Repository.Abstraction;
using Hospital.DAL.Repository.Implementation;
using HospitalManagement.Infrastructure;
using HospitalManagement.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace HospitalManagement;

public class Program
{
    public static async Task Main(string[] args)
    {
        var bootstrapAdmin = args.Contains("--bootstrap-admin", StringComparer.Ordinal);
        var builderArgs = bootstrapAdmin
            ? args.Where(argument => !string.Equals(argument, "--bootstrap-admin", StringComparison.Ordinal)).ToArray()
            : args;
        var builder = WebApplication.CreateBuilder(builderArgs);
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole();

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required. Configure it with a secret manager or environment variable.");
        }

        var allowedHosts = builder.Configuration["AllowedHosts"];
        if (builder.Environment.IsProduction() &&
            (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts.Trim() == "*"))
        {
            throw new InvalidOperationException(
                "Production requires an explicit AllowedHosts value; wildcard host filtering is not permitted.");
        }

        var keyRingPath = builder.Configuration["DataProtection:KeysPath"];
        if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(keyRingPath))
        {
            throw new InvalidOperationException(
                "Production requires DataProtection:KeysPath to point to persistent, access-controlled storage.");
        }

        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName("CareAxis");
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            var fullKeyRingPath = Path.GetFullPath(keyRingPath);
            Directory.CreateDirectory(fullKeyRingPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(fullKeyRingPath));
        }

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
            {
                if (string.IsNullOrWhiteSpace(proxy))
                {
                    continue;
                }

                if (!IPAddress.TryParse(proxy, out var address))
                {
                    throw new InvalidOperationException("ForwardedHeaders:KnownProxies contains an invalid IP address.");
                }
                options.KnownProxies.Add(address);
            }
        });

        builder.Services.AddDbContext<HospitalDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer =>
                sqlServer.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null)));

        builder.Services.AddHangfire(configuration =>
            configuration.UseSqlServerStorage(connectionString));
        builder.Services.AddHangfireServer();

        builder.Services
            .AddOptions<EmailOptions>()
            .Bind(builder.Configuration.GetSection("EmailOptions"))
            .Validate(options => !options.Enabled ||
                (!string.IsNullOrWhiteSpace(options.From) &&
                 !string.IsNullOrWhiteSpace(options.Password) &&
                 !string.IsNullOrWhiteSpace(options.SmtpServer) &&
                 options.Port is > 0 and <= 65535),
                "Enabled email requires a sender, SMTP host, valid port, and provider credential.")
            .ValidateOnStart();

        var googleOptions = builder.Configuration.GetSection("Auth:Google").Get<GoogleOptions>() ?? new GoogleOptions();
        var authentication = builder.Services.AddAuthentication();
        if (!string.IsNullOrWhiteSpace(googleOptions.ClientID) &&
            !string.IsNullOrWhiteSpace(googleOptions.ClientSecret))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = googleOptions.ClientID;
                options.ClientSecret = googleOptions.ClientSecret;
                options.SaveTokens = false;
            });
        }

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedEmail = true;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddEntityFrameworkStores<HospitalDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "CareAxis.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/LogIn";
            options.AccessDeniedPath = "/Account/AccessDenied";
        });

        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "RequestVerificationToken";
            options.Cookie.Name = "CareAxis.AntiForgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        builder.Services.AddControllersWithViews(options =>
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("authentication", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.AddPolicy("public-search", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Please wait a short time before trying again."
                    }, cancellationToken);
            };
        });

        builder.Services.AddAutoMapper(configuration => configuration.AddProfile<DomainProfile>());
        builder.Services.AddScoped<IEmailSender, EmailSender>();
        builder.Services.AddScoped<IProfileImageStorage, LocalProfileImageStorage>();
        builder.Services.AddScoped<IEmailQueue, HangfireEmailQueue>();
        builder.Services.AddTransient<IEmailDeliveryJob, EmailDeliveryJob>();
        builder.Services.AddScoped<IAuditLogger, AuditLogger>();
        builder.Services.AddScoped<IPatientRepository, PatientRepository>();
        builder.Services.AddScoped<IShiftRepo, ShiftRepo>();
        builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        builder.Services.AddScoped<IAppointmentService, AppointmentService>();
        builder.Services.AddScoped<IMedicalRecordRepository, MedicalRecordRepository>();
        builder.Services.AddScoped<IDoctorRepository, DoctorRepository>();
        builder.Services.AddScoped<IPatientService, PatientService>();
        builder.Services.AddScoped<ISepcializationRepo, SepcializationRepo>();
        builder.Services.AddScoped<ISepcializationService, SepcializationService>();
        builder.Services.AddScoped<IDoctorService, DoctorService>();
        builder.Services.AddScoped<IShiftService, ShiftService>();
        builder.Services.AddScoped<ImedicalRecordService, MedicalRecordService>();
        builder.Services.AddScoped<IScheduleRepo, ScheduleRepo>();
        builder.Services.AddScoped<IScheduleService, ScheduleService>();

        await using var app = builder.Build();
        app.UseForwardedHeaders();
        await SeedRolesAsync(app.Services);
        if (bootstrapAdmin)
        {
            await BootstrapAdminAsync(app.Services, builder.Configuration);
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                headers["Content-Security-Policy"] =
                    "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
                    "form-action 'self'; img-src 'self' data: blob:; font-src 'self' data:; " +
                    "style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; connect-src 'self';";

                if (context.Request.Path.StartsWithSegments("/Patient") ||
                    context.Request.Path.StartsWithSegments("/Doctor") ||
                    context.Request.Path.StartsWithSegments("/Admin") ||
                    context.Request.Path.StartsWithSegments("/Account/Profile"))
                {
                    headers.CacheControl = "no-store, private";
                    headers.Pragma = "no-cache";
                }

                return Task.CompletedTask;
            });

            await next();
        });

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseHangfireDashboard("/ops/jobs", new DashboardOptions
        {
            Authorization = [new AdminDashboardAuthorizationFilter()],
            IsReadOnlyFunc = _ => true
        });

        RecurringJob.AddOrUpdate<IAppointmentService>(
            "daily-close-past-appointments",
            service => service.UpdateAppointmentStatus(Hospital.DAL.Entities.OwnedTypes.AppointStatus.NotApproved),
            Cron.Daily);

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthResponseAsync
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponseAsync
        });

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");

        await app.RunAsync();
    }

    private static async Task BootstrapAdminAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["BootstrapAdmin:Email"]?.Trim();
        var firstName = configuration["BootstrapAdmin:FirstName"]?.Trim();
        var lastName = configuration["BootstrapAdmin:LastName"]?.Trim();
        var password = configuration["BootstrapAdmin:Password"];
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _) ||
            string.IsNullOrWhiteSpace(firstName) || firstName.Length > 50 ||
            string.IsNullOrWhiteSpace(lastName) || lastName.Length > 50 ||
            string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set BootstrapAdmin:Email, FirstName, LastName, and Password in a secure environment before bootstrapping.");
        }

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new InvalidOperationException(
                "An account already exists for that email. The bootstrap command does not alter existing accounts.");
        }

        var administrator = new ApplicationUser
        {
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        var createResult = await userManager.CreateAsync(administrator, password);
        if (!createResult.Succeeded)
        {
            var errorCodes = string.Join(", ", createResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Administrator account creation failed. Identity errors: {errorCodes}");
        }

        var roleResult = await userManager.AddToRoleAsync(administrator, Role.Admin.ToString());
        if (!roleResult.Succeeded)
        {
            await userManager.DeleteAsync(administrator);
            var errorCodes = string.Join(", ", roleResult.Errors.Select(error => error.Code));
            throw new InvalidOperationException($"Administrator role assignment failed. Identity errors: {errorCodes}");
        }

        Console.WriteLine("Bootstrap administrator created. Remove the bootstrap credentials from the process environment.");
    }

    private static async Task SeedRolesAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Enum.GetNames<Role>())
        {
            if (await roleManager.RoleExistsAsync(role))
            {
                continue;
            }

            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Required role '{role}' could not be initialized.");
            }
        }
    }

    private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
}
