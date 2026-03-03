using AssetTracker.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using AssetTracker.Services;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var configuredConnectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

var connectionBuilder = new MySqlConnectionStringBuilder(configuredConnectionString);
connectionBuilder.Database = "asset_tracker";

var envDbName = Environment.GetEnvironmentVariable("DB_NAME");
if (!string.IsNullOrWhiteSpace(envDbName) && !string.Equals(envDbName, "asset_tracker", StringComparison.OrdinalIgnoreCase))
{
    builder.Logging.AddConsole();
}

var effectiveConnectionString = connectionBuilder.ConnectionString;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        effectiveConnectionString,
        new MySqlServerVersion(new Version(8, 0, 34))
    ));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(IdentitySeeder.RequireAdminRolePolicy, policy =>
        policy.RequireRole(IdentitySeeder.AdminRole));
});

builder.Services.AddMemoryCache();
builder.Services.AddScoped<IImportService, ImportService>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var scopedServices = scope.ServiceProvider;
    var logger = scopedServices.GetRequiredService<ILoggerFactory>().CreateLogger("StartupDiagnostics");
    var context = scopedServices.GetRequiredService<ApplicationDbContext>();

    var runtimeConnectionString = context.Database.GetDbConnection().ConnectionString;
    var runtimeConnectionBuilder = new MySqlConnectionStringBuilder(runtimeConnectionString);
    var runtimeDatabaseName = context.Database.GetDbConnection().Database;

    if (!string.IsNullOrEmpty(runtimeConnectionBuilder.Password))
    {
        runtimeConnectionBuilder.Password = "***";
    }

    logger.LogInformation("Runtime Database: {DatabaseName}", runtimeDatabaseName);
    logger.LogInformation("Runtime ConnectionString: {ConnectionString}", runtimeConnectionBuilder.ConnectionString);

    if (!string.IsNullOrWhiteSpace(envDbName))
    {
        logger.LogInformation("Environment DB_NAME: {DbName}", envDbName);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.Equals("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/Identity/Account/Login");
        return;
    }

    await next();
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", async context =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        context.Response.Redirect("/Dashboard");
    }
    else
    {
        context.Response.Redirect("/Identity/Account/Login");
    }

    await Task.CompletedTask;
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapRazorPages();
await IdentitySeeder.SeedAsync(app.Services, builder.Configuration);

app.Run();
