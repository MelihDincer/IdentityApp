using System.ComponentModel.DataAnnotations;

namespace IdentityApp.ViewModels;

public class CreateViewModel
{
    [Required(ErrorMessage = "Kullanıcı adı gereklidir.")]
    public string UserName { get; set; } = String.Empty;
    [Required(ErrorMessage = "E-posta gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = String.Empty;
    public string? PhoneNumber { get; set; } = String.Empty;
    [Required(ErrorMessage = "Şifre gereklidir.")]
    [StringLength(100, ErrorMessage = "Şifre en az 6 karakter olmalıdır.", MinimumLength = 6)]
    public string Password { get; set; } = String.Empty;
    [Required(ErrorMessage = "Şifre tekrarı gereklidir.")]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = String.Empty;
    public bool EmailConfirmed { get; set; } = false;
    public bool PhoneNumberConfirmed { get; set; } = false;
}