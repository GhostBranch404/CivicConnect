using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;

namespace CivicConnect.Core.Services;

public sealed class TwilioSmsConfirmationSender : ISmsConfirmationSender
{
    private readonly HttpClient _httpClient;
    private readonly TwilioSmsOptions _options;

    public TwilioSmsConfirmationSender(HttpClient httpClient, IOptions<TwilioSmsOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task SendAsync(string destination, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AccountSid) ||
            string.IsNullOrWhiteSpace(_options.AuthToken) ||
            string.IsNullOrWhiteSpace(_options.FromNumber))
        {
            throw new InvalidOperationException("Twilio account credentials and sender number must be configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(_options.AccountSid)}/Messages.json")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = destination,
                ["From"] = _options.FromNumber,
                ["Body"] = $"Your CivicConnect confirmation code is {code}. It expires in 10 minutes."
            })
        };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.AccountSid}:{_options.AuthToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
