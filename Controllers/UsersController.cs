using IdentityApp.Models;
using IdentityApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityApp.Controllers;

public class UsersController: Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IdentityContext _dbContext;

    public UsersController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, IdentityContext dbContext)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userManager.Users.ToListAsync();
        return View(users);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var availableRoles = await GetActiveRolesAsync();
        return View(new CreateViewModel
        {
            AvailableRoles = availableRoles,   
            Roles = availableRoles.Any(role => role.Name == "User")
                ? new[] { "User" }
                : Array.Empty<string>()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = await GetActiveRolesAsync();
            return View(model);
        }

        var selectedRoles = model.Roles
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            // Aktif rolleri transaction içinde kontrol et.
            model.AvailableRoles = await GetActiveRolesAsync();

            var activeRoleNames = model.AvailableRoles
                .Select(role => role.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (selectedRoles.Any(role => !activeRoleNames.Contains(role)))
            {
                ModelState.AddModelError(
                    nameof(model.Roles),
                    "Seçtiğiniz rollerden biri artık aktif değil veya silinmiş. " +
                    "Rol seçiminizi kontrol edin.");

                await transaction.RollbackAsync();
                return View(model);
            }

            var user = new AppUser
            {
                UserName = model.UserName,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = model.EmailConfirmed,
                PhoneNumberConfirmed = model.PhoneNumberConfirmed,
                FullName = model.FullName
            };

            IdentityResult createResult =
                await _userManager.CreateAsync(user, model.Password);

            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                await transaction.RollbackAsync();
                return View(model);
            }

            IdentityResult roleResult =
                await _userManager.AddToRolesAsync(user, selectedRoles);

            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                        nameof(model.Roles),
                        error.Description);
                }

                await transaction.RollbackAsync();
                return View(model);
            }

            await transaction.CommitAsync();

            TempData["SuccessMessage"] =
                "Kullanıcı oluşturuldu ve rolleri atandı.";

            return RedirectToAction(nameof(Index));
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["WarningMessage"] = "Düzenlemek istediğiniz kullanıcıyı seçin.";
    
            return RedirectToAction(nameof(Index));
        }
    
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("UserNotFound");
        }
    
        var availableRoles = await GetActiveRolesAsync();
        var userRoles = await _userManager.GetRolesAsync(user);
    
        var activeRoleNames = availableRoles
            .Select(role => role.Name)
            .ToHashSet(StringComparer.Ordinal);
    
        return View(new EditViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed,
            AvailableRoles = availableRoles,
            Roles = userRoles
                .Where(roleName => activeRoleNames.Contains(roleName))
                .ToArray()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            return BadRequest();
        }

        // Parola değiştirme seçildiyse alanları sunucuda doğrula.
        if (model.ChangePassword)
        {
            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError(
                    nameof(model.NewPassword),
                    "Yeni parola gereklidir.");
            }

            if (string.IsNullOrWhiteSpace(model.ConfirmNewPassword))
            {
                ModelState.AddModelError(
                    nameof(model.ConfirmNewPassword),
                    "Yeni parola tekrarı gereklidir.");
            }
            else if (!string.Equals(
                         model.NewPassword,
                         model.ConfirmNewPassword,
                         StringComparison.Ordinal))
            {
                ModelState.AddModelError(
                    nameof(model.ConfirmNewPassword),
                    "Parolalar eşleşmiyor.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.AvailableRoles = await GetActiveRolesAsync();
            return View(model);
        }

        var selectedRoles = model.Roles
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(async () =>
        {
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            var user = await _userManager.FindByIdAsync(model.Id);

            if (user == null)
            {
                await transaction.RollbackAsync();

                Response.StatusCode = StatusCodes.Status404NotFound;
                return View("UserNotFound");
            }

            // Aktif rolleri transaction içinde yeniden oku.
            model.AvailableRoles = await GetActiveRolesAsync();

            var activeRoleNames = model.AvailableRoles
                .Select(role => role.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (selectedRoles.Any(role => !activeRoleNames.Contains(role)))
            {
                ModelState.AddModelError(
                    nameof(model.Roles),
                    "Seçtiğiniz rollerden biri artık aktif değil veya silinmiş. " +
                    "Rol seçiminizi kontrol edin.");

                await transaction.RollbackAsync();
                return View(model);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            var rolesToAdd = selectedRoles
                .Except(currentRoles, StringComparer.Ordinal)
                .ToArray();

            // Formda görünmeyen pasif rol bağlantılarını koru.
            var rolesToRemove = currentRoles
                .Where(role => activeRoleNames.Contains(role))
                .Except(selectedRoles, StringComparer.Ordinal)
                .ToArray();

            user.UserName = model.UserName;
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.PhoneNumber = model.PhoneNumber;
            user.EmailConfirmed = model.EmailConfirmed;
            user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;

            IdentityResult updateResult = await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(
                        string.Empty,
                        error.Description);
                }

                await transaction.RollbackAsync();
                return View(model);
            }

            // Eski parolayı kaldırmadan yeni parolayı uygula.
            if (model.ChangePassword)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                IdentityResult passwordResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword!);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.NewPassword),
                            error.Description);
                    }
                    await transaction.RollbackAsync();
                    return View(model);
                }
            }

            if (rolesToAdd.Length > 0)
            {
                IdentityResult addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    foreach (var error in addResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.Roles),
                            error.Description);
                    }

                    await transaction.RollbackAsync();
                    return View(model);
                }
            }

            if (rolesToRemove.Length > 0)
            {
                IdentityResult removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    foreach (var error in removeResult.Errors)
                    {
                        ModelState.AddModelError(
                            nameof(model.Roles),
                            error.Description);
                    }

                    await transaction.RollbackAsync();
                    return View(model);
                }
            }

            await transaction.CommitAsync();

            TempData["SuccessMessage"] = model.ChangePassword
                ? "Kullanıcı bilgileri, parola ve rol seçimleri güncellendi."
                : "Kullanıcı bilgileri ve rol seçimleri güncellendi.";

            return RedirectToAction(nameof(Index));
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("UserNotFound");
        }

        IdentityResult result = await _userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            TempData["WarningMessage"] = string.Join(
                " ",
                result.Errors.Select(error => error.Description));

            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = "Kullanıcı başarıyla silindi.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<List<RoleOptionViewModel>> GetActiveRolesAsync()
    {
    return await _roleManager.Roles
        .AsNoTracking()
        .Where(role => role.IsActive)
        .OrderBy(role => role.DisplayName)
        .Select(role => new RoleOptionViewModel
        {
            Name = role.Name ?? string.Empty,

            DisplayName = role.DisplayName == null ||
                          role.DisplayName == ""
                ? role.Name ?? string.Empty
                : role.DisplayName
        })
        .ToListAsync();
    }
}