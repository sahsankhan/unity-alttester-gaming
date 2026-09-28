# TrashCat build (instrumented)



Place an **AltTester-instrumented** Windows standalone build of Unity’s [Endless Runner Sample (TrashCat)](https://assetstore.unity.com/packages/templates/tutorials/endless-runner-sample-game-87901) here:



```text

App/TrashCatWindows/TrashCat.exe

```



## Instrument (one-time)



1. Import the Endless Runner sample in Unity.

2. Add **AltTester Unity SDK** — download from [AltTester Downloads](https://alttester.com/downloads/) (Unity SDK section, not Desktop). Import the `.unitypackage` into the project.

3. **AltTester → AltTester Editor** — port **13000**, scenes for build, output folder = this repo’s `App/TrashCatWindows/` → **Build Only**.

4. Use a **Development Build** so AltTester stays enabled (`Run Only In Debug Mode` on the prefab).

   If you build from **Build Profiles** instead of **AltTester → Build Only**, also ensure:
   - **Scripting define** `ALTTESTER` is set for **Standalone** (Player Settings).
   - `Assets/Resources/AltTester/AltTesterInputAxisData.json` exists (AltTester Editor creates this on instrumented builds; an empty `[]` file is OK for smoke tests).
   - After changing AltTester scripts, **rebuild** into this repo’s `App/TrashCatWindows/` folder.

5. **AltTester Desktop** must be **running** on your PC when you run tests (SDK 2.x). The Desktop app hosts port **13000**; the game and `AltDriver` connect to it.

The test project does **not** ship the game binary (Unity license + size). After you drop the exe, set `GAME_EXE` in `.env`, start AltTester Desktop, then run `npm test` from the repo root.

