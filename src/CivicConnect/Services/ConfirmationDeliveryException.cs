namespace CivicConnect.Core.Services;

public sealed class ConfirmationDeliveryException : Exception
{
    public ConfirmationDeliveryException(Exception innerException)
        : base("The confirmation message could not be delivered.", innerException)
    {
    }
}
