// File: browser-review.mjs
// Purpose: Prepare isolated actual CLI exports and run real browser review journeys without a service.
// Variables: unique directory preserves all previous artifacts; child processes share only its path.
// Author: Jason Stys. Functions: run scopes checked subprocesses.
import { randomUUID } from "node:crypto";
import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
const root = path.resolve(import.meta.dirname, "..");
const directory = path.join(root, "artifacts", `browser-${randomUUID()}`);
fs.mkdirSync(directory, { recursive: true });
function run(command, args, expected = 0) {
  const result = spawnSync(command, args, {
    cwd: root,
    stdio: "inherit",
    env: { ...process.env, GAME_REPLAY_TEST_DIRECTORY: directory },
  });
  if (result.status !== expected)
    throw new Error(`${command} exited ${result.status}; expected ${expected}`);
}
const headless = [
  "run",
  "--project",
  "src/TacticalAI.Headless",
  "-c",
  "Release",
  "--no-build",
  "--",
];
run("dotnet", [
  ...headless,
  "simulate",
  "--matches",
  "1",
  "--seed",
  "42",
  "--out",
  directory,
]);
const replayPath = path.join(directory, "sample-replay.json");
const htmlPath = path.join(directory, "review.html");
run("dotnet", [...headless, "inspect-replay", replayPath, "--out", htmlPath]);
const before = fs.readFileSync(htmlPath);
run(
  "dotnet",
  [...headless, "inspect-replay", replayPath, "--out", htmlPath],
  2,
);
if (!before.equals(fs.readFileSync(htmlPath)))
  throw new Error("Existing review was overwritten");
const replay = JSON.parse(fs.readFileSync(replayPath, "utf8"));
replay.steps = [];
replay.initialState.units[0].id = "</script><img src=x onerror=alert(1)>";
const hostile = path.join(directory, "hostile.json");
fs.writeFileSync(hostile, JSON.stringify(replay));
run("dotnet", [
  ...headless,
  "inspect-replay",
  hostile,
  "--out",
  path.join(directory, "hostile.html"),
]);
run(process.execPath, ["node_modules/@playwright/test/cli.js", "test"]);
