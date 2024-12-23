using Hangfire;
using Hospital.BLL.Helpers;
using Hospital.BLL.Mappers;
using Hospital.BLL.Services.Abstraction;
using Hospital.BLL.Services.Implementation;
using Hospital.DAL.DataBase;
using Hospital.DAL.Entities;
using Hospital.DAL.Repository.Abstraction;
using Hospital.DAL.Repository.Implementation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Configuration;

namespace HospitalManagement
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddHangfire(config =>
            {
                config.UseSqlServerStorage(builder.Configuration.GetConnectionString("defaultConnection"));
            });
            builder.Services.AddHangfireServer();
            
            // Add services to the container.
            builder.Services.AddControllersWithViews();
            
            builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("EmailOptions"));

            builder.Services.AddScoped<IEmailSender, EmailSender>();
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
            builder.Services.AddScoped<ImedicalRecordService,MedicalRecordService>();
            builder.Services.AddScoped<IScheduleRepo, ScheduleRepo>();
            builder.Services.AddScoped<IScheduleService, ScheduleService>();
            var connectionString = builder.Configuration.GetConnectionString("defaultConnection");

            builder.Services.AddDbContext<HospitalDbContext>(options =>
            options.UseSqlServer(connectionString));
            var googleOptions = builder.Configuration.GetSection("Auth:Google").Get<GoogleOptions>();
            builder.Services.AddAuthentication().AddGoogle(
                options =>
                {
                    options.ClientId = googleOptions.ClientID;
                    options.ClientSecret =  googleOptions.ClientSecret;
                   
                   
                });

            builder.Services.AddIdentity<ApplicationUser,IdentityRole>(options => {
                options.SignIn.RequireConfirmedAccount = true;
                options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<HospitalDbContext>().AddDefaultTokenProviders();
            builder.Services.AddAutoMapper(option => option.AddProfile<DomainProfile>());

            

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHangfireDashboard("/mydashboard");
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            
            app.UseRouting();
            app.UseAuthentication();
            
            app.UseAuthorization();
            
            
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            
            app.Run();
        }
    }
}
