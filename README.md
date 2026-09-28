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

| When | What runs | Where |
|------|-----------|--------|
| Push to `main` (test-related paths) | Restore and build the test project | GitHub-hosted `windows-latest` |
| **Actions → Run workflow** | That same build, then the AltTester smoke suite | Build on GitHub, smoke on your Windows runner |

A normal push does not wait for your PC. The game job runs only when you start it by hand, and only after the build job is green.

The smoke job starts AltTester Desktop in batch mode, launches the instrumented game, runs the tests, then closes the game and the server **it** started. If something is already listening on `127.0.0.1:13000`, that process is left alone. An optional NUnit category filter is on the Run workflow form.

Artifacts from the smoke job:

| Artifact | Contents |
|----------|----------|
| `allure-report-<runId>` | Browsable Allure HTML report — open `index.html` |
| `allure-results-<runId>` | Raw Allure JSONs (feed to `allure generate` locally or into an allure-history job later) |
| `run-artifacts-<runId>` | `reports/videos/**` (mp4 + contact-sheet PNG), `reports/logs/**` (per-run JSONL event log), `reports/screenshots/**` (failure PNGs) |

### One-time runner setup

The smoke job needs one Windows PC that stays online, with a real desktop session so the game can draw. GitHub's cloud machines cannot do that.

1. Install .NET 8 SDK, Node.js 18+, ffmpeg, and the Allure CLI (`scoop install allure` or `choco install allure-commandline`).
2. Install [AltTester Desktop](https://alttester.com/alttester/). The workflow looks for `C:\Program Files\AltTesterDesktop\AltTesterDesktop.exe`. If yours is somewhere else, add a repository variable named `ALTTESTER_DESKTOP_EXE` with the full path.
3. If batch mode asks for a license, add a repository secret named `ALTTESTER_LICENSE`.
4. Place the instrumented `TrashCat.exe` under `App/TrashCatWindows/` **or** create a repository variable named `GAME_EXE` with the absolute path.
5. Repo → **Settings** → **Actions** → **Runners** → **New self-hosted runner**. Register it with labels `self-hosted` and `windows`, then leave the runner program running in the **logged-in desktop**. A runner that exists only as a Windows service cannot show the game window.
6. **Actions → Run workflow** when you want the smoke tests. If that runner program is not running, this job will sit on "Waiting for a runner" again.

### Reporting

Reporting is [Allure](https://allurereport.org). It is already integrated in the tests (`[AllureNUnit]`, `AllureApi.Step`, `AllureApi.AddAttachment` for the run video, contact sheet, and JSONL log). CI regenerates the HTML report from the raw results and publishes it as a downloadable artifact — no extra tool required to view it, and no external service to host. If you later want trend history across runs, the `allure-results-<runId>` artifacts can be fed into an Allure history job.

## License note

TrashCat is Unity’s sample; ship **your instrumented build** or document build steps — do not commit Unity project/binaries unless your license allows.
