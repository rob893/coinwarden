namespace Coinwarden.API.Constants;

public static class ApplicationSettings
{
    public const string HealthCheckEndpoint = "/health";

    public const string LivenessHealthCheckEndpoint = "/health/liveness";

    public const int SystemUserId = 1;
}