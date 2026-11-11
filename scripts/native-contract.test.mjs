// File: native-contract.test.mjs
// Purpose: Preserve real clean-source native packaging, original core/browser gates and actual Windows client extraction.
// Variables: checked-in workflow/source text and independent negative mutations; no native acceptance result is mocked.
// Author: Jason Stys. This checks the reviewed recipe, not arbitrary untrusted YAML/PowerShell.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";

const read = (name) => readFileSync(new URL("../" + name, import.meta.url), "utf8").replaceAll("\r\n", "\n");
const native = read(".github/workflows/native-app.yml"), codeql = read(".github/workflows/codeql.yml"), ci = read(".github/workflows/ci.yml"), packaging = read("scripts/package-native.ps1");

function requireNative(source) {
  for (const value of ["pull_request:", "branches: [main]", "contents: read", "runs-on: windows-latest", "timeout-minutes: 20", "dotnet-version: 10.0.404", './scripts/package-native.ps1 -OutputRoot "$PWD/artifacts/native-ci"', "artifacts/native-ci/acceptance/phase-*.json", "artifacts/native-ci/acceptance/native-*.png", "github.event_name == 'workflow_dispatch' && github.ref == 'refs/heads/main' && inputs.retain_package", "retention-days: 1"]) assert.ok(source.includes(value), `Missing actual native contract: ${value}`);
  assert.doesNotMatch(source, /contents: write|--allow-dirty|continue-on-error|\|\|\s*true|gh release|force-kill/i);
  const evidence = source.split("name: Retain bounded receipts and actual app screenshots")[1].split("name: Retain full unsigned package")[0];
  assert.doesNotMatch(evidence, /\.zip|path: artifacts\s*\n/);
}

function requirePackage(source) {
  for (const value of ["CI cannot label dirty sources as accepted.", "Clean source required; explicitly label local candidates.", "CI checkout identity mismatch.", "'10.0.404'", "'10.0.15'", "--locked-mode --configfile", "DebugType=None", "RuntimeFrameworkVersion", "System.Private.CoreLib.dll", "System.Windows.Forms.dll", "Published runtime bytes differ from the exact restored vendor package.", "THIRD-PARTY-NOTICES.TXT", "windowsdesktop-LICENSE.txt", "FileMode]::CreateNew", "$total -gt 1073741824", "$count -ne $entry.Length", "foreach ($phase in @(1,2))", "WaitForExit(90000)", "windowVisible", "Actual native receipt contract failed."]) assert.ok(source.includes(value), `Missing real package boundary: ${value}`);
  assert.doesNotMatch(source, /Stop-Process|\.Kill\(|Remove-Item|gh release|--clobber|--allow-unsigned/i);
  assert.ok(source.indexOf("source = (& git") < source.indexOf("New-Item -ItemType Directory -Path $output"));
  assert.ok(source.indexOf("Compress-Archive") < source.indexOf("$zip ="));
  assert.ok(source.indexOf("Extracted native byte mismatch") < source.indexOf("$executable ="));
}

function requireScans(source) {
  const original = source.split("  analyze:\n")[1]?.split("  analyze-designer:\n")[0];
  assert.ok(original?.includes("name: Analyze C#") && original.includes("runs-on: ubuntu-latest") && original.includes("dotnet build UnityTacticalAI.slnx -c Release --no-restore"));
  const client = source.split("  analyze-designer:\n")[1];
  for (const value of ["runs-on: windows-latest", "languages: csharp", "build-mode: manual", "queries: security-extended", "dotnet restore src/TacticalAI.Designer/TacticalAI.Designer.csproj --locked-mode --configfile scripts/NuGet.Native.Config", "dotnet build src/TacticalAI.Designer/TacticalAI.Designer.csproj -c Release --no-restore", "category: /language:csharp/os:windows/designer"]) assert.ok(client?.includes(value));
  assert.doesNotMatch(source, /pull_request_target|paths-ignore|continue-on-error/);
}

test("actual Windows package and bounded default artifact contracts", () => requireNative(native));
test("actual packaging keeps patched vendor bytes, source, safe extraction and two real native journeys", () => requirePackage(packaging));
test("original Ubuntu CodeQL identity and actual additional Windows C# client extraction remain", () => requireScans(codeql));
test("all original source/tournament and browser assertions remain mandatory", () => {
  assert.ok(ci.includes("run: node scripts/verify.mjs") && ci.includes("os: [ubuntu-latest, windows-latest]"));
  assert.ok(ci.includes("options: --user 1001") && ci.includes("npm run test:e2e") && ci.includes("node scripts/check-browser-host.mjs chromium firefox webkit"));
  assert.doesNotMatch(ci, /install --with-deps|SKIP_VALIDATE_HOST_REQUIREMENTS|continue-on-error/);
  const original = read("scripts/verify.mjs");
  for (const file of ["balance-report.json", "balance-report.csv", "sample-replay.json"]) assert.ok(original.includes(`compare("${file}")`));
  assert.ok(original.includes('"500"'));
  const project = read("src/TacticalAI.Designer/TacticalAI.Designer.csproj");
  assert.ok(project.includes("<RuntimeFrameworkVersion>10.0.15</RuntimeFrameworkVersion>") && project.includes("<SelfContained>true</SelfContained>"));
  const config = read("scripts/NuGet.Native.Config");
  assert.ok(config.includes('key="signatureValidationMode" value="require"') && config.includes('allowUntrustedRoot="false"'));
  assert.doesNotMatch(config, /allowUntrustedRoot="true"/);
});
for (const [name, source, before, after, check] of [
  ["automatic full-package retention", native, "github.event_name == 'workflow_dispatch' && github.ref == 'refs/heads/main' && inputs.retain_package", "always()", requireNative],
  ["source override in CI", native, '-OutputRoot "$PWD/artifacts/native-ci"', '-OutputRoot "$PWD/artifacts/native-ci" --allow-dirty', requireNative],
  ["unpatched build SDK", native, "dotnet-version: 10.0.404", "dotnet-version: 10.0.401", requireNative],
  ["missing extracted native receipts", native, "artifacts/native-ci/acceptance/phase-*.json", "artifacts/guessed-success.json", requireNative],
  ["omitted second actual process", packaging, "foreach ($phase in @(1,2))", "foreach ($phase in @(1))", requirePackage],
  ["native deadline bypass", packaging, "WaitForExit(90000)", "WaitForExit()", requirePackage],
  ["missing actual vendor-byte verification", packaging, "Published runtime bytes differ from the exact restored vendor package.", "skipped", requirePackage],
  ["missing native-client extraction", codeql, "dotnet build src/TacticalAI.Designer/TacticalAI.Designer.csproj -c Release --no-restore", "echo source inspected", requireScans],
  ["lost original analysis identity", codeql, "  analyze:\n", "  replacement:\n", requireScans],
]) test(`reject ${name}`, () => { assert.notEqual(source.replace(before, after), source); assert.throws(() => check(source.replace(before, after))); });
