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

    public IActionResult Create()
    {
        return View(new CreateViewModel
        {
            Roles = new[] { "User" }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateViewModel model)
    {
        if (ModelState.IsValid)
        {
            var roles = model.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var allowedRoles = new[] { "User", "Admin", "Moderator" };
            foreach (var role in roles)
            {
                if (!allowedRoles.Contains(role) || !await _roleManager.RoleExistsAsync(role))
                {
                    ModelState.AddModelError(nameof(model.Roles), "Seçilen rollerden biri geçersiz veya sistemde tanımlı değil.");
                    return View(model);
                }
            }
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                EmailConfirmed = model.EmailConfirmed,
                PhoneNumberConfirmed = model.PhoneNumberConfirmed,
                FullName = model.FullName
            };
            IdentityResult createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await transaction.RollbackAsync();
                return View(model);
            }
            IdentityResult roleResult = await _userManager.AddToRolesAsync(user, roles);
            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                {
                    ModelState.AddModelError(
                    nameof(model.Roles),
                    error.Description);
                }
                // Rol ataması başarısızsa kullanıcı kaydını da geri al.
                await transaction.RollbackAsync();
                return View(model);
            }
            await transaction.CommitAsync();

            TempData["SuccessMessage"] = "Kullanıcı oluşturuldu ve rolleri atandı.";
            return RedirectToAction(nameof(Index));
        }
        return View(model);
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["WarningMessage"] = "Düzenlemek istediğiniz kullanıcıyı listeden seçin.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.FindByIdAsync(id);

        if (user == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;

            return View("UserNotFound");
        }

        return View(new EditViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            PhoneNumberConfirmed = user.PhoneNumberConfirmed
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

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(model.Id);

        if (user == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("UserNotFound");
        }

        user.FullName = model.FullName;
        user.Email = model.Email;
        user.PhoneNumber = model.PhoneNumber;
        user.EmailConfirmed = model.EmailConfirmed;
        user.PhoneNumberConfirmed = model.PhoneNumberConfirmed;

        IdentityResult result = await _userManager.UpdateAsync(user);

        if (result.Succeeded)
        {
            if(!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                await _userManager.RemovePasswordAsync(user);
                await _userManager.AddPasswordAsync(user, model.NewPassword);
                // var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                // var passwordResult = await _userManager.ResetPasswordAsync(user, token, model.Password);

                // if (!passwordResult.Succeeded)
                // {
                //     foreach (var error in passwordResult.Errors)
                //     {
                //         ModelState.AddModelError(string.Empty, error.Description);
                //     }
                //     return View(model);
                // }
            }
            TempData["SuccessMessage"] = "Kullanıcı bilgileri başarıyla güncellendi.";

            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return View(model);
    }

    [HttpPost]
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
        if (result.Succeeded)
        {
            TempData["SuccessMessage"] = "Kullanıcı başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return RedirectToAction(nameof(Index));
    }
}