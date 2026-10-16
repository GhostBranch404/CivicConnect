namespace CivicConnect.Core.Services;

/// <summary>
/// SMTP settings used to send confirmation codes (for example Resend: smtp.resend.com, port 587,
/// user name "resend", password = the Resend API key).
/// </summary>
public sealed class EmailSmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "CivicConnect";
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// Reads the settings from configuration. Each setting can be given as a section key
    /// (Email:Host, which is the Azure app setting / environment variable "Email__Host")
    /// or as a flat key ("Email_Host" or "EmailHost"), so either naming style works in Azure.
    /// </summary>
    public static void Bind(EmailSmtpOptions options, IConfiguration configuration)
    {
        string? Read(string name)
        {
            foreach (var key in new[] { $"Email:{name}", $"Email_{name}", $"Email{name}" })
            {
                var value = configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return null;
        }

        options.Host = Read(nameof(Host)) ?? options.Host;
        options.UserName = Read(nameof(UserName)) ?? options.UserName;
        options.Password = Read(nameof(Password)) ?? options.Password;
        options.FromAddress = Read(nameof(FromAddress)) ?? options.FromAddress;
        options.FromName = Read(nameof(FromName)) ?? options.FromName;

        if (int.TryParse(Read(nameof(Port)), out var port))
            options.Port = port;
        if (bool.TryParse(Read(nameof(EnableSsl)), out var ssl))
            options.EnableSsl = ssl;
        if (int.TryParse(Read(nameof(TimeoutSeconds)), out var timeout) && timeout > 0)
            options.TimeoutSeconds = timeout;
    }

    /// <summary>Names of required settings that are missing. Never includes values.</summary>
    public IReadOnlyList<string> MissingSettings()
    {
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(Host)) missing.Add("Email Host");
        if (string.IsNullOrWhiteSpace(FromAddress)) missing.Add("Email FromAddress");
        if (!string.IsNullOrWhiteSpace(UserName) && string.IsNullOrWhiteSpace(Password)) missing.Add("Email Password");
        return missing;
    }
}
