// File: verify-repository.mjs
// Purpose: Enforce documentation, source-header, workflow pinning, and secret-hygiene policies.
// Functions: walk, fail, checkRequiredFiles, checkSourceHeaders, checkActions, and main.
// Variables: errors accumulates actionable policy violations before one deterministic exit.

import fs from "node:fs";
import path from "node:path";
import process from "node:process";

const root = path.resolve(import.meta.dirname, "..");
const errors = [];

function walk(directory) {
  if (!fs.existsSync(directory)) {
    return [];
  }

  return fs.readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    if (["bin", "obj", ".git", "artifacts", "Library"].includes(entry.name)) {
      return [];
    }

    const absolute = path.join(directory, entry.name);
    return entry.isDirectory() ? walk(absolute) : [absolute];
  });
}

function fail(message) {
  errors.push(message);
}

function checkRequiredFiles() {
  const required = [
    "README.md",
    "LICENSE",
    "SECURITY.md",
    "docs/ARCHITECTURE.md",
    "docs/TESTING.md",
    "docs/SECURITY.md",
    "docs/OPERATIONS.md",
    "docs/LIMITATIONS.md",
    "docs/RESEARCH.md",
    "docs/CODE_INDEX.md",
    "docs/adr/0001-utility-ai.md",
    "docs/reports/VALIDATION.md",
    "docs/reports/PERFORMANCE.md",
    "docs/reports/BALANCE.md",
    ".github/workflows/ci.yml",
    ".github/workflows/codeql.yml",
  ];
  for (const relative of required) {
    if (!fs.existsSync(path.join(root, relative))) {
      fail(`missing required file: ${relative}`);
    }
  }
}

function checkSourceHeaders(files) {
  for (const filePath of files.filter((value) => value.endsWith(".cs") || value.endsWith(".mjs"))) {
    const relative = path.relative(root, filePath).replaceAll("\\", "/");
    const firstLines = fs.readFileSync(filePath, "utf8").split(/\r?\n/u).slice(0, 8).join("\n");
    if (!firstLines.includes("File:") || !firstLines.includes("Purpose:") || !firstLines.includes("Variables:")) {
      fail(`incomplete source header: ${relative}`);
    }
  }
}

function checkActions(files) {
  const workflows = files.filter((value) => value.includes(`${path.sep}.github${path.sep}workflows${path.sep}`) && /\.ya?ml$/u.test(value));
  const actionReference = /^\s*uses:\s*([^\s@]+)@([^\s#]+)/gmu;
  for (const workflow of workflows) {
    const text = fs.readFileSync(workflow, "utf8");
    for (const match of text.matchAll(actionReference)) {
      if (!/^[0-9a-f]{40}$/u.test(match[2])) {
        fail(`action is not pinned to a full commit SHA: ${match[0].trim()}`);
      }
    }
  }
}

function checkContent(files) {
  const textFiles = files.filter((value) => /\.(?:cs|mjs|md|json|ya?ml|xml|props|csproj|slnx|txt)$/u.test(value));
  const forbiddenSecret = /(?:ghp_[A-Za-z0-9]{20,}|BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY)/u;
  const forbiddenReference = new RegExp(["Opt", "o 22"].join(""), "iu");
  for (const filePath of textFiles) {
    const text = fs.readFileSync(filePath, "utf8");
    const relative = path.relative(root, filePath).replaceAll("\\", "/");
    if (forbiddenSecret.test(text)) {
      fail(`possible credential material in ${relative}`);
    }

    if (forbiddenReference.test(text)) {
      fail(`forbidden employer reference in ${relative}`);
    }
  }
}

function main() {
  const files = walk(root);
  checkRequiredFiles();
  checkSourceHeaders(files);
  checkActions(files);
  checkContent(files);
  if (errors.length > 0) {
    for (const error of errors.sort()) {
      console.error(`ERROR ${error}`);
    }

    process.exitCode = 1;
    return;
  }

  console.log(`repository policy passed (${files.length} files inspected)`);
}

main();
