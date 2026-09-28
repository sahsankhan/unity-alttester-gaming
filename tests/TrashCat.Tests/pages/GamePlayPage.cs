using TrashCat.Tests.Infrastructure;

namespace TrashCat.Tests.pages;

public class GamePlayPage : BasePage
{
    private const string Assembly = "UnityTechnologies.EndlessRunner";
    private const float LaneWidth = 1.5f;

    // Trigger distances (metres ahead). Chosen for how fast the endless runner moves given
    // that our decision loop fires roughly every 100-200 ms.
    private const float JumpDistance = 7f;
    private const float LaneDistance = 11f;
    // Cooldown between two consecutive inputs. Short enough that back-to-back obstacles can
    // still be dodged, long enough that the animation actually plays.
    private const int JumpSlideCooldownMs = 250;
    private const int LaneCooldownMs = 180;
    // How close in x we consider an obstacle to be "in" a given lane centre.
    private const float LaneMatchTolerance = 0.75f;

    private int _lane = 1;
    private long _actionCooldownUntil;

    public int JumpCount { get; private set; }
    public int SlideCount { get; private set; }
    public int LaneLeftCount { get; private set; }
    public int LaneRightCount { get; private set; }

    public bool HasPerformedAllMovements =>
        JumpCount > 0 && SlideCount > 0 && LaneLeftCount > 0 && LaneRightCount > 0;

    public string MovementSummary =>
        $"Up(Jump)={JumpCount}, Down(Slide)={SlideCount}, Left={LaneLeftCount}, Right={LaneRightCount}";

    public GamePlayPage(AltDriver driver) : base(driver)
    {
    }

    public AltObject PauseButton => Driver.WaitForObject(By.NAME, "pauseButton", timeout: 10);
    public AltObject Player => Driver.WaitForObject(By.NAME, "PlayerPivot", timeout: 10);

