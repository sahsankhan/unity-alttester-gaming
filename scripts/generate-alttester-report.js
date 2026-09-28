#!/usr/bin/env node
/**
 * Lightweight HTML bundle: test video + links to Allure (if present).
 */
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const reports = path.join(root, process.env.REPORT_ROOT || "reports");
const bundle = path.join(reports, "alttester-report");
const assets = path.join(bundle, "assets");

function latestMp4(dir, name) {
  const direct = path.join(dir, name);
  if (fs.existsSync(direct)) return direct;
  if (!fs.existsSync(dir)) return null;
  const files = fs
    .readdirSync(dir)
    .filter((f) => f.endsWith(".mp4"))
    .map((f) => ({ f, m: fs.statSync(path.join(dir, f)).mtimeMs }))
    .sort((a, b) => b.m - a.m);
  return files[0] ? path.join(dir, files[0].f) : null;
}

function copyIfExists(src, destName) {
  if (!src || !fs.existsSync(src)) return null;
  fs.mkdirSync(assets, { recursive: true });
  const dest = path.join(assets, destName);
  fs.copyFileSync(src, dest);
  return `assets/${destName}`;
}

const runVideo = latestMp4(path.join(reports, "videos"), "latest.mp4");
const desktopVideo = latestMp4(path.join(reports, "videos"), "latest-desktop.mp4");
const runRel = copyIfExists(runVideo, "run-recording.mp4");
const desktopRel = copyIfExists(desktopVideo, "desktop-recording.mp4");

const allureIndex = path.join(reports, "allure-report", "index.html");
const hasAllure = fs.existsSync(allureIndex);

const html = `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <title>Unity AltTester run</title>
  <style>
    body { font-family: system-ui, sans-serif; margin: 2rem; background: #0f1419; color: #e7ecf3; }
    h1 { font-size: 1.35rem; }
    video { max-width: 100%; border-radius: 8px; background: #000; margin: 1rem 0; }
    a { color: #6cb6ff; }
    .card { background: #1a2332; padding: 1rem 1.25rem; border-radius: 8px; margin: 1rem 0; }
  </style>
</head>
<body>
  <h1>Unity TrashCat — AltTester run</h1>
  <p>Frame-based recording from AltTester screenshots (encoded with ffmpeg when available).</p>
  <div class="card">
    <h2>Run recording</h2>
    ${
      runRel
        ? `<video controls preload="metadata" src="${runRel}"></video>`
        : "<p>No run-recording.mp4 yet. Run tests with RECORD_VIDEO=1 and ffmpeg on PATH.</p>"
    }
  </div>
  ${
    desktopRel
      ? `<div class="card"><h2>Desktop capture (optional)</h2><video controls preload="metadata" src="${desktopRel}"></video></div>`
      : ""
  }
  <div class="card">
    <h2>Allure</h2>
    ${
      hasAllure
        ? `<p><a href="../allure-report/index.html">Open Allure report</a> (steps, failure screenshots, video attachment).</p>`
        : "<p>Install Allure CLI and run <code>npm run report</code> after tests.</p>"
    }
  </div>
</body>
</html>`;

fs.mkdirSync(bundle, { recursive: true });
fs.writeFileSync(path.join(bundle, "index.html"), html, "utf8");
console.log(`Report bundle: ${path.join(bundle, "index.html")}`);
