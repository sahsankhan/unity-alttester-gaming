namespace TrashCat.Tests.Infrastructure;

public static class TestConfig
{
    public static string RepoRoot =>
        Environment.GetEnvironmentVariable("REPO_ROOT")
        ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    public static string ReportsRoot =>
        Path.Combine(RepoRoot, Environment.GetEnvironmentVariable("REPORT_ROOT") ?? "reports");

    public static string Host =>
        Environment.GetEnvironmentVariable("ALT_DRIVER_HOST") ?? "127.0.0.1";

    public static int Port =>
        int.TryParse(Environment.GetEnvironmentVariable("ALT_DRIVER_PORT"), out var port) ? port : 13000;

    public static int RecordFrameMs =>
        int.TryParse(Environment.GetEnvironmentVariable("RECORD_FRAME_MS"), out var ms) ? ms : 500;

    public static bool RecordVideo =>
        (Environment.GetEnvironmentVariable("RECORD_VIDEO") ?? "1") is "1" or "true" or "TRUE";

    public static string RunId =>
        Environment.GetEnvironmentVariable("TEST_RUN_ID")
        ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
}
