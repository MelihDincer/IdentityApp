using System.Data;
using System.Text;
using IdentityApp.Models;
using IdentityApp.Services.Email;
using IdentityApp.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace IdentityApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly RoleManager<AppRole> _roleManager;
    private readonly IdentityContext _dbContext;
    private readonly AccountEmailService _accountEmailService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        RoleManager<AppRole> roleManager,
        IdentityContext dbContext,
        AccountEmailService accountEmailService,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _accountEmailService = accountEmailService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        const string loginError =
            "Giriş yapılamadı. E-posta ve parolanızı kontrol edin. " +
            "E-posta doğrulamanızı tamamladığınızdan emin olun.";

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, loginError);
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var now = DateTimeOffset.UtcNow;

            var message =
                "Hesabınıza şu anda giriş yapılamıyor. " +
                "Lütfen kısa bir süre sonra tekrar deneyin.";

            if (lockoutEnd.HasValue && lockoutEnd.Value > now)
            {
                var minutes = Math.Ceiling(
                    (lockoutEnd.Value - now).TotalMinutes);

                message =
                    $"Hesabınız geçici olarak kilitlendi. " +
                    $"Yaklaşık {minutes:0} dakika sonra tekrar deneyin.";
            }

            ModelState.AddModelError(string.Empty, message);
            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            // İki aşamalı doğrulama eklenince ilgili ekrana yönlendirilecek.
            ModelState.AddModelError(
                string.Empty,
                "Girişi tamamlamak için iki aşamalı doğrulama gerekiyor.");

            return View(model);
        }

        ModelState.AddModelError(string.Empty, loginError);
        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account-email")]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        AppUser? createdUser = null;

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        var result = await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            createdUser = null;

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            // Rolü formdan almıyoruz; dış kayıtların rolü User.
            var role = await _roleManager.FindByNameAsync("User");

            if (role == null ||
                !role.IsActive ||
                string.IsNullOrWhiteSpace(role.Name))
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RegistrationUnavailable",
                    Description =
                        "Şu anda yeni kayıt alınamıyor. " +
                        "Lütfen daha sonra tekrar deneyin."
                });
            }

            var email = model.Email.Trim();

            var user = new AppUser
            {
                FullName = model.FullName.Trim(),
                UserName = email,
                Email = email,
                PhoneNumber = null,
                EmailConfirmed = false,
                PhoneNumberConfirmed = false
            };

            var createResult = await _userManager.CreateAsync(
                user,
                model.Password);

            if (!createResult.Succeeded)
            {
                return createResult;
            }

            var roleResult = await _userManager.AddToRoleAsync(
                user,
                role.Name);

            if (!roleResult.Succeeded)
            {
                _logger.LogWarning(
                    "Kayıtta varsayılan rol atanamadı. Hatalar: {Codes}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Code)));

                return IdentityResult.Failed(new IdentityError
                {
                    Code = "RegistrationUnavailable",
                    Description =
                        "Kayıt tamamlanamadı. Lütfen daha sonra tekrar deneyin."
                });
            }

            await transaction.CommitAsync();
            createdUser = user;

            return IdentityResult.Success;
        });

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                var message = error.Code switch
                {
                    "DuplicateEmail" or "DuplicateUserName" =>
                        "Bu bilgilerle yeni hesap oluşturulamıyor. " +
                        "Mevcut hesabınız varsa giriş yapabilirsiniz.",

                    "InvalidEmail" =>
                        "Geçerli bir e-posta adresi girin.",

                    "InvalidUserName" =>
                        "E-posta adresi kullanıcı adı olarak kullanılamıyor.",

                    "PasswordTooShort" =>
                        "Parolanız en az 6 karakter olmalıdır.",

                    "RegistrationUnavailable" => error.Description,

                    _ => "Kayıt tamamlanamadı. Bilgilerinizi kontrol edin."
                };

                ModelState.AddModelError(string.Empty, message);
            }

            return View(model);
        }

        var sent = await _accountEmailService.TrySendConfirmationAsync(createdUser!);

        TempData["InfoMessage"] = sent
    ? "Hesabınız oluşturuldu. Giriş yapabilmek için önce " +
      "e-posta adresinizi doğrulamanız gerekiyor. " +
      "Doğrulama bağlantısı e-posta adresinize gönderildi."
    : "Hesabınız oluşturuldu ancak doğrulama e-postası gönderilemedi. " +
      "Giriş yapabilmek için e-posta doğrulaması gereklidir. " +
      "Aşağıdaki formdan yeniden gönderim isteyebilirsiniz.";

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterConfirmation()
    {
        return View(
        "RegisterConfirmation",
        new ResendConfirmationViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("account-email")]
    public async Task<IActionResult> ResendConfirmation(
        ResendConfirmationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("RegisterConfirmation", model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email.Trim());

        if (user != null && !user.EmailConfirmed)
        {
            await _accountEmailService.TrySendConfirmationAsync(user);
        }

        // Hesabın varlığını veya onay durumunu açıklamıyoruz.
        TempData["InfoMessage"] =
            "Bu adresle doğrulama bekleyen bir hesap varsa " +
            "e-posta gönderimi talep edildi. " +
            "Gelen kutunuzu ve spam klasörünüzü kontrol edin.";

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? code)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(code))
        {
            return View("EmailConfirmation", false);
        }

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return View("EmailConfirmation", false);
        }

        string token;

        try
        {
            token = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return View("EmailConfirmation", false);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);

        return View("EmailConfirmation", result.Succeeded);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        // Cookie ayarındaki adres 404 vermesin.
        // Daha sonra buraya kurumsal bir hata görünümü bağlanabilir.
        return new ContentResult
        {
            StatusCode = StatusCodes.Status403Forbidden,
            ContentType = "text/plain; charset=utf-8",
            Content = "Bu sayfaya erişim yetkiniz bulunmuyor."
        };
    }
}