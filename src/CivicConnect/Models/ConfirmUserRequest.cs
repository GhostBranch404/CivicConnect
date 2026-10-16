using System.ComponentModel.DataAnnotations;

namespace CivicConnect.Core.Models;

public sealed class ConfirmUserRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}
