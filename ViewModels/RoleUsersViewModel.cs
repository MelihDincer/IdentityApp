namespace IdentityApp.ViewModels;

public class RoleUsersViewModel
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool IsActive { get; set; }
    public bool IsSystemRole { get; set; }

    public string Search { get; set; } = string.Empty;

    public int TotalUserCount { get; set; }
    public int FilteredUserCount { get; set; }

    public int CurrentPage { get; set; }
    public int PageSize { get; set; }

    public int TotalPages =>
        Math.Max(1, (int)Math.Ceiling(
            (double)FilteredUserCount / PageSize));

    public List<RoleUserItemViewModel> Users { get; set; } = new();
}

public class RoleUserItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
}