using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityApp.Models;

public static class IdentitySeedData
{
    private const string SeedUserName = "Administrator";
    private const string SeedPassword = "Admin_123";

    public static async Task IdentityTestUserAsync(WebApplication app)
    {
        // Test hesabını yanlışlıkla canlı ortamda oluşturmamak için.
        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();

        var context = scope.ServiceProvider
            .GetRequiredService<IdentityContext>();

        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<AppUser>>();

        var roleManager = scope.ServiceProvider
            .GetRequiredService<RoleManager<AppRole>>();

        // Veritabanı yoksa oluşturur ve mevcut migration'ları uygular.
        // Varsa yalnızca bekleyen migration'ları uygular.
        await context.Database.MigrateAsync();

        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Olası yeniden denemede önceki nesneleri temizle.
            context.ChangeTracker.Clear();

            await using var transaction =
                await context.Database.BeginTransactionAsync();

            // 1. Başlangıç rollerini tanımla.
            var defaultRoles = new[]
            {
                new AppRole
                {
                    Name = "Admin",
                    DisplayName = "Sistem Yöneticisi",
                    Description = "Kullanıcı ve rol yönetiminden sorumludur.",
                    IsActive = true,
                    IsSystemRole = true
                },
                new AppRole
                {
                    Name = "User",
                    DisplayName = "Kullanıcı",
                    Description = "Uygulamanın standart kullanıcı rolüdür.",
                    IsActive = true,
                    IsSystemRole = false
                },
                new AppRole
                {
                    Name = "Moderator",
                    DisplayName = "Moderatör",
                    Description = "Kendisine tanımlanan denetim işlemlerini yürütür.",
                    IsActive = true,
                    IsSystemRole = false
                }
            };

            // 2. Yalnızca eksik rolleri oluştur.
            foreach (var role in defaultRoles)
            {
                if (await roleManager.RoleExistsAsync(role.Name!))
                {
                    continue;
                }

                IdentityResult roleResult =
                    await roleManager.CreateAsync(role);

                EnsureSucceeded(
                    roleResult,
                    $"'{role.Name}' rolü oluşturulamadı");
            }

            // 3. Test kullanıcısını bul; yoksa oluştur.
            var user = await userManager.FindByNameAsync(SeedUserName);

            if (user == null)
            {
                user = new AppUser
                {
                    FullName = "Administrator",
                    UserName = SeedUserName,
                    Email = "info@admin.com",
                    PhoneNumber = "1234567890",

                    // Yerel test hesabı için.
                    EmailConfirmed = true,
                    PhoneNumberConfirmed = false
                };

                IdentityResult userResult = await userManager.CreateAsync(user, SeedPassword);
                EnsureSucceeded(userResult, "Başlangıç kullanıcısı oluşturulamadı");
            }

            // 4. Kullanıcının mevcut rollerini öğren.
            var currentRoles = await userManager.GetRolesAsync(user);

            // 5. Üç başlangıç rolünden yalnızca eksikleri ata.
            var missingRoles = defaultRoles
                .Select(role => role.Name!)
                .Except(currentRoles, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingRoles.Length > 0)
            {
                IdentityResult assignmentResult =
                    await userManager.AddToRolesAsync(user, missingRoles);

                EnsureSucceeded(
                    assignmentResult,
                    "Başlangıç kullanıcısına roller atanamadı");
            }

            await transaction.CommitAsync();
        });
    }

    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join(
            " | ",
            result.Errors.Select(error => error.Description));

        throw new InvalidOperationException($"{operation}: {errors}");
    }
}