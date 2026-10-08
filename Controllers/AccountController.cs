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
    private readonly IEmailSender _email;
    private readonly IConfiguration _configuration;
    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        RoleManager<AppRole> roleManager,
        IdentityContext dbContext,
        AccountEmailService accountEmailService,
        ILogger<AccountController> logger,
        IEmailSender email,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _dbContext = dbContext;
        _accountEmailService = accountEmailService;
        _logger = logger;
        _email = email;
        _configuration = configuration;
    }

    [HttpGet]
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        ViewData["PasswordStep"] = true;
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        const string invalidCredentials = "E-posta adresi veya parola hatalı.";
        var user = await _userManager.FindByEmailAsync(model.Email.Trim());

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, invalidCredentials);
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (result.IsNotAllowed && !user.EmailConfirmed)
        {
            if (await _userManager.IsLockedOutAsync(user))
            {
                result = Microsoft.AspNetCore.Identity.SignInResult.LockedOut;
            }
            else
            {
                var passwordCorrect = await _userManager.CheckPasswordAsync(
                    user,
                    model.Password);

                if (passwordCorrect)
                {
                    ViewData["ShowEmailConfirmation"] = true;

                    ModelState.AddModelError(
                        string.Empty,
                        "Giriş yapabilmek için e-posta adresinizi doğrulamalısınız.");

                    return View(model);
                }

                // Bu dalda SignInManager parola kontrolünü atladığından
                // yanlış denemeyi burada sayıyoruz.
                if (await _userManager.GetLockoutEnabledAsync(user))
                {
                    var failureResult =
                        await _userManager.AccessFailedAsync(user);

                    if (!failureResult.Succeeded)
                    {
                        ModelState.AddModelError(
                            string.Empty,
                            "Giriş işlemi tamamlanamadı. Lütfen tekrar deneyin.");

                        return View(model);
                    }

                    if (await _userManager.IsLockedOutAsync(user))
                    {
                        result =
                            Microsoft.AspNetCore.Identity.SignInResult.LockedOut;
                    }
                }

                if (!result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, invalidCredentials);
                    return View(model);
                }
            }
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
        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(
                string.Empty,
                "Hesabınızın giriş koşulları henüz sağlanmamış. " +
                "Lütfen sistem yöneticisiyle iletişime geçin.");

            return View(model);
        }
        ModelState.AddModelError(string.Empty, invalidCredentials);
        return View(model);
    }

    [HttpGet]
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult LoginSuccess(string? returnUrl = null)
    {
        var targetUrl =
            !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("Index", "Home")!;
    
        return View("LoginSuccess", model: targetUrl);
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

            // Dışarıdan kayıt olan kullanıcıya yalnızca User rolü atanır.
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

                // Commit yapılmadığından kullanıcı kaydı da geri alınır.
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
            var errors = result.Errors
                .Select(error => error.Code switch
                {
                    "DuplicateEmail" or "DuplicateUserName" =>
                        "Bu bilgilerle yeni hesap oluşturulamıyor. " +
                        "Mevcut hesabınız varsa giriş yapabilirsiniz.",

                    "InvalidEmail" =>
                        "Geçerli bir e-posta adresi girin.",

                    "InvalidUserName" =>
                        "E-posta adresi kullanıcı adı olarak kullanılamıyor.",

                    "PasswordTooShort" =>
                        $"Parolanız en az " +
                        $"{_userManager.Options.Password.RequiredLength} " +
                        "karakter olmalıdır.",

                    "PasswordRequiresDigit" =>
                        "Parolanız en az bir rakam içermelidir.",

                    "PasswordRequiresLower" =>
                        "Parolanız en az bir küçük harf içermelidir.",

                    "PasswordRequiresUpper" =>
                        "Parolanız en az bir büyük harf içermelidir.",

                    "PasswordRequiresNonAlphanumeric" =>
                        "Parolanız en az bir özel karakter içermelidir.",

                    "RegistrationUnavailable" => error.Description,

                    _ => "Kayıt tamamlanamadı. Bilgilerinizi kontrol edin."
                })
                .Distinct()
                .ToArray();

            foreach (var error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return View(model);
        }

        // Mail, veritabanı işlemi tamamlandıktan sonra gönderilir.
        var sent = await _accountEmailService
            .TrySendConfirmationAsync(createdUser!);

        // Önceki işlemlerden kalan karşıt mesajı temizle.
        TempData.Remove("SuccessMessage");
        TempData.Remove("WarningMessage");

        if (sent)
        {
            TempData["SuccessMessage"] =
                "Hesabınız oluşturuldu. E-posta adresinize bir doğrulama " +
                "bağlantısı gönderdik. Giriş yapmadan önce e-posta adresinizi " +
                "doğrulayın. Gelen kutunuzu ve spam klasörünüzü kontrol edin.";
        }
        else
        {
            TempData["WarningMessage"] =
                "Hesabınız oluşturuldu ancak doğrulama e-postası gönderilemedi. " +
                "Aşağıdaki bağlantıdan yeniden doğrulama e-postası talep edin.";
        }

        TempData["ShowResendConfirmation"] = true;

        // POST tamamlandıktan sonra tarayıcı Login sayfasına GET isteği yapar.
        return RedirectToAction("Login", "Account");
    }


    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterConfirmation()
    {
        return View(new ResendConfirmationViewModel());
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

        return RedirectToAction("Login", "Account");
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

    [HttpGet]
[AllowAnonymous]
public IActionResult ForgotPassword()
{
    return View(new ForgotPasswordViewModel());
}

[HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
[EnableRateLimiting("account-email")]
public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    var user = await _userManager.FindByEmailAsync(model.Email.Trim());

    // Yalnızca e-postası doğrulanmış, parolayla giriş yapabilen hesaplara gönder.
    if (user != null &&
        user.EmailConfirmed &&
        await _userManager.HasPasswordAsync(user))
    {
        try
        {
            var baseUrl = _configuration["App:BaseUrl"];

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException(
                    "App:BaseUrl geçerli bir HTTPS adresi olmalıdır.");
            }

            var token =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var code = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));

            // Bağlantı AccountController içindeki Reset metoduna gider.
            var path = Url.Action(
                nameof(Reset),
                "Account",
                new
                {
                    userId = user.Id,
                    code
                });

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException(
                    "Parola sıfırlama bağlantısı oluşturulamadı.");
            }

            var link = baseUrl!.TrimEnd('/') + path;

            await _email.SendEmailAsync(
                user.Email!,
                "IdentityApp | Parolanızı sıfırlayın",
                PasswordResetEmailTemplate.Render(
                    user.FullName ?? "Kullanıcımız",
                    link));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Parola sıfırlama e-postası gönderilemedi. Kullanıcı: {UserId}",
                user.Id);
        }
    }

    // Hesabın varlığını açıklamıyoruz; kesin gönderim iddiasında bulunmuyoruz.
    TempData["InfoMessage"] =
        "Bu adresle kayıtlı, e-postası doğrulanmış ve parolayla giriş " +
        "yapılabilen bir hesabınız varsa sıfırlama bağlantısı gönderimi " +
        "talep edildi. Gelen kutunuzu ve spam klasörünüzü kontrol edin.";

    return RedirectToAction(nameof(Login));
}

