using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace IdentityApp.ViewModels;

public class EditRoleViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [BindNever]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Görünen rol adı gereklidir.")]
    [StringLength(100, ErrorMessage = "Görünen rol adı en fazla 100 karakter olabilir.")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
    public string? Description { get; set; }

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}