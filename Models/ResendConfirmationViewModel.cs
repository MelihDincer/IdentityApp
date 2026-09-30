using System.ComponentModel.DataAnnotations;

namespace IdentityApp.ViewModels;

public class ResendConfirmationViewModel
{
    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = string.Empty;
}