[HttpGet]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public IActionResult Reset(string? userId, string? code)
{
    Response.Headers["Referrer-Policy"] = "no-referrer";

    // Başarılı POST sonrasında yönlendirme ile bu ekran açılır.
    if (TempData["PasswordResetSucceeded"] is true)
    {
        ViewData["ResetState"] = "Success";

        return View(new ResetPasswordViewModel());
    }

    if (string.IsNullOrWhiteSpace(userId) ||
        string.IsNullOrWhiteSpace(code))
    {
        ViewData["ResetState"] = "Invalid";

        return View(new ResetPasswordViewModel());
    }

    try
    {
        WebEncoders.Base64UrlDecode(code);
    }
    catch (FormatException)
    {
        ViewData["ResetState"] = "Invalid";

        return View(new ResetPasswordViewModel());
    }

    return View(new ResetPasswordViewModel
    {
        UserId = userId,
        Code = code
    });
}

[HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
[EnableRateLimiting("account-email")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public async Task<IActionResult> Reset(ResetPasswordViewModel model)
{
    Response.Headers["Referrer-Policy"] = "no-referrer";

    if (string.IsNullOrWhiteSpace(model.UserId) ||
        string.IsNullOrWhiteSpace(model.Code))
    {
        ViewData["ResetState"] = "Invalid";

        return View(model);
    }

    if (!ModelState.IsValid)
    {
        return View(model);
    }

    string token;

    try
    {
        token = Encoding.UTF8.GetString(
            WebEncoders.Base64UrlDecode(model.Code));
    }
    catch (FormatException)
    {
        ViewData["ResetState"] = "Invalid";

        return View(model);
    }

    var user = await _userManager.FindByIdAsync(model.UserId);

    if (user == null ||
        !user.EmailConfirmed ||
        !await _userManager.HasPasswordAsync(user))
    {
        ViewData["ResetState"] = "Invalid";

        return View(model);
    }

    var result = await _userManager.ResetPasswordAsync(
        user,
        token,
        model.Password);

    if (result.Succeeded)
    {
        TempData["PasswordResetSucceeded"] = true;

        // Sayfa yenilendiğinde parola formu tekrar gönderilmesin.
        return RedirectToAction(nameof(Reset));
    }

    if (result.Errors.Any(error => error.Code == "InvalidToken"))
    {
        ViewData["ResetState"] = "Invalid";

        return View(model);
    }

    var messages = result.Errors
        .Select(error => error.Code switch
        {
            "PasswordTooShort" =>
                $"Parola en az {_userManager.Options.Password.RequiredLength} karakter olmalıdır.",

            "PasswordRequiresDigit" =>
                "Parola en az bir rakam içermelidir.",

            "PasswordRequiresLower" =>
                "Parola en az bir küçük harf içermelidir.",

            "PasswordRequiresUpper" =>
                "Parola en az bir büyük harf içermelidir.",

            "PasswordRequiresNonAlphanumeric" =>
                "Parola en az bir özel karakter içermelidir.",

            "PasswordRequiresUniqueChars" =>
                $"Parola en az {_userManager.Options.Password.RequiredUniqueChars} farklı karakter içermelidir.",

            _ =>
                "Parola değiştirilemedi. Lütfen tekrar deneyin."
        })
        .Distinct();

    foreach (var message in messages)
    {
        ModelState.AddModelError(string.Empty, message);
    }

    return View(model);
}
}