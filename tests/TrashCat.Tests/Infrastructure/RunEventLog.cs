using System.Diagnostics;
using System.Text.Json;

namespace TrashCat.Tests.Infrastructure;

/// <summary>
/// Append-only JSONL log of everything that happens during a run. One line per event with
/// a millisecond timestamp, event type, and free-form payload. Cheap to read and easy for an
/// AI reviewer (or a human) to skim after the fact.
/// </summary>
public sealed class RunEventLog
{
    private readonly string _path;
    private readonly object _lock = new();
    private readonly Stopwatch _watch = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public RunEventLog(string path)
    {
        _path = path;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        // Truncate any previous file so each run starts fresh.
        File.WriteAllText(_path, string.Empty);
        _watch.Start();
    }

    public string Path => _path;

    public void Log(string type, object? data = null, int? frameIndex = null)
    {
        var line = JsonSerializer.Serialize(new
        {
            t_ms = _watch.ElapsedMilliseconds,
            type,
            frame = frameIndex,
            data,
        }, JsonOptions);

        lock (_lock)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }

    public void Flush() { /* File.AppendAllText already flushes. */ }
}
