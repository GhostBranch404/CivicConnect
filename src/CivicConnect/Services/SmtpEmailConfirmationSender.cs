using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace CivicConnect.Core.Services;

public sealed class SmtpEmailConfirmationSender : IEmailConfirmationSender
{
    private readonly EmailSmtpOptions _options;

    public SmtpEmailConfirmationSender(IOptions<EmailSmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string destination, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            throw new InvalidOperationException("SMTP host and sender address must be configured.");
        }

        using var message = new MailMessage(
            _options.FromAddress,
            destination,
            "Confirm your CivicConnect account",
            $"Your CivicConnect confirmation code is {code}. It expires in 10 minutes.");
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.UserName)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.UserName, _options.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
