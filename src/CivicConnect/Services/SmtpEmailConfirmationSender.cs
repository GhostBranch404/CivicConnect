using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Options;

namespace CivicConnect.Core.Services;

public sealed class SmtpEmailConfirmationSender : IEmailConfirmationSender
{
    private readonly EmailSmtpOptions _options;

    public SmtpEmailConfirmationSender(IOptions<EmailSmtpOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendAsync(string email, string fullName, string code, CancellationToken cancellationToken)
    {
        var missing = _options.MissingSettings();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Email is not configured. Missing: {string.Join(", ", missing)}.");

        // System.Net.Mail only supports STARTTLS (port 587/25/2587), not implicit TLS on port 465.
        if (_options.Port == 465)
            throw new InvalidOperationException("Email Port 465 is not supported. Use 587 with EnableSsl = true.");

        var from = new MailAddress(_options.FromAddress);
        if (string.IsNullOrWhiteSpace(from.DisplayName) && !string.IsNullOrWhiteSpace(_options.FromName))
            from = new MailAddress(from.Address, _options.FromName);

        using var message = new MailMessage(from, new MailAddress(email))
        {
            Subject = $"Your CivicConnect confirmation code: {code}",
            Body = PlainBody(fullName, code),
            IsBodyHtml = false
        };
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            HtmlBody(fullName, code), null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = _options.TimeoutSeconds * 1000
        };
        if (!string.IsNullOrWhiteSpace(_options.UserName))
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);

        await client.SendMailAsync(message, cancellationToken);
    }

    private static string Greeting(string fullName) =>
        string.IsNullOrWhiteSpace(fullName) ? "Hi" : $"Hi {fullName.Trim().Split(' ')[0]}";

    private static string PlainBody(string fullName, string code) =>
        $"{Greeting(fullName)},\n\n" +
        $"Your CivicConnect confirmation code is: {code}\n\n" +
        "Enter it on the confirmation screen to finish creating your account. The code expires in 10 minutes.\n\n" +
        "If you didn't try to create a CivicConnect account, you can ignore this email.\n";

    private static string HtmlBody(string fullName, string code)
    {
        var greeting = WebUtility.HtmlEncode(Greeting(fullName));
        return $@"<!doctype html><html><body style=""margin:0;background:#f3f6f8;font-family:Segoe UI,Arial,sans-serif;color:#15283a"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:32px 16px"">
<table role=""presentation"" width=""100%"" style=""max-width:480px;background:#ffffff;border-radius:12px;border:1px solid #e1e6ec"" cellpadding=""0"" cellspacing=""0"">
<tr><td style=""background:#10293d;color:#ffffff;padding:18px 24px;border-radius:12px 12px 0 0;font-size:18px;font-weight:700"">Civic<span style=""color:#4fc3b3"">Connect</span></td></tr>
<tr><td style=""padding:24px"">
<p style=""margin:0 0 12px"">{greeting},</p>
<p style=""margin:0 0 18px"">Use this code to finish creating your CivicConnect account:</p>
<p style=""margin:0 0 18px;font-size:32px;font-weight:700;letter-spacing:8px;color:#0f7a6c"">{code}</p>
<p style=""margin:0 0 6px;color:#627284;font-size:13px"">The code expires in 10 minutes.</p>
<p style=""margin:0;color:#627284;font-size:13px"">If you didn't try to create an account, you can ignore this email.</p>
</td></tr></table></td></tr></table></body></html>";
    }
}
