namespace Coinwarden.API.Services.Core;

public interface ICorrelationIdService
{
    string CorrelationId { get; set; }
}