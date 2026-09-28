using System.Diagnostics;

namespace TrashCat.Tests.Infrastructure;

/// <summary>
/// Captures AltTester PNG frames during the run and encodes an MP4 with ffmpeg when available.
/// </summary>
public sealed class FrameVideoRecorder
{
    private readonly AltDriver _driver;
    private readonly string _framesDir;
    private readonly int _minIntervalMs;
    private int _frameIndex;
    private long _lastCaptureTicks;

    public FrameVideoRecorder(AltDriver driver, string framesDir, int intervalMs)
    {
        _driver = driver;
        _framesDir = framesDir;
        _minIntervalMs = Math.Max(0, intervalMs);
        _lastCaptureTicks = -_minIntervalMs;   // allow first capture immediately
    }

    /// <summary>Index of the next frame that will be written (i.e. the count of frames captured).</summary>
    public int NextFrameIndex => _frameIndex;

    /// <summary>Index of the most recently captured frame, or -1 if none yet.</summary>
    public int LastFrameIndex => _frameIndex - 1;

    public string FramesDir => _framesDir;

    public void Start()
    {
        Directory.CreateDirectory(_framesDir);
    }

    /// <summary>
    /// AltTester allows one command at a time on a connection. Capture from the test thread
    /// so a background screenshot cannot steal the reply meant for findObject.
    ///
    /// Screenshots are throttled to <c>intervalMs</c>: capture calls between intervals return
    /// -1 quickly without hitting the driver. This keeps the dodge loop responsive because
    /// GetPNGScreenshot blocks the driver for a few hundred milliseconds each time.
    /// </summary>
    public int CaptureFrame()
    {
        var now = Environment.TickCount64;
        if (now - _lastCaptureTicks < _minIntervalMs)
        {
            return -1;
        }

        try
        {
            var path = Path.Combine(_framesDir, $"frame_{_frameIndex:D5}.png");
            _driver.GetPNGScreenshot(path);
            var index = _frameIndex;
            _frameIndex++;
            _lastCaptureTicks = Environment.TickCount64;
            return index;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[recorder] frame capture skipped: {ex.Message}");
            return -1;
        }
    }

    public Task<string?> StopAndEncodeAsync(string outputMp4)
    {

        if (_frameIndex == 0 || !TestConfig.RecordVideo)
        {
            return Task.FromResult<string?>(null);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputMp4)!);
        if (!TryEncodeWithFfmpeg(_framesDir, outputMp4))
        {
            Console.WriteLine("[recorder] ffmpeg not found or encode failed — frames kept on disk.");
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(File.Exists(outputMp4) ? outputMp4 : null);
    }

    /// <summary>
    /// Produces a single PNG that tiles ~24 evenly-spaced frames from the run into a grid.
    /// Handy for an at-a-glance review of the whole run without loading a video player.
    /// Returns the output path if the image was generated, otherwise null.
    /// </summary>
    public string? GenerateContactSheet(string outputPng, int columns = 6, int rows = 4)
    {
        if (_frameIndex == 0)
        {
            return null;
        }

        var ffmpeg = FindFfmpeg();
        if (ffmpeg == null)
        {
            return null;
        }

        var target = Math.Max(1, columns * rows);
        var stride = Math.Max(1, _frameIndex / target);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPng)!);

        var inputPattern = Path.Combine(_framesDir, "frame_%05d.png");
        var vf = $"select='not(mod(n\\,{stride}))',scale=320:-1,tile={columns}x{rows}";
        // -update 1 tells the image2 muxer to write a single output image (required for
        // static filenames like foo.png). -fps_mode vfr replaces the removed -vsync option.
        var args =
            $"-y -i \"{inputPattern}\" -vf \"{vf}\" -frames:v 1 -update 1 -fps_mode vfr \"{outputPng}\"";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        if (process == null)
        {
            return null;
        }

        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(60_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return null;
        }

        return process.ExitCode == 0 && File.Exists(outputPng) ? outputPng : null;
    }

    private static bool TryEncodeWithFfmpeg(string framesDir, string outputMp4)
    {
        var ffmpeg = FindFfmpeg();
        if (ffmpeg == null)
        {
            return false;
        }

        var inputPattern = Path.Combine(framesDir, "frame_%05d.png");
        var args =
            $"-y -framerate 2 -i \"{inputPattern}\" -c:v libx264 -pix_fmt yuv420p \"{outputMp4}\"";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = ffmpeg,
            Arguments = args,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        if (process == null)
        {
            return false;
        }

        // ffmpeg writes progress to stderr. If we redirect the streams but never read them,
        // the OS pipe buffer fills up, ffmpeg blocks, and WaitForExit waits the full timeout.
        // Draining both streams asynchronously keeps encoding fast.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(60_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return false;
        }

        return process.ExitCode == 0 && File.Exists(outputMp4);
    }

    private static string? FindFfmpeg()
    {
        foreach (var candidate in new[] { "ffmpeg", "ffmpeg.exe" })
        {
            try
            {
                using var which = Process.Start(new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = candidate,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                if (which == null)
                {
                    continue;
                }

                var line = which.StandardOutput.ReadLine();
                which.WaitForExit(5000);
                if (!string.IsNullOrWhiteSpace(line) && File.Exists(line.Trim()))
                {
                    return line.Trim();
                }
            }
            catch
            {
                // try next
            }
        }

        return null;
    }

}
