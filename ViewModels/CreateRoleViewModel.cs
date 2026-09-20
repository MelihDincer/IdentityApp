using System.ComponentModel.DataAnnotations;

namespace IdentityApp.ViewModels;

public class CreateRoleViewModel
{
    [Required(ErrorMessage = "Teknik rol adı gereklidir.")]
    [StringLength(256, ErrorMessage = "Teknik rol adı en fazla 256 karakter olabilir.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Görünen rol adı gereklidir.")]
    [StringLength(100, ErrorMessage = "Görünen rol adı en fazla 100 karakter olabilir.")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    public string? Description { get; set; }
}