    public bool IsDisplayed()
    {
        try
        {
            _ = PauseButton;
            _ = Player;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int CurrentLife =>
        Player.GetComponentProperty<int>("CharacterInputController", "currentLife", Assembly);

    public AltVector3 WorldPosition => Player.UpdateObject().GetWorldPosition();

    public void Pause()
    {
        BaseAltFixture.ActiveLog?.Log("gameplay.pauseTap", new
        {
            counters = new { JumpCount, SlideCount, LaneLeftCount, LaneRightCount },
        }, BaseAltFixture.ActiveRecorder?.LastFrameIndex);
        PauseButton.Tap();
    }

    /// <summary>
    /// Runs a normal (non-tutorial) game for a fixed duration. Every 100 ms it looks at the
    /// nearest obstacle in front of the cat and does the same move a player would: jump low
    /// barriers, slide under high barriers, and change lane for a trash bin or dog.
    /// </summary>
    public void PlayFor(int durationMs)
    {
        var deadline = Environment.TickCount64 + durationMs;
        while (Environment.TickCount64 < deadline)
        {
            BaseAltFixture.ActiveRecorder?.CaptureFrame();
            TryDodge();
            Thread.Sleep(100);
        }
    }

    /// <summary>
    /// Plays the game until the cat has visibly performed every movement (Up jump, Down slide,
    /// Left lane change and Right lane change) at least once, or the max duration elapses.
    /// Returns true if all four movements were seen.
    /// </summary>
    public bool PlayUntilAllMovementsSeen(int maxDurationMs)
    {
        BaseAltFixture.ActiveLog?.Log("gameplay.playUntilAllMoves.begin", new { maxDurationMs });
        var deadline = Environment.TickCount64 + maxDurationMs;
        while (Environment.TickCount64 < deadline)
        {
            BaseAltFixture.ActiveRecorder?.CaptureFrame();
            TryDodge();

            if (HasPerformedAllMovements)
            {
                BaseAltFixture.ActiveLog?.Log("gameplay.playUntilAllMoves.done", new
                {
                    reason = "all-moves-seen",
                    JumpCount, SlideCount, LaneLeftCount, LaneRightCount,
                });
                return true;
            }

            // Bail out early if the cat has died: no point looping to timeout.
            try
            {
                if (CurrentLife <= 0)
                {
                    BaseAltFixture.ActiveLog?.Log("gameplay.playUntilAllMoves.done", new
                    {
                        reason = "cat-died",
                        JumpCount, SlideCount, LaneLeftCount, LaneRightCount,
                    });
                    return HasPerformedAllMovements;
                }
            }
            catch
            {
                // Player object may not be resolvable for a frame; keep going.
            }

            Thread.Sleep(100);
        }

        BaseAltFixture.ActiveLog?.Log("gameplay.playUntilAllMoves.done", new
        {
            reason = "timeout",
            JumpCount, SlideCount, LaneLeftCount, LaneRightCount,
        });
        return HasPerformedAllMovements;
    }

    private void TryDodge()
    {
        if (Environment.TickCount64 < _actionCooldownUntil)
        {
            return;
        }

        var player = WorldPosition;
        var obstacles = FindObstacles(player);
        if (obstacles.Count == 0)
        {
            return;
        }

        var currentLaneX = LaneWidth * (_lane - 1);

        // Look at every obstacle within reaction range, not just the single nearest one.
        // A rat in the left lane at 4 m does not require action, but a bin in our lane at 6 m
        // does; the old "nearest only" logic missed exactly that case and let the cat crash.
        Obstacle? barrier = null;
        Obstacle? laneHazard = null;

        foreach (var obs in obstacles)   // already sorted nearest-first
        {
            if (barrier == null
                && (obs.Kind == ObstacleKind.Jump || obs.Kind == ObstacleKind.Slide)
                && obs.Ahead <= JumpDistance)
            {
                barrier = obs;
            }

            if (laneHazard == null
                && obs.Kind == ObstacleKind.Lane
                && obs.Ahead <= LaneDistance
                && Math.Abs(obs.X - currentLaneX) < LaneMatchTolerance)
            {
                laneHazard = obs;
            }

            if (barrier != null && laneHazard != null)
            {
                break;
            }
        }

        // Jump/slide barriers take priority - they block every lane, so no lane change helps.
        if (barrier is { } b)
        {
            if (b.Kind == ObstacleKind.Jump)
            {
                DoAction("Jump", AltKeyCode.UpArrow, b, player);
            }
            else
            {
                DoAction("Slide", AltKeyCode.DownArrow, b, player);
            }
            return;
        }

        if (laneHazard is { } h)
        {
            var direction = PickFreeLane(obstacles);
            if (direction != 0)
            {
                ChangeLaneNow(direction, h, player);
            }
        }
    }

    /// <summary>
    /// Picks a free adjacent lane. Uses absolute lane geometry (the character's own transform
    /// is a fixed pivot whose x is always ~0, so we cannot derive current-lane x from it).
    /// Lane 0 is at x = -LaneWidth, lane 1 at 0, lane 2 at +LaneWidth.
    /// </summary>
    private int PickFreeLane(List<Obstacle> obstacles)
    {
        var laneBlocked = new bool[3];
        foreach (var obs in obstacles)
        {
            if (obs.Kind != ObstacleKind.Lane || obs.Ahead > LaneDistance)
            {
                continue;
            }

            for (var i = 0; i < 3; i++)
            {
                var laneX = LaneWidth * (i - 1);
                if (Math.Abs(obs.X - laneX) < LaneMatchTolerance)
                {
                    laneBlocked[i] = true;
                }
            }
        }

        var leftLane = _lane - 1;
        var rightLane = _lane + 1;

        // Prefer whichever adjacent lane looks free.
        var canGoLeft  = leftLane  >= 0 && !laneBlocked[leftLane];
        var canGoRight = rightLane <= 2 && !laneBlocked[rightLane];

        if (canGoLeft && canGoRight)
        {
            // Both free: bias toward the direction we've used less so both counters grow.
            return LaneLeftCount <= LaneRightCount ? -1 : 1;
        }
        if (canGoLeft)  return -1;
        if (canGoRight) return 1;

        // Both adjacent lanes look blocked. As a last resort, still swap toward the middle
        // (or any legal direction) - staying put would guarantee a hit.
        if (leftLane  >= 0) return -1;
        if (rightLane <= 2) return 1;
        return 0;
    }

    private void DoAction(string method, AltKeyCode key, Obstacle obstacle, AltVector3 player)
    {
        Driver.PressKey(key, power: 1f, duration: 0.05f, wait: false);
        Invoke(method);

        if (method == "Jump")
        {
            JumpCount++;
        }
        else if (method == "Slide")
        {
            SlideCount++;
        }

        BaseAltFixture.ActiveLog?.Log("gameplay.move", new
        {
            move = method.ToLowerInvariant(),
            key = key.ToString(),
            obstacle = obstacle.Name,
            obstacleKind = obstacle.Kind.ToString(),
            ahead = obstacle.Ahead,
            playerX = player.x,
            playerZ = player.z,
            lane = _lane,
            counters = new { JumpCount, SlideCount, LaneLeftCount, LaneRightCount },
        }, BaseAltFixture.ActiveRecorder?.LastFrameIndex);

        _actionCooldownUntil = Environment.TickCount64 + JumpSlideCooldownMs;
    }

    private void ChangeLaneNow(int direction, Obstacle obstacle, AltVector3 player)
    {
        var key = direction < 0 ? AltKeyCode.LeftArrow : AltKeyCode.RightArrow;
        Driver.PressKey(key, power: 1f, duration: 0.05f, wait: false);
        Player.CallComponentMethod<string>(
            "CharacterInputController",
            "ChangeLane",
            Assembly,
            new object[] { direction },
            new[] { "System.Int32" });
        var previousLane = _lane;
        _lane = Math.Clamp(_lane + direction, 0, 2);

        if (direction < 0)
        {
            LaneLeftCount++;
        }
        else if (direction > 0)
        {
            LaneRightCount++;
        }

        BaseAltFixture.ActiveLog?.Log("gameplay.move", new
        {
            move = direction < 0 ? "left" : "right",
            key = key.ToString(),
            obstacle = obstacle.Name,
            obstacleKind = obstacle.Kind.ToString(),
            ahead = obstacle.Ahead,
            playerX = player.x,
            playerZ = player.z,
            fromLane = previousLane,
            toLane = _lane,
            counters = new { JumpCount, SlideCount, LaneLeftCount, LaneRightCount },
        }, BaseAltFixture.ActiveRecorder?.LastFrameIndex);

        _actionCooldownUntil = Environment.TickCount64 + LaneCooldownMs;
    }

    private List<Obstacle> FindObstacles(AltVector3 player)
    {
        var found = new List<Obstacle>();
        List<AltObject> raw;
        try
        {
            raw = Driver.FindObjectsWhichContain(By.NAME, "Obstacle");
        }
        catch
        {
            return found;
        }

        foreach (var obj in raw)
        {
            if (!obj.name.StartsWith("Obstacle", StringComparison.Ordinal))
            {
                continue;
            }

            var pos = obj.GetWorldPosition();
            var ahead = pos.z - player.z;
            if (ahead < 0.3f || ahead > 15f)
            {
                continue;
            }

            found.Add(new Obstacle(obj.name, KindFor(obj.name), pos.x, ahead));
        }

        found.Sort((a, b) => a.Ahead.CompareTo(b.Ahead));
        return found;
    }

    private static ObstacleKind KindFor(string name)
    {
        if (name.Contains("HighBarrier", StringComparison.Ordinal))
        {
            return ObstacleKind.Slide;
        }

        if (name.Contains("LowBarrier", StringComparison.Ordinal))
        {
            return ObstacleKind.Jump;
        }

        // Roadworks barriers and cones spawn 1-2 across the lanes, so at least one lane is open.
        return ObstacleKind.Lane;
    }

    private void Invoke(string method)
    {
        Player.CallComponentMethod<string>(
            "CharacterInputController",
            method,
            Assembly,
            Array.Empty<object>());
    }

    private readonly record struct Obstacle(string Name, ObstacleKind Kind, float X, float Ahead);

    private enum ObstacleKind
    {
        Lane,
        Jump,
        Slide
    }
}
