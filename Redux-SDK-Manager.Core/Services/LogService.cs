using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Redux_SDK_Manager.Services;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3
}

public interface ILogService
{
    void Info(string message, [CallerFilePath] string source = "", [CallerMemberName] string member = "");
    void Warn(string message, [CallerFilePath] string source = "", [CallerMemberName] string member = "");
    void Error(string message, Exception? exception = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "");
    void Debug(string message, [CallerFilePath] string source = "", [CallerMemberName] string member = "");
    string? CurrentLogFilePath { get; }

    /// <summary>
    /// The lowest level that will actually be written. Defaults to <see cref="Services.LogLevel.Info"/>;
    /// lower it to <see cref="Services.LogLevel.Debug"/> to troubleshoot a specific session.
    /// </summary>
    LogLevel MinimumLevel { get; set; }
}

public sealed class LogService : ILogService, IDisposable
{
    private const int MaxLogFilesToKeep = 10;
    private const long DefaultMaxLogFileSizeBytes = 20 * 1024 * 1024;
    private const string LogFilePrefix = "manager-";
    private const string LogFileExtension = ".log";

    private readonly IFileSystem _fileSystem;
    private readonly long _maxLogFileSizeBytes;
    private readonly object _writeLock = new();
    private StreamWriter? _writer;
    private bool _disposed;
    private bool _sizeCapReached;
    private readonly int _processId;

    public string? CurrentLogFilePath { get; private set; }
    public LogLevel MinimumLevel { get; set; } = LogLevel.Info;

    public LogService(IFileSystem fileSystem, IEnvironmentProvider environmentProvider, long maxLogFileSizeBytes = DefaultMaxLogFileSizeBytes)
    {
        _fileSystem = fileSystem;
        _maxLogFileSizeBytes = maxLogFileSizeBytes;
        _processId = environmentProvider.ProcessId;
        try
        {
            var logsDir = LocalStoragePaths.GetLogsDirectory(fileSystem, environmentProvider);
            fileSystem.Directory.CreateDirectory(logsDir);
            RotateOldLogs(logsDir);

            var fileName = LogFilePrefix + DateTime.Now.ToString("yyyyMMdd-HHmmss") + LogFileExtension;
            CurrentLogFilePath = fileSystem.Path.Combine(logsDir, fileName);

            var stream = fileSystem.FileStream.New(CurrentLogFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            WriteHeader();
        }
        catch (Exception ex)
        {
            _writer = null;
            CurrentLogFilePath = null;
            Console.WriteLine($"[LogService] Failed to open log file, falling back to console only: {ex.Message}");
        }
    }

    private void WriteHeader()
    {
        var asm = typeof(LogService).Assembly.GetName();
        var header = new StringBuilder();
        header.AppendLine("===== Redux SDK Manager Log =====");
        header.AppendLine($"Started: {DateTime.Now:O}");
        header.AppendLine($"Version: {asm.Version}");
        header.AppendLine($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        header.AppendLine($"Framework: {RuntimeInformation.FrameworkDescription}");
        header.AppendLine($"Process: {_processId}");
        header.AppendLine("=================================");
        lock (_writeLock)
        {
            _writer?.Write(header.ToString());
        }
        Console.Write(header.ToString());
    }

    private void RotateOldLogs(string logsDir)
    {
        try
        {
            var stale = _fileSystem.Directory
                .EnumerateFiles(logsDir, LogFilePrefix + "*" + LogFileExtension)
                .Select(p => new { Path = p, Info = _fileSystem.FileInfo.New(p) })
                .OrderByDescending(f => f.Info.LastWriteTimeUtc)
                .Skip(MaxLogFilesToKeep - 1)
                .ToList();

            foreach (var file in stale)
            {
                try { _fileSystem.File.Delete(file.Path); }
                catch { /* best-effort cleanup */ }
            }
        }
        catch { /* best-effort cleanup */ }
    }

    public void Info(string message, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
        => Write(LogLevel.Info, "INFO", message, null, source, member);

    public void Warn(string message, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
        => Write(LogLevel.Warn, "WARN", message, null, source, member);

    public void Error(string message, Exception? exception = null, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
        => Write(LogLevel.Error, "ERROR", message, exception, source, member);

    public void Debug(string message, [CallerFilePath] string source = "", [CallerMemberName] string member = "")
        => Write(LogLevel.Debug, "DEBUG", message, null, source, member);

    private void Write(LogLevel level, string levelName, string message, Exception? exception, string source, string member)
    {
        if (_disposed || level < MinimumLevel) return;

        var sourceName = string.IsNullOrEmpty(source)
            ? ""
            : _fileSystem.Path.GetFileNameWithoutExtension(source);

        // Tag every line with the process id so a fragment pasted out of a bug report can still be
        // matched back to which run (and which header, further up the file) it came from.
        var prefix = string.IsNullOrEmpty(sourceName)
            ? $"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff}] [pid={_processId}] [{levelName}]"
            : $"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff}] [pid={_processId}] [{levelName}] [{sourceName}.{member}]";

        var line = $"{prefix} {message}";

        lock (_writeLock)
        {
            try
            {
                if (!_sizeCapReached && CurrentLogFilePath is not null &&
                    _fileSystem.FileInfo.New(CurrentLogFilePath).Length >= _maxLogFileSizeBytes)
                {
                    _sizeCapReached = true;
                    _writer?.WriteLine($"[{DateTime.Now:yyyy-MM-ddTHH:mm:ss.fff}] [pid={_processId}] [WARN] Log file reached {_maxLogFileSizeBytes} bytes, no further lines will be written to disk this session.");
                }
                if (!_sizeCapReached)
                {
                    _writer?.WriteLine(line);
                    if (exception != null) _writer?.WriteLine(exception.ToString());
                }
            }
            catch { /* never let logging throw into a caller */ }
        }
        Console.WriteLine(line);
        if (exception != null) Console.WriteLine(exception.ToString());
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_writeLock)
        {
            try { _writer?.Flush(); } catch { /* disposing */ }
            try { _writer?.Dispose(); } catch { /* disposing */ }
            _writer = null;
        }
    }
}
