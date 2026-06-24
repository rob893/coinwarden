using Azure.Communication.Email;
using Azure.Core;

namespace Coinwarden.API.Utilities;

public interface IAcsEmailClientFactory
{
    EmailClient CreateClient(TokenCredential? tokenCredential = null);
}