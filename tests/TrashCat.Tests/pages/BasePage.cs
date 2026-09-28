namespace TrashCat.Tests.pages;

public abstract class BasePage
{
    protected BasePage(AltDriver driver)
    {
        Driver = driver;
    }

    protected AltDriver Driver { get; }
}
