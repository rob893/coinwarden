using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Coinwarden.API.Utilities;

public static class UtilityFunctions
{
    /// <summary>
    /// Compares two strings for equality in constant time relative to their length to avoid
    /// leaking information through timing side channels (e.g. when comparing security tokens).
    /// </summary>
    /// <param name="left">The first string, may be null.</param>
    /// <param name="right">The second string, may be null.</param>
    /// <returns><see langword="true"/> if both strings are non-null and have identical UTF-8 bytes; otherwise <see langword="false"/>.</returns>
    public static bool FixedTimeEquals(string? left, string? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    public static string GetControllerName<T>()
    {
        var typeName = typeof(T).Name;
        var splitOn = "Controller";

        if (typeName == null || !typeName.EndsWith(splitOn, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Controllers must end with 'Controller'. {typeName} does not.", nameof(T));
        }

        return typeName.Split(splitOn).First();
    }

    public static LogLevel LogLevelFromString(string logLevel)
    {
        ArgumentNullException.ThrowIfNull(logLevel);

        return logLevel.ToUpperInvariant() switch
        {
            "TRACE" => LogLevel.Trace,
            "DEBUG" => LogLevel.Debug,
            "INFORMATION" => LogLevel.Information,
            "WARNING" => LogLevel.Warning,
            "ERROR" => LogLevel.Error,
            "CRITICAL" => LogLevel.Critical,
            _ => throw new ArgumentException($"{nameof(logLevel)} must be Trace, Debug, Information, Warning, Error, or Critical.", nameof(logLevel)),
        };
    }
}