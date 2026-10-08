using System.ComponentModel.DataAnnotations;
namespace IdentityApp.ViewModels;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta adresinizi girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required] public string UserId { get; set; } = string.Empty;
    [Required] public string Code { get; set; } = string.Empty;
    [Required(ErrorMessage = "Yeni parolanızı girin.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Yeni parolanızı tekrar girin.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
