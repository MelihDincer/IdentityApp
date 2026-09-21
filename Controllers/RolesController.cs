using IdentityApp.Models;
using IdentityApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace IdentityApp.Controllers;

public class RolesController: Controller
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IdentityContext _dbContext;

    public RolesController(RoleManager<AppRole> roleManager, IdentityContext dbContext)
    {
        _roleManager = roleManager;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderByDescending(role => role.IsSystemRole)
            .ThenBy(role => role.Name)
            .Select(role => new RoleListViewModel
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,

                DisplayName = role.DisplayName == null ||
                              role.DisplayName == ""
                    ? role.Name ?? string.Empty
                    : role.DisplayName,

                Description = role.Description,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,

                UserCount = _dbContext.UserRoles
                    .Count(userRole => userRole.RoleId == role.Id)
            })
            .ToListAsync();

        return View(roles);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateRoleViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var role = new AppRole
        {
            Name = model.Name.Trim(),
            DisplayName = model.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),

            IsActive = true,
            IsSystemRole = false
        };

        IdentityResult result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    error.Description);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Rol başarıyla oluşturuldu.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return RoleWarning("Düzenlemek istediğiniz rolü seçin.");
        }

        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            return RoleWarning("Rol bulunamadı. Silinmiş olabilir.");
        }

        if (string.IsNullOrWhiteSpace(role.ConcurrencyStamp))
        {
            role.ConcurrencyStamp = Guid.NewGuid().ToString();

            IdentityResult result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                return RoleWarning(string.Join(
                    " ",
                    result.Errors.Select(error => error.Description)));
            }
        }

        return View(new EditRoleViewModel
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(role.DisplayName)
                ? role.Name ?? string.Empty
                : role.DisplayName,
            Description = role.Description,
            ConcurrencyStamp = role.ConcurrencyStamp ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditRoleViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
        {
            return BadRequest();
        }

        var role = await _roleManager.FindByIdAsync(model.Id);

        if (role == null)
        {
            return RoleWarning("Rol bulunamadı. Silinmiş olabilir.");
        }

        // Teknik adı formdan değil, veritabanından alıyoruz.
        model.Name = role.Name ?? string.Empty;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.ConcurrencyStamp != role.ConcurrencyStamp)
        {
            ModelState.AddModelError(
                string.Empty,
                "Bu rol siz düzenlerken değiştirilmiş. " +
                "Sayfayı yeniden yükleyip güncel bilgiler üzerinden devam edin.");

            return View(model);
        }

        // Yalnızca düzenlenmesine izin verdiğimiz alanları değiştir.
        role.DisplayName = model.DisplayName.Trim();
        role.Description = string.IsNullOrWhiteSpace(model.Description)
            ? null
            : model.Description.Trim();

        IdentityResult result = await _roleManager.UpdateAsync(role);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Rol bilgileri güncellendi.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(string? id, bool? isActive)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            !isActive.HasValue ||
            !ModelState.IsValid)
        {
            return BadRequest();
        }

        bool targetIsActive = isActive.Value;

        IdentityResult result = await RunRoleTransactionAsync(async () =>
        {
            var role = await _roleManager.FindByIdAsync(id);

            if (role == null)
            {
                return RoleFailure("Rol bulunamadı. Silinmiş olabilir.");
            }

            // Anahtarı tersine çevirmek yerine istenen durumu uygularız.
            // Aynı istek tekrarlanırsa rol yanlışlıkla tekrar değişmez.
            if (role.IsActive == targetIsActive)
            {
                return IdentityResult.Success;
            }

            if (!targetIsActive && IsProtectedRole(role))
            {
                return RoleFailure("Sistem rolleri pasife alınamaz.");
            }

            if (!targetIsActive)
            {
                // Bu role sahip olup başka aktif rolü bulunmayan
                // en az bir kullanıcı var mı?
                bool leavesUserWithoutActiveRole =
                    await _dbContext.UserRoles
                        .Where(userRole => userRole.RoleId == role.Id)
                        .AnyAsync(userRole =>
                            !_dbContext.UserRoles.Any(otherUserRole =>
                                otherUserRole.UserId == userRole.UserId &&
                                otherUserRole.RoleId != role.Id &&
                                _dbContext.Roles.Any(otherRole =>
                                    otherRole.Id == otherUserRole.RoleId &&
                                    otherRole.IsActive)));

                if (leavesUserWithoutActiveRole)
                {
                    return RoleFailure(
                        "Bu rol bazı kullanıcıların tek aktif rolü. " +
                        "Önce bu kullanıcılara başka bir aktif rol atayın.");
                }
            }

            role.IsActive = targetIsActive;

            return await _roleManager.UpdateAsync(role);
        });

        if (!result.Succeeded)
        {
            return RoleWarning(string.Join(
                " ",
                result.Errors.Select(error => error.Description)));
        }

        TempData["SuccessMessage"] = targetIsActive
            ? "Rol aktif durumda."
            : "Rol pasif durumda.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return BadRequest();
        }

        IdentityResult result = await RunRoleTransactionAsync(async () =>
        {
            var role = await _roleManager.FindByIdAsync(id);

            if (role == null)
            {
                return RoleFailure("Rol bulunamadı. Daha önce silinmiş olabilir.");
            }

            if (IsProtectedRole(role))
            {
                return RoleFailure("Sistem rolleri silinemez.");
            }

            bool hasUsers = await _dbContext.UserRoles
                .AnyAsync(userRole => userRole.RoleId == role.Id);

            if (hasUsers)
            {
                return RoleFailure(
                    "Bu rol kullanıcılara atanmış durumda. " +
                    "Silmeden önce kullanıcıların rol atamalarını değiştirin.");
            }

            return await _roleManager.DeleteAsync(role);
        });

        if (!result.Succeeded)
        {
            return RoleWarning(string.Join(
                " ",
                result.Errors.Select(error => error.Description)));
        }

        TempData["SuccessMessage"] = "Rol silindi.";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Users(string? id, string? search, int pageNumber = 1)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            TempData["WarningMessage"] =
                "Kullanıcılarını görmek istediğiniz rolü seçin.";

            return RedirectToAction(nameof(Index));
        }

        var role = await _roleManager.FindByIdAsync(id);

        if (role == null)
        {
            TempData["WarningMessage"] =
                "Rol bulunamadı. Silinmiş olabilir.";

            return RedirectToAction(nameof(Index));
        }

        const int pageSize = 20;

        search = search?.Trim() ?? string.Empty;

        // Yalnızca seçilen role atanmış kullanıcılar.
        // Pasif rollerin mevcut atamalarını da görüntüleyebiliriz.
        var usersQuery = _dbContext.Users
            .AsNoTracking()
            .Where(user => _dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                userRole.RoleId == role.Id));

        int totalUserCount = await usersQuery.CountAsync();

        // Arama da veritabanında yapılır.
        if (!string.IsNullOrWhiteSpace(search))
        {
            usersQuery = usersQuery.Where(user =>
                (user.FullName != null &&
                 user.FullName.Contains(search)) ||

                (user.UserName != null &&
                 user.UserName.Contains(search)) ||

                (user.Email != null &&
                 user.Email.Contains(search)) ||

                (user.PhoneNumber != null &&
                 user.PhoneNumber.Contains(search)));
        }

        int filteredUserCount = string.IsNullOrWhiteSpace(search)
            ? totalUserCount
            : await usersQuery.CountAsync();

        int totalPages = Math.Max(
            1,
            (int)Math.Ceiling((double)filteredUserCount / pageSize));

        pageNumber = Math.Clamp(pageNumber, 1, totalPages);

        var users = await usersQuery
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new RoleUserItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            })
            .ToListAsync();

        return View(new RoleUsersViewModel
        {
            RoleId = role.Id,
            RoleName = role.Name ?? string.Empty,

            DisplayName = string.IsNullOrWhiteSpace(role.DisplayName)
                ? role.Name ?? string.Empty
                : role.DisplayName,

            Description = role.Description,
            IsActive = role.IsActive,

            IsSystemRole = role.IsSystemRole ||
                string.Equals(
                    role.Name,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase),

            Search = search,
            TotalUserCount = totalUserCount,
            FilteredUserCount = filteredUserCount,
            CurrentPage = pageNumber,
            PageSize = pageSize,
            Users = users
        });
    }

    // Admin, eski kayıtlarda IsSystemRole işaretlenmemiş olsa da korunur.
    private static bool IsProtectedRole(AppRole role)
    {
        return role.IsSystemRole ||
               string.Equals(
                   role.Name,
                   "Admin",
                   StringComparison.OrdinalIgnoreCase);
    }


    // Beklenen işlem hatasını IdentityResult biçiminde döndürür.
    private static IdentityResult RoleFailure(string message)
    {
        return IdentityResult.Failed(new IdentityError
        {
            Code = "RoleOperationRejected",
            Description = message
        });
    }


    // Uyarıyı Index sayfasında gösterir.
    private IActionResult RoleWarning(string message)
    {
        TempData["WarningMessage"] = message;

        return RedirectToAction(nameof(Index));
    }


    // Kullanıcı/rol kontrolleriyle değişikliği aynı transaction'da yapar.
    // Bu yardımcı yalnızca SetActive ve Delete tarafından kullanılır.
    private async Task<IdentityResult> RunRoleTransactionAsync(
        Func<Task<IdentityResult>> operation)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Yeniden deneme olursa önceki denemenin izlenen
            // nesnelerini kullanmadan veritabanından tekrar oku.
            _dbContext.ChangeTracker.Clear();

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            IdentityResult result = await operation();

            if (result.Succeeded)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }

            return result;
        });
    }

}