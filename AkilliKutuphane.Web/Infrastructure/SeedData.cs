using AkilliKutuphane.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AkilliKutuphane.Web.Infrastructure;

public static class SeedData
{
    private const string AdminRole = "Admin";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        using var scope = services.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var roleManager = scopedServices.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scopedServices.GetRequiredService<AkilliKutuphane.Data.Persistence.ApplicationDbContext>();

        await dbContext.Database.MigrateAsync();

        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(AdminRole));
            if (!roleResult.Succeeded)
            {
                logger.LogWarning("Admin role could not be created: {Errors}", string.Join(", ", roleResult.Errors.Select(x => x.Description)));
                return;
            }
        }

        var email = configuration["SeedAdmin:Email"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN_EMAIL");
        var password = configuration["SeedAdmin:Password"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Seed admin credentials were not provided. Skipping admin user creation.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(admin, password);
            if (!createResult.Succeeded)
            {
                logger.LogWarning("Admin user could not be created: {Errors}", string.Join(", ", createResult.Errors.Select(x => x.Description)));
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(admin, AdminRole))
        {
            var roleAssignResult = await userManager.AddToRoleAsync(admin, AdminRole);
            if (!roleAssignResult.Succeeded)
            {
                logger.LogWarning("Admin role assignment failed: {Errors}", string.Join(", ", roleAssignResult.Errors.Select(x => x.Description)));
            }
        }
    }
}
