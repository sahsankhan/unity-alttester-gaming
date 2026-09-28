# Unity AltTester gaming automation

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [AltTester Desktop](https://alttester.com/alttester/) — **must stay open** while tests run (AltTester 2.x hosts the server on port **13000**)
- **Instrumented TrashCat Windows build** — see [App/README.md](App/README.md)
- **ffmpeg** on `PATH` — encodes frame captures to MP4 (optional but recommended for video)
- Node.js 18+ — wrapper scripts and HTML report bundle

## Setup

```powershell
cd unity-alttester-gaming
copy .env.example .env
# Edit GAME_EXE after you place TrashCat.exe under App/TrashCatWindows/

dotnet restore tests/TrashCat.Tests/TrashCat.Tests.csproj
dotnet build tests/TrashCat.Tests/TrashCat.Tests.csproj
```

## First test run (checklist)

You need an **AltTester-instrumented** TrashCat build before automation can connect. This repo does not include the `.exe`.

### 1. Build the game (one-time)

1. Install [Unity Hub](https://unity.com/download) and a **2021.3+** or **2022 LTS** editor (match the Endless Runner sample requirements).
2. In Unity Hub → **Add** → import [Endless Runner Sample (TrashCat)](https://assetstore.unity.com/packages/templates/tutorials/endless-runner-sample-game-87901).
3. Download **AltTester Unity SDK** from [AltTester Downloads](https://alttester.com/downloads/) (not `/tools/sdk` — that URL is dead). Import into Unity; port **13000** in **AltTester → AltTester Editor**.
4. **File → Build Settings** → **Windows x64** → output folder:

   `unity-alttester-gaming/App/TrashCatWindows/`

   Build so `TrashCat.exe` lives at `App/TrashCatWindows/TrashCat.exe`.

### 2. Sanity-check the build (manual)

1. Open **AltTester Desktop** (server on `127.0.0.1:13000`).
2. Start your instrumented build (`EndlessRunnerSetup.exe` or `TrashCat.exe`).
3. In Desktop, connect to the game and confirm objects like `StartButton`, `StoreButton`, scene `Main`.

### 3. Run automation

```powershell
cd unity-alttester-gaming
# Keep AltTester Desktop open. .env points at your built exe under App/TrashCatWindows/

npm test
# or: npm run test:smoke
```

Preflight output in the script shows whether `GAME_EXE`, ffmpeg, and AltTester Desktop look OK.

### 4. Open results

- Summary + embedded video (if ffmpeg encoded): `reports/alttester-report/index.html`
- Allure raw results: `reports/allure-results/`
- Failure PNGs: `reports/screenshots/`

**Without ffmpeg:** install [FFmpeg](https://ffmpeg.org/) (e.g. `winget install Gyan.FFmpeg`) or set `RECORD_VIDEO=0` in `.env` — tests and Allure still run; you keep PNG frames under `reports/frames/`.

**Without Unity yet:** you cannot complete a green run until step 1 is done. You can still `dotnet build` the test project locally.

## Run

```powershell
npm test              # launch game (if configured) + smoke + reports
npm run test:smoke    # Category=smoke only
npm run report        # regenerate Allure HTML (needs Allure CLI)
npm run report:html   # lightweight reports/alttester-report/index.html
```

**Important:** With AltTester **2.0+**, keep **AltTester Desktop running** during `npm test`. The game and the NUnit driver both connect to Desktop on `ALT_DRIVER_HOST` / `ALT_DRIVER_PORT` (default `127.0.0.1:13000`).

## Recording

| Mode | Env | Output |
|------|-----|--------|
| **AltTester frames → MP4** (default) | `RECORD_VIDEO=1`, `RECORD_FRAME_MS=500` | `reports/videos/run-<id>.mp4`, `latest.mp4`, Allure attachment |
| **Desktop ffmpeg** (optional) | `FFMPEG_DESKTOP_CAPTURE=1` | `reports/videos/latest-desktop.mp4` |

AltTester has no built-in video API; this repo captures **PNG frames during the run** via `GetPNGScreenshot`, then **ffmpeg** builds the MP4.

## Smoke journey

1. Main menu (`Main` scene, `StartButton`)
2. Start run → gameplay HUD (`pauseButton`, `PlayerPivot`)
3. Assert 3 lives, wait for player movement
4. Pause → exit to menu

Tests: `tests/TrashCat.Tests/tests/FullJourneySmokeTests.cs`

## Reports

| Output | Path |
|--------|------|
| Allure results | `reports/allure-results/` |
| Allure HTML | `reports/allure-report/index.html` |
| Summary + video | `reports/alttester-report/index.html` |
| Failure screenshots | `reports/screenshots/` |

## Layout

```text
unity-alttester-gaming/
├── App/                    # TrashCat.exe (you add)
├── scripts/                # run-tests.ps1, reports
├── tests/TrashCat.Tests/   # AltDriver, POM, smoke test
└── reports/                # generated (gitignored)
```

## CI

GitHub Actions workflow at [`.github/workflows/tests.yml`](.github/workflows/tests.yml).

Runs on every push to `main` (test-related paths only) and via manual **Run workflow** trigger with an optional NUnit category filter. It restores + builds the test project, runs the smoke suite, generates the Allure HTML report, and uploads three artifacts per run:

| Artifact | Contents |
|----------|----------|
| `allure-report-<runId>` | Browsable Allure HTML report — open `index.html` |
| `allure-results-<runId>` | Raw Allure JSONs (feed to `allure generate` locally or into an allure-history job later) |
| `run-artifacts-<runId>` | `reports/videos/**` (mp4 + contact-sheet PNG), `reports/logs/**` (per-run JSONL event log), `reports/screenshots/**` (failure PNGs) |

### Why a self-hosted Windows runner

The tests **launch the instrumented Unity Windows player** and talk to it through AltTester Desktop. That needs:

- Windows with a real interactive desktop session (Unity must actually render)
- The instrumented `TrashCat.exe` present on the machine
- **AltTester Desktop already open** with the WebSocket server on `127.0.0.1:13000`
- **The game process already running** and connected to AltTester Desktop

GitHub-hosted runners don't offer a persistent Unity+AltTester environment, so a **self-hosted Windows runner** is the practical choice for this kind of Unity automation.

### One-time runner setup

On the Windows box that will host the runner:

1. Install .NET 8 SDK, Node.js 18+, ffmpeg, and the Allure CLI (`scoop install allure` or `choco install allure-commandline`).
2. Place the instrumented `TrashCat.exe` under `App/TrashCatWindows/` **or** create a GitHub repository variable named `GAME_EXE` with the absolute path.
3. Install the GitHub Actions runner on the machine and register it with labels `self-hosted` **and** `windows`. Configure it as a service so it survives reboots.
4. Before every run: open AltTester Desktop and start the game so it connects. The workflow deliberately does **not** try to relaunch the exe (`LAUNCH_GAME=0`) — you keep manual control of the Unity session.

### Reporting

Reporting is [Allure](https://allurereport.org). It is already integrated in the tests (`[AllureNUnit]`, `AllureApi.Step`, `AllureApi.AddAttachment` for the run video, contact sheet, and JSONL log). CI regenerates the HTML report from the raw results and publishes it as a downloadable artifact — no extra tool required to view it, and no external service to host. If you later want trend history across runs, the `allure-results-<runId>` artifacts can be fed into an Allure history job.

## License note

TrashCat is Unity’s sample; ship **your instrumented build** or document build steps — do not commit Unity project/binaries unless your license allows.
