using Allure.Net.Commons;
using Allure.NUnit;
using TrashCat.Tests.Infrastructure;

namespace TrashCat.Tests.tests;

[AllureNUnit]
[Category("smoke")]
public class FullJourneySmokeTests : BaseAltFixture
{
    [Test]
    [Description("Menu → run → dodge obstacles → pause → main menu (TrashCat / AltTester)")]
    public void Smoke_MenuRunPauseAndReturn()
    {
        AllureApi.Step("Start endless run from main menu", () => GoToGameplayFromMenu());

        AllureApi.Step("Run and dodge obstacles like a player (must Jump, Slide, go Left and Right)", () =>
        {
            Assert.That(GamePlay.CurrentLife, Is.EqualTo(3));
            var start = GamePlay.WorldPosition;

            // Keep playing until the cat has visibly performed every movement (Up jump, Down
            // slide, Left lane change, Right lane change), up to a 60 second ceiling.
            var sawEveryMove = GamePlay.PlayUntilAllMovementsSeen(60_000);
            TestContext.Progress.WriteLine($"[gameplay] movements: {GamePlay.MovementSummary}");
            BaseAltFixture.ActiveLog?.Log("gameplay.summary", new
            {
                sawEveryMove,
                GamePlay.JumpCount,
                GamePlay.SlideCount,
                GamePlay.LaneLeftCount,
                GamePlay.LaneRightCount,
                GamePlay.CurrentLife,
            });

            Assert.That(GamePlay.WorldPosition, Is.Not.EqualTo(start), "Player should move during gameplay.");
            Assert.That(GamePlay.CurrentLife, Is.GreaterThan(0), "Player should still be alive.");

            Assert.That(GamePlay.JumpCount, Is.GreaterThan(0),
                $"Cat should have jumped at least once over a low barrier. Movements: {GamePlay.MovementSummary}");
            Assert.That(GamePlay.SlideCount, Is.GreaterThan(0),
                $"Cat should have slid at least once under a high barrier. Movements: {GamePlay.MovementSummary}");
            Assert.That(GamePlay.LaneLeftCount, Is.GreaterThan(0),
                $"Cat should have changed lane to the left at least once. Movements: {GamePlay.MovementSummary}");
            Assert.That(GamePlay.LaneRightCount, Is.GreaterThan(0),
                $"Cat should have changed lane to the right at least once. Movements: {GamePlay.MovementSummary}");
            Assert.That(sawEveryMove, Is.True,
                $"All four movements should have happened during gameplay. Movements: {GamePlay.MovementSummary}");
        });

        AllureApi.Step("Pause and return to menu", () =>
        {
            GamePlay.Pause();
            Assert.That(PauseOverlay.IsDisplayed(), Is.True, "Pause menu should show Resume and Exit.");
            PauseOverlay.ReturnToMainMenu();
            Assert.That(MainMenu.IsDisplayed(), Is.True, "Main menu should show after exit.");
        });
    }

    [Test]
    [Description("Menu → run → pause → resume → verify gameplay continues → exit (TrashCat / AltTester)")]
    public void PauseAndResume_KeepsGameplayRunning()
    {
        AllureApi.Step("Start endless run from main menu", () => GoToGameplayFromMenu());

        AltVector3 positionBeforePause = default;

        AllureApi.Step("Play briefly to build up some distance", () =>
        {
            Assert.That(GamePlay.CurrentLife, Is.EqualTo(3), "Cat should start with 3 lives.");
            var start = GamePlay.WorldPosition;
            GamePlay.PlayFor(4000);
            positionBeforePause = GamePlay.WorldPosition;
            Assert.That(positionBeforePause.z, Is.GreaterThan(start.z),
                "Cat should have advanced forward before we pause.");
            Assert.That(GamePlay.CurrentLife, Is.GreaterThan(0), "Cat should still be alive before pause.");
        });

        AllureApi.Step("Pause the run and verify the pause overlay is shown", () =>
        {
            GamePlay.Pause();
            Assert.That(PauseOverlay.IsDisplayed(), Is.True,
                "Pause overlay with Resume and Exit should appear after tapping the pause button.");
        });

        AllureApi.Step("Resume and verify the pause overlay is dismissed", () =>
        {
            PauseOverlay.Resume();
            Assert.That(PauseOverlay.IsDismissed(), Is.True,
                "Pause overlay should be gone after tapping Resume (including any short countdown).");
            Assert.That(GamePlay.IsDisplayed(), Is.True,
                "Gameplay HUD should be visible again after resume.");
        });

        AllureApi.Step("Play a bit more and verify gameplay actually resumed", () =>
        {
            GamePlay.PlayFor(3000);
            var positionAfterResume = GamePlay.WorldPosition;
            BaseAltFixture.ActiveLog?.Log("gameplay.resumeCheck", new
            {
                zBeforePause = positionBeforePause.z,
                zAfterResume = positionAfterResume.z,
                delta = positionAfterResume.z - positionBeforePause.z,
                life = GamePlay.CurrentLife,
            });

            Assert.That(positionAfterResume.z, Is.GreaterThan(positionBeforePause.z),
                $"Cat should keep advancing after Resume. Before pause z={positionBeforePause.z}, after resume z={positionAfterResume.z}.");
            Assert.That(GamePlay.CurrentLife, Is.GreaterThan(0),
                "Cat should still be alive after the resumed segment.");
        });

        AllureApi.Step("Exit to main menu so the driver is left on a known screen", () =>
        {
            GamePlay.Pause();
            Assert.That(PauseOverlay.IsDisplayed(), Is.True, "Pause overlay should reopen.");
            PauseOverlay.ReturnToMainMenu();
            Assert.That(MainMenu.IsDisplayed(), Is.True, "Main menu should show after exit.");
        });
    }
}
