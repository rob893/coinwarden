using System.Threading;
using System.Threading.Tasks;
using Coinwarden.API.Models.Entities;

namespace Coinwarden.API.Services.Email;

public interface IEmailService
{
    Task SendEmailConfirmationToUserAsync(User user, string token, CancellationToken cancellationToken);

    Task SendResetPasswordToUserAsync(User user, string token, CancellationToken cancellationToken);

    Task SendEmailToUserAsync(User user, string subject, string plainTextMessage, string htmlMessage, CancellationToken cancellationToken);
}