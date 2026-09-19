// File: verify.mjs
// Purpose: Run the same complete, cross-platform validation gate locally and in GitHub Actions.
// Functions: run, compare, and main.
// Variables: root, artifacts, and temporary report paths scope all generated validation output.

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = path.resolve(import.meta.dirname, "..");
const artifacts = path.join(root, "artifacts");
const generated = path.join(root, "docs", "reports", "generated");
const simulation = path.join(artifacts, "simulation");

function run(command, arguments_) {
  console.log(`> ${command} ${arguments_.join(" ")}`);
  const result = spawnSync(command, arguments_, { cwd: root, encoding: "utf8" });
  process.stdout.write(result.stdout ?? "");
  process.stderr.write(result.stderr ?? "");
  if (result.status !== 0) {
    throw new Error(`${command} exited with status ${result.status}`);
  }
}

function compare(fileName) {
  const expected = fs.readFileSync(path.join(generated, fileName));
  const actual = fs.readFileSync(path.join(simulation, fileName));
  if (!expected.equals(actual)) {
    throw new Error(`${fileName} differs from checked-in deterministic evidence.`);
  }
}

function main() {
  fs.rmSync(artifacts, { recursive: true, force: true });
  fs.mkdirSync(artifacts, { recursive: true });
  run("dotnet", ["restore", "UnityTacticalAI.slnx", "--locked-mode", "--configfile", "NuGet.Config"]);
  run("dotnet", ["format", "UnityTacticalAI.slnx", "--no-restore", "--verify-no-changes"]);
  run("dotnet", ["build", "UnityTacticalAI.slnx", "-c", "Release", "--no-restore"]);
  run("dotnet", ["run", "--project", "tests/TacticalAI.Tests/TacticalAI.Tests.csproj", "-c", "Release", "--no-build", "--", "--report", "artifacts/test-results.json"]);
  run("dotnet", ["run", "--project", "src/TacticalAI.Headless/TacticalAI.Headless.csproj", "-c", "Release", "--no-build", "--", "simulate", "--matches", "500", "--seed", "1000", "--out", "artifacts/simulation"]);
  compare("balance-report.json");
  compare("balance-report.csv");
  compare("sample-replay.json");
  run("dotnet", ["run", "--project", "src/TacticalAI.Headless/TacticalAI.Headless.csproj", "-c", "Release", "--no-build", "--", "replay", "artifacts/simulation/sample-replay.json"]);
  run("dotnet", ["run", "--project", "src/TacticalAI.Headless/TacticalAI.Headless.csproj", "-c", "Release", "--no-build", "--", "validate-save", "scenarios/duel-v1.json"]);
  run("node", ["scripts/generate-code-index.mjs", "--check"]);
  run("node", ["scripts/verify-repository.mjs"]);
  console.log("complete validation gate passed");
}

try {
  main();
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
}
