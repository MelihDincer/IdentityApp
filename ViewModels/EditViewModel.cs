using System.ComponentModel.DataAnnotations;

public class EditViewModel
{
    public string? Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; } = string.Empty;
    public bool ChangePassword { get; set; }
    public string? NewPassword { get; set; }
    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
    public string? ConfirmNewPassword { get; set; }
    public bool EmailConfirmed { get; set; } = false;
    public bool PhoneNumberConfirmed { get; set; } = false;
}