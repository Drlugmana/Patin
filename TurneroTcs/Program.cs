using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TurneroTcs.Data;
using TurneroTcs.Models;
using TurneroTcs.Seeders;
using TurneroTcs.Authorization;
using TurneroTcs.Repositories;
using TurneroTcs.Repositories.Interfaces;
using TurneroTcs.Services;
using TurneroTcs.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddAuthorization(options => 
{
    options.FallbackPolicy = options.DefaultPolicy;
    options.AddPolicy("SuperAdminOnly", policy => policy.RequireRole("SuperAdmin"));
    options.AddPolicy("AdminAbove", policy => policy.RequireRole("SuperAdmin", "Admin"));
    options.AddPolicy("LiderAbove", policy => policy.RequireRole("SuperAdmin", "Admin", "Lider"));
    options.AddPolicy("UserAbove", policy => policy.RequireRole("SuperAdmin", "Admin", "Lider", "Usuario"));
});

builder.Services.AddDefaultIdentity<IdentityUser>(options => 
    
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 15;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "__Host-turnerotcs-auth";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

builder.Services.AddAntiforgery(options =>
{
    options.FormFieldName = "__RequestVerificationToken";
    options.HeaderName = "RequestVerificationToken";
    options.SuppressXFrameOptionsHeader = false;
    options.Cookie.Name = "__Host-turnerotcs-antiforgery";
    options.Cookie.Path = "/";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("non-post");
        }

        var path = httpContext.Request.Path.Value ?? string.Empty;
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (string.Equals(path, "/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"login:{clientIp}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(5),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        }

        if (string.Equals(path, "/Persona/ResetPassword", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                $"reset-password:{clientIp}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(10),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        }

        return RateLimitPartition.GetNoLimiter("other-post");
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            : 60;

        if (string.Equals(path, "/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            var message = $"Demasiados intentos de inicio de sesión. Espera {retryAfterSeconds} segundos antes de volver a intentarlo.";
            var encodedMessage = Uri.EscapeDataString(message);
            context.HttpContext.Response.Redirect($"/Identity/Account/Login?rateLimitMessage={encodedMessage}");
            return;
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var messageText = string.Equals(path, "/Persona/ResetPassword", StringComparison.OrdinalIgnoreCase)
            ? $"Demasiados intentos para restablecer la contraseña. Espera {retryAfterSeconds} segundos antes de volver a intentarlo."
            : $"Demasiadas solicitudes. Espera {retryAfterSeconds} segundos antes de volver a intentarlo.";

        await context.HttpContext.Response.WriteAsJsonAsync(new { message = messageText }, cancellationToken);
    };
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<GenerationPreviewProgressTracker>();

builder.Services.AddScoped<IRoleSeeder, RoleSeeder>();
builder.Services.AddScoped<IPermisoAccesoSeeder, PermisoAccesoSeeder>();
builder.Services.AddScoped<IRolService, RolService>();
builder.Services.AddScoped<IPersonaService, PersonaService>();
builder.Services.AddScoped<IEquipoService, EquipoService>();
builder.Services.AddScoped<ITipoTurnoService, TipoTurnoService>();
builder.Services.AddScoped<IRegistroTurnoService, RegistroTurnoService>();
builder.Services.AddScoped<IGrupoService, GrupoService>();
builder.Services.AddScoped<ISolicitudService, SolicitudService>();
builder.Services.AddScoped<IPlanificacionService, PlanificacionService>();
builder.Services.AddScoped<IFeriadoService, FeriadoService>();
builder.Services.AddScoped<IExcepcionTurnoPersonaService, ExcepcionTurnoPersonaService>();
builder.Services.AddScoped<IHorasMensualesRepository, HorasMensualesRepository>();
builder.Services.AddScoped<IHorasMensualesService, HorasMensualesService>();
builder.Services.AddScoped<IPermisoAccesoResolver, PermisoAccesoResolver>();
builder.Services.AddScoped<IPermisoAccesoService, PermisoAccesoService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoAccesoPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermisoAccesoHandler>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var roleSeeder = scope.ServiceProvider.GetRequiredService<IRoleSeeder>();
    await roleSeeder.SeedAsync();

    try
    {
        var permisoSeeder = scope.ServiceProvider.GetRequiredService<IPermisoAccesoSeeder>();
        await permisoSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "PermisoAcceso seeding skipped. Make sure latest migrations are applied.");
    }
}

using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    if (!userManager.Users.Any())
    {
        var superAdminUserName = builder.Configuration["SuperAdmin:UserName"];
        var superAdminPassword = builder.Configuration["SuperAdmin:Password"];
        var superAdminRole = builder.Configuration["SuperAdmin:Role"] ?? "SuperAdmin";

        if (string.IsNullOrWhiteSpace(superAdminUserName) || string.IsNullOrWhiteSpace(superAdminPassword))
        {
            app.Logger.LogWarning("SuperAdmin seeding skipped because UserName/Password is missing.");
        }
        else
        {
            if (!await roleManager.RoleExistsAsync(superAdminRole))
            {
                await roleManager.CreateAsync(new IdentityRole(superAdminRole));
            }

            var user = new IdentityUser
            {
                UserName = superAdminUserName
            };

            var createResult = await userManager.CreateAsync(user, superAdminPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(user, superAdminRole);
            }
            else
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                app.Logger.LogWarning("SuperAdmin seeding failed: {Errors}", errors);
            }
        }
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
app.MapRazorPages();

app.Run();
