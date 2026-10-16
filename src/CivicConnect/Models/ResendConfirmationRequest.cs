using System.ComponentModel.DataAnnotations;

namespace CivicConnect.Core.Models;

public sealed class ResendConfirmationRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
}
