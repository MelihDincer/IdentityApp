using IdentityApp.Models;
using IdentityApp.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityApp.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;

    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var user = await _userManager.FindByEmailAsync(model.Email.Trim());
        if (user == null)
        {
            ModelState.AddModelError(
                string.Empty,
                "Giriş yapılamadı. E-posta ve parolanızı kontrol edin. " +
                "E-posta doğrulamanızı tamamladığınızdan emin olun.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user,model.Password,model.RememberMe,lockoutOnFailure: true);
        if (result.Succeeded)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        if (result.IsLockedOut)
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var now = DateTimeOffset.UtcNow;

            if (lockoutEnd.HasValue && lockoutEnd.Value > now)
            {
                var remainingMinutes = Math.Ceiling((lockoutEnd.Value - now).TotalMinutes);

                ModelState.AddModelError(
                    string.Empty,
                    $"Hesabınız geçici olarak kilitlendi. " +
                    $"Yaklaşık {remainingMinutes:0} dakika sonra tekrar deneyin.");
            }
            else
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Hesabınıza şu anda giriş yapılamıyor. " +
                    "Lütfen kısa bir süre sonra tekrar deneyin.");
            }

            return View(model);
        }

        if (result.RequiresTwoFactor)
        {
            // İki aşamalı doğrulama ekranını eklediğimizde
            // burada o ekrana yönlendirme yapılacak.
            // Bu sonuçta kullanıcı henüz tam olarak giriş yapmış değildir.
            ModelState.AddModelError(
                string.Empty,
                "Girişi tamamlamak için iki aşamalı doğrulama gerekiyor.");

            return View(model);
        }

        // IsNotAllowed: E-posta veya hesap doğrulaması gibi
        // giriş koşulları sağlanmadığında dönebilir.
        // Bu durum ve hatalı parola için aynı genel mesajı göster.
        ModelState.AddModelError(
            string.Empty,
            "Giriş yapılamadı. E-posta ve parolanızı kontrol edin. " +
            "E-posta doğrulamanızı tamamladığınızdan emin olun.");
    
        return View(model);
        }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }
}
