using Microsoft.Extensions.Logging;

namespace TermRTS.Log;

/// <summary>
///     Size-based rolling file logger matching the previous log4net RollingFileAppender:
///     25MB files, 5 backups, process-id file name.
/// </summary>
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly int _maxBackupFiles;
    private readonly long _maxFileSizeBytes;
    private readonly Lock _sync = new();
    private bool _disposed;
    private StreamWriter? _writer;

    public RollingFileLoggerProvider(string filePath, long maxFileSizeBytes, int maxBackupFiles)
    {
        _filePath = filePath;
        _maxFileSizeBytes = maxFileSizeBytes;
        _maxBackupFiles = maxBackupFiles;
        _writer = CreateWriter();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new RollingFileLogger(this, categoryName);
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    internal void Write(string categoryName, LogLevel logLevel, string message,
        Exception? exception)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss,fff");
        var line = exception == null
            ? $"{timestamp} [{logLevel}] {categoryName} - {message}"
            : $"{timestamp} [{logLevel}] {categoryName} - {message}{Environment.NewLine}{exception}";

        lock (_sync)
        {
            if (_disposed || _writer == null) return;
            RollIfNeeded(line.Length);
            _writer.WriteLine(line);
            _writer.Flush();
        }
    }

    private void RollIfNeeded(int nextLineLength)
    {
        if (_writer == null) return;
        if (_writer.BaseStream.Length + nextLineLength + Environment.NewLine.Length
            < _maxFileSizeBytes)
            return;

        _writer.Dispose();
        _writer = null;

        var oldest = $"{_filePath}.{_maxBackupFiles}";
        if (File.Exists(oldest)) File.Delete(oldest);
        for (var i = _maxBackupFiles - 1; i >= 1; i--)
        {
            var source = $"{_filePath}.{i}";
            if (File.Exists(source)) File.Move(source, $"{_filePath}.{i + 1}");
        }

        if (File.Exists(_filePath)) File.Move(_filePath, $"{_filePath}.1");
        _writer = CreateWriter();
    }

    private StreamWriter CreateWriter()
    {
        var stream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        return new StreamWriter(stream) { AutoFlush = false };
    }

    private sealed class RollingFileLogger(RollingFileLoggerProvider provider, string categoryName)
        : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(categoryName, logLevel, formatter(state, exception), exception);
        }
    }
}