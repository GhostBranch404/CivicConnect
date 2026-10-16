using System.ComponentModel.DataAnnotations;

namespace CivicConnect.Core.Models;

public sealed class UserRegistrationRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;
}
