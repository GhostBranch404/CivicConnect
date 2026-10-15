using System.ComponentModel.DataAnnotations;

namespace CivicConnect.Core.Models;

public sealed class ConfirmUserRequest
{
    [Required]
    public string ConfirmationMethod { get; set; } = string.Empty;

    [Required]
    public string Destination { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}
