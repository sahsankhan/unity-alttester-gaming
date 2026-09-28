namespace TrashCat.Tests.pages;

public class MainMenuPage : BasePage
{
    public MainMenuPage(AltDriver driver) : base(driver)
    {
    }

    public AltObject RunButton => Driver.WaitForObject(By.NAME, "StartButton", timeout: 15);

    public void LoadMainScene()
    {
        Driver.LoadScene("Main");
    }

    public bool IsDisplayed()
    {
        try
        {
            _ = Driver.WaitForObject(By.NAME, "StoreButton", timeout: 5);
            _ = Driver.WaitForObject(By.NAME, "MissionButton", timeout: 5);
            _ = RunButton;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StartRun()
    {
        RunButton.Tap();
    }
}
