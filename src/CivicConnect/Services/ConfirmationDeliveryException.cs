namespace CivicConnect.Core.Services;

/// <summary>The confirmation email could not be sent.</summary>
public sealed class ConfirmationDeliveryException : Exception
{
    public ConfirmationDeliveryException(Exception innerException)
        : base("The confirmation email could not be sent.", innerException)
    {
    }
}

/// <summary>An account (other than an unconfirmed sign-up) already uses this email address.</summary>
public sealed class AccountAlreadyExistsException : Exception
{
    public AccountAlreadyExistsException()
        : base("An account with that email address already exists.")
    {
    }
}
