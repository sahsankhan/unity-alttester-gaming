using Allure.Net.Commons;
using TrashCat.Tests.pages;

namespace TrashCat.Tests.Infrastructure;

public abstract class BaseAltFixture
{
    protected AltDriver AltDriver = null!;
    protected MainMenuPage MainMenu = null!;
    protected GamePlayPage GamePlay = null!;
    protected PauseOverlayPage PauseOverlay = null!;

    public static FrameVideoRecorder? ActiveRecorder { get; private set; }
    public static RunEventLog? ActiveLog { get; private set; }

    private FrameVideoRecorder? _recorder;
    private RunEventLog? _log;
    private string? _runVideoPath;
    private string? _runContactSheetPath;

    [OneTimeSetUp]
    public void ConnectToGame()
    {
        Directory.CreateDirectory(TestConfig.ReportsRoot);
        AltDriver = new AltDriver(host: TestConfig.Host, port: TestConfig.Port);

        var framesDir = Path.Combine(TestConfig.ReportsRoot, "frames", TestConfig.RunId);
        _runVideoPath = Path.Combine(TestConfig.ReportsRoot, "videos", $"run-{TestConfig.RunId}.mp4");
        _runContactSheetPath = Path.Combine(TestConfig.ReportsRoot, "videos", $"run-{TestConfig.RunId}-contact.png");
        var logPath = Path.Combine(TestConfig.ReportsRoot, "logs", $"run-{TestConfig.RunId}.jsonl");

        _recorder = new FrameVideoRecorder(AltDriver, framesDir, TestConfig.RecordFrameMs);
        _recorder.Start();
        ActiveRecorder = _recorder;

        _log = new RunEventLog(logPath);
        ActiveLog = _log;
        _log.Log("run.start", new
        {
            runId = TestConfig.RunId,
            host = TestConfig.Host,
            port = TestConfig.Port,
            framesDir,
            videoPath = _runVideoPath,
            contactSheetPath = _runContactSheetPath,
        });

        MainMenu = new MainMenuPage(AltDriver);
        GamePlay = new GamePlayPage(AltDriver);
        PauseOverlay = new PauseOverlayPage(AltDriver);
    }

    [OneTimeTearDown]
    public async Task DisconnectFromGame()
    {
        _log?.Log("run.end.begin", new { framesCaptured = _recorder?.NextFrameIndex ?? 0 });

        if (_recorder != null && _runVideoPath != null)
        {
            var video = await _recorder.StopAndEncodeAsync(_runVideoPath);
            if (video != null && File.Exists(video))
            {
                AllureApi.AddAttachment("run-recording.mp4", "video/mp4", video);
                File.Copy(video, Path.Combine(TestConfig.ReportsRoot, "videos", "latest.mp4"), overwrite: true);
                _log?.Log("run.video.saved", new { path = video });
            }
            else
            {
                _log?.Log("run.video.skipped");
            }

            if (_runContactSheetPath != null)
            {
                var sheet = _recorder.GenerateContactSheet(_runContactSheetPath);
                if (sheet != null && File.Exists(sheet))
                {
                    AllureApi.AddAttachment("run-contact-sheet.png", "image/png", sheet);
                    File.Copy(sheet, Path.Combine(TestConfig.ReportsRoot, "videos", "latest-contact.png"), overwrite: true);
                    _log?.Log("run.contactSheet.saved", new { path = sheet });
                }
                else
                {
                    _log?.Log("run.contactSheet.skipped");
                }
            }
        }

        if (_log != null && File.Exists(_log.Path))
        {
            AllureApi.AddAttachment("run-events.jsonl", "application/x-ndjson", _log.Path);
        }

        // Print review-friendly paths to the test output so both the human and any AI
        // reviewer can locate the artifacts without guessing.
        var progress = TestContext.Progress;
        progress.WriteLine("[review] Artifacts for this run:");
        if (_log != null)          progress.WriteLine($"[review]   events log:    {_log.Path}");
        if (_runVideoPath != null) progress.WriteLine($"[review]   video (mp4):   {_runVideoPath}");
        if (_runContactSheetPath != null) progress.WriteLine($"[review]   contact sheet: {_runContactSheetPath}");
        if (_recorder != null)     progress.WriteLine($"[review]   frames dir:    {_recorder.FramesDir}");

        ActiveRecorder = null;
        ActiveLog = null;
        AltDriver?.Stop();
        await Task.Delay(500);
    }

    [SetUp]
    public void LogTestStart()
    {
        _log?.Log("test.begin", new
        {
            name = TestContext.CurrentContext.Test.Name,
            fullName = TestContext.CurrentContext.Test.FullName,
        });
    }

    [TearDown]
    public void CaptureFailureScreenshot()
    {
        var ctx = TestContext.CurrentContext;

        _log?.Log("test.end", new
        {
            name = ctx.Test.Name,
            outcome = ctx.Result.Outcome.Status.ToString(),
            message = ctx.Result.Message,
        });

        if (ctx.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            return;
        }

        try
        {
            var shotsDir = Path.Combine(TestConfig.ReportsRoot, "screenshots");
            Directory.CreateDirectory(shotsDir);
            var path = Path.Combine(shotsDir, $"{ctx.Test.Name}-fail.png");
            AltDriver.GetPNGScreenshot(path);
            AllureApi.AddAttachment("failure-screenshot.png", "image/png", path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[screenshot] {ex.Message}");
        }
    }

    protected void GoToGameplayFromMenu()
    {
        _log?.Log("menu.loadScene");
        MainMenu.LoadMainScene();
        Assert.That(MainMenu.IsDisplayed(), Is.True, "Main menu should be visible.");

        _log?.Log("menu.skipTutorial");
        SkipTutorialForRun();

        _log?.Log("menu.startRun");
        MainMenu.StartRun();
        AltDriver.WaitForObjectNotBePresent(By.NAME, "StartButton", timeout: 10);
        Assert.That(GamePlay.IsDisplayed(), Is.True, "Gameplay HUD should be visible.");
        _log?.Log("gameplay.entered");
    }

    private void SkipTutorialForRun()
    {
        // The tutorial's SLIDE SIDEWAY / SLIDE UP TO JUMP / SLIDE DOWN prompts freeze the run
        // on a modal overlay that hides the pause button. In normal mode the pause button is
        // always available and the cat still has to dodge obstacles to survive, which is what
        // the test is actually checking.
        //
        // AltTester's SetStaticProperty has a bug: when the property path goes through a
        // read-only property (PlayerData.instance), the internal write-back step casts a
        // PropertyInfo to FieldInfo and throws "Specified cast is not valid". Going through
        // the backing field m_Instance instead uses the field branch, which works.
        AltDriver.SetStaticProperty(
            "PlayerData",
            "m_Instance.tutorialDone",
            "UnityTechnologies.EndlessRunner",
            true);

        var applied = AltDriver.GetStaticProperty<bool>(
            "PlayerData",
            "m_Instance.tutorialDone",
            "UnityTechnologies.EndlessRunner");
        Assert.That(applied, Is.True, "PlayerData.tutorialDone must be true so the run skips the modal tutorial prompts.");
    }
}
