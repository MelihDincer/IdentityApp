using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace IdentityApp.Models;

public class AppRole : IdentityRole
{
    [Required(ErrorMessage = "Görünen rol adı gereklidir.")]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemRole { get; set; } = false;
}