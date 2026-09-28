using TrashCat.Tests.Infrastructure;

namespace TrashCat.Tests.pages;

public class PauseOverlayPage : BasePage
{
    public PauseOverlayPage(AltDriver driver) : base(driver)
    {
    }

    public AltObject MainMenuButton => Driver.WaitForObject(By.NAME, "Exit", timeout: 5);
    public AltObject ResumeButton => Driver.WaitForObject(By.NAME, "Resume", timeout: 5);

    public bool IsDisplayed()
    {
        try
        {
            _ = MainMenuButton;
            _ = Driver.WaitForObject(By.NAME, "Resume", timeout: 2);
            BaseAltFixture.ActiveLog?.Log("pause.displayed", null, BaseAltFixture.ActiveRecorder?.LastFrameIndex);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsDismissed(int timeoutSeconds = 5)
    {
        try
        {
            Driver.WaitForObjectNotBePresent(By.NAME, "Resume", timeout: timeoutSeconds);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ReturnToMainMenu()
    {
        BaseAltFixture.ActiveLog?.Log("pause.exitToMenu", null, BaseAltFixture.ActiveRecorder?.LastFrameIndex);
        MainMenuButton.Tap();
    }

    public void Resume()
    {
        BaseAltFixture.ActiveLog?.Log("pause.resumeTap", null, BaseAltFixture.ActiveRecorder?.LastFrameIndex);
        ResumeButton.Tap();
        // Wait for the overlay to actually go away. In Trash Cat this also covers the short
        // "3, 2, 1, GO" countdown that plays before control returns to the player.
        try
        {
            Driver.WaitForObjectNotBePresent(By.NAME, "Resume", timeout: 10);
        }
        catch
        {
            // Even if the overlay stayed, let the caller assert on IsDismissed() for a clearer failure.
        }
        BaseAltFixture.ActiveLog?.Log("pause.resumed", null, BaseAltFixture.ActiveRecorder?.LastFrameIndex);
    }
}
