using Microsoft.AspNetCore.Identity;

namespace AssetTracker.Data;

public static class IdentitySeeder
{
    public const string AdminRole = "Admin";
    public const string StaffRole = "Staff";
    public const string RequireAdminRolePolicy = "RequireAdminRole";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        await EnsureRoleAsync(roleManager, AdminRole);
        await EnsureRoleAsync(roleManager, StaffRole);

        var adminEmail = GetSuperAdminEmail(configuration);
        var adminPassword = GetSuperAdminPassword(configuration);

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser is null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to create super admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, AdminRole))
        {
            var addRoleResult = await userManager.AddToRoleAsync(adminUser, AdminRole);
            if (!addRoleResult.Succeeded)
            {
                var errors = string.Join("; ", addRoleResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to assign Admin role: {errors}");
            }
        }
    }

    private static async Task EnsureRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
    {
        if (await roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var result = await roleManager.CreateAsync(new IdentityRole(roleName));
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create role '{roleName}': {errors}");
        }
    }

    private static string GetSuperAdminEmail(IConfiguration configuration)
    {
        var email = Environment.GetEnvironmentVariable("SUPERADMIN_EMAIL")
                    ?? configuration["SuperAdmin:Email"];

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Super admin email is missing. Set SUPERADMIN_EMAIL or SuperAdmin:Email.");
        }

        return email;
    }

    private static string GetSuperAdminPassword(IConfiguration configuration)
    {
        var password = Environment.GetEnvironmentVariable("SUPERADMIN_PASSWORD")
                       ?? configuration["SuperAdmin:Password"];

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("Super admin password is missing. Set SUPERADMIN_PASSWORD or SuperAdmin:Password.");
        }

        return password;
    }
}
