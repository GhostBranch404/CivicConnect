namespace CivicConnect.Core.Services;

/// <summary>Sends the 6-digit account confirmation code by email.</summary>
public interface IEmailConfirmationSender
{
    /// <summary>Throws if the email could not be handed to the mail server.</summary>
    Task SendAsync(string email, string fullName, string code, CancellationToken cancellationToken);
}
