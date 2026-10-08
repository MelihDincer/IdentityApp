using System.Threading.RateLimiting;
using IdentityApp.Models;
using IdentityApp.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<IdentityContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("SQLite_Connection")));

builder.Services
    .AddIdentity<AppUser, AppRole>()
    .AddEntityFrameworkStores<IdentityContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireDigit = false;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;

    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
});

builder.Services.AddScoped<IEmailSender>(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();

    return new SmtpEmailSender(
        configuration["EmailSender:Host"],
        configuration.GetValue<int>("EmailSender:Port"),
        configuration.GetValue<bool>("EmailSender:EnableSSL"),
        configuration["EmailSender:Username"],
        configuration["EmailSender:Password"]);
});

builder.Services.AddScoped<AccountEmailService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("account-email", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey:
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        var retrySeconds = context.Lease.TryGetMetadata(
            MetadataName.RetryAfter, out var retryAfter)
                ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                : 900;

        var httpContext = context.HttpContext;
        httpContext.Response.Headers["Retry-After"] = retrySeconds.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        httpContext.Response.Headers["Cache-Control"] = "no-store";

        // Sınır aşımında düz yazı yerine aynı tasarımdaki 429 sayfasını göster.
        var result = new ViewResult
        {
            ViewName = "~/Views/Account/TooManyRequests.cshtml",
            StatusCode = StatusCodes.Status429TooManyRequests,
            ViewData = new ViewDataDictionary<int>(
                new EmptyModelMetadataProvider(),
                new ModelStateDictionary())
            {
                Model = retrySeconds
            }
        };

        await result.ExecuteResultAsync(new ActionContext(
            httpContext,
            httpContext.GetRouteData(),
            new ActionDescriptor()));
    };
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await IdentitySeedData.IdentityTestUserAsync(app);

app.Run();