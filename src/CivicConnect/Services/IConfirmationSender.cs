namespace CivicConnect.Core.Services;

public interface IConfirmationSender
{
    Task SendAsync(string destination, string code, CancellationToken cancellationToken);
}

public interface IEmailConfirmationSender : IConfirmationSender
{
}

public interface ISmsConfirmationSender : IConfirmationSender
{
}
