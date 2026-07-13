using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrintingBooksPortal.Components;
using PrintingBooksPortal.Data;
using PrintingBooksPortal.Models;
using PrintingBooksPortal.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var isProduction = builder.Environment.IsProduction();

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isProduction)
        options.UseSqlServer(connectionString);
    else
        options.UseSqlite(connectionString);
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ShopOnly", policy => policy.RequireRole("Shop"));
});

builder.Services.AddScoped<FileStorageService>();
builder.Services.AddScoped<PrintLoggingService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddControllers();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["AppUrl"] ?? "http://localhost:5035") });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapControllers();

// Login endpoint (minimal API - avoids Blazor circuit / response header conflict)
app.MapPost("/login-check", async (HttpContext context, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<Program> logger) =>
{
    try
    {
        var form = await context.Request.ReadFormAsync();
        var email = form["email"].FirstOrDefault() ?? "";
        var password = form["password"].FirstOrDefault() ?? "";
        var rememberMe = form["rememberMe"].FirstOrDefault() == "true";

        logger.LogInformation("Login attempt for {Email}", email);

        var result = await signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            logger.LogInformation("Login successful for {Email}", email);
            var user = await userManager.FindByEmailAsync(email);
            if (user != null)
            {
                if (await userManager.IsInRoleAsync(user, "Admin"))
                    return Results.Redirect("/admin/dashboard");
                if (await userManager.IsInRoleAsync(user, "Shop"))
                    return Results.Redirect("/shop/mybooks");
            }
            return Results.Redirect("/");
        }

        logger.LogWarning("Login failed for {Email}: {Reason}", email, result);
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("Invalid email or password"));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login error");
        return Results.Redirect("/login?error=" + Uri.EscapeDataString("An error occurred: " + ex.Message));
    }
});

// Apply pending migrations and seed data
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (isProduction)
    {
        // Attempt to apply migrations (may fail if no DDL permissions)
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not apply migrations automatically. Attempting EnsureCreated...");
            await db.Database.EnsureCreatedAsync();
        }
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await DbSeeder.SeedAsync(db, userManager, roleManager);
    logger.LogInformation("Database initialization completed.");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Database initialization failed. The app will still start.");
}

app.Run();
