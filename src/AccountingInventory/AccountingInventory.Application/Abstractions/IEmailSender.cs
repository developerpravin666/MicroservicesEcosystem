namespace AccountingInventory.Application.Abstractions;

public interface IEmailSender
{
    Task SendAsync(string recipient, string subject, string htmlBody, CancellationToken cancellationToken);
    Task<bool> SendTestAsync(string recipient, CancellationToken cancellationToken);
}
