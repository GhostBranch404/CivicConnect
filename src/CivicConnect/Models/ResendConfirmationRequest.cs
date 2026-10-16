using System.ComponentModel.DataAnnotations;

namespace CivicConnect.Core.Models;

public sealed class ResendConfirmationRequest
{
    [Required]
    public string ConfirmationMethod { get; set; } = string.Empty;

    [Required]
    public string Destination { get; set; } = string.Empty;
}
