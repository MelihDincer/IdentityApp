using System.ComponentModel.DataAnnotations;

namespace IdentityApp.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Ad soyad gereklidir.")]
    [StringLength(150, ErrorMessage = "Ad soyad en fazla 150 karakter olabilir.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(256, ErrorMessage = "E-posta adresi çok uzun.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola gereklidir.")]
    [StringLength(
        100,
        MinimumLength = 6,
        ErrorMessage = "Parola 6 ile 100 karakter arasında olmalıdır.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarı gereklidir.")]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}