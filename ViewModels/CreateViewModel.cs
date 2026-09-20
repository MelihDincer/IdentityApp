using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace IdentityApp.ViewModels;

public class CreateViewModel
{
     [Required(ErrorMessage = "Ad gereklidir.")]
    public string FullName { get; set; } = string.Empty;
    [Required(ErrorMessage = "E-posta gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } = string.Empty;
    [Required(ErrorMessage = "Şifre gereklidir.")]
    [StringLength(100, ErrorMessage = "Şifre en az 6 karakter olmalıdır.", MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
    [Required(ErrorMessage = "Şifre tekrarı gereklidir.")]
    [Compare(nameof(Password), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string ConfirmPassword { get; set; } = string.Empty;
    public bool EmailConfirmed { get; set; } = false;
    public bool PhoneNumberConfirmed { get; set; } = false;
    [Required(ErrorMessage = "En az bir rol seçmelisiniz.")]
    [MinLength(1, ErrorMessage = "En az bir rol seçmelisiniz.")]
    public string[] Roles { get; set; } = Array.Empty<string>();

    [BindNever]
    [ValidateNever]
    public List<RoleOptionViewModel> AvailableRoles { get; set; } = new();
    
}