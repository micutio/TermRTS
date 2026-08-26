using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TermRTS.Log;

/// <summary>
///     Host-configured logger factory for the engine. Defaults to <see cref="NullLoggerFactory" />
///     so tests and unconfigured consumers emit nothing.
/// </summary>
public static class TermRtsLog
{
    public static ILoggerFactory Factory { get; set; } = NullLoggerFactory.Instance;

    public static ILogger<T> For<T>() => Factory.CreateLogger<T>();
}