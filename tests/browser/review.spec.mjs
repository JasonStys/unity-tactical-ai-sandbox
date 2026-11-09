// File: review.spec.mjs
// Purpose: Test actual generated offline replay navigation, export, accessibility and hostile labels.
// Variables: fixture paths are per-invocation outputs, never source or user data.
import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
const directory = process.env.GAME_REPLAY_TEST_DIRECTORY;
test("designer inspects validated frames and downloads the exact review", async ({
  page,
}) => {
  const remote = [];
  page.on("request", (r) => {
    if (/^https?:/u.test(r.url())) remote.push(r.url());
  });
  await page.goto(pathToFileURL(path.join(directory, "review.html")).href);
  await expect(
    page.getByRole("heading", {
      name: "Fictional Game Replay Review",
      exact: true,
    }),
  ).toBeVisible();
  const data = await page
    .locator("#data")
    .evaluate((node) => JSON.parse(node.textContent));
  await expect(page.locator("#hash")).toContainText(data.initialHash);
  await page.locator("#frame").fill(String(data.frames.length - 1));
  await expect(page.locator("#hash")).toContainText(data.finalHash);
  await expect(page.locator("#next")).toHaveText(
    "Next recorded command (hindsight): none",
  );
  for (const unit of data.frames.at(-1).units)
    await expect(
      page.getByRole("row").filter({ hasText: unit.id }),
    ).toContainText(`[${unit.x},${unit.y}]`);
  await page
    .locator("#avatar")
    .selectOption(data.frames.at(-1).units.at(-1).id);
  await expect(page.locator("#units tr")).toHaveCount(
    data.frames.at(-1).units.length,
  );
  const download = page.waitForEvent("download");
  await page.getByRole("button", { name: "Download inspection JSON" }).click();
  expect(
    JSON.parse(await fs.readFile(await (await download).path(), "utf8")),
  ).toEqual(data);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
  expect(
    await page.evaluate(
      () => document.documentElement.scrollWidth <= innerWidth,
    ),
  ).toBe(true);
  expect(remote).toEqual([]);
});
test("hostile game identifiers remain text", async ({ page }) => {
  await page.goto(pathToFileURL(path.join(directory, "hostile.html")).href);
  await expect(page.locator("#units")).toContainText(
    "</script><img src=x onerror=alert(1)>",
  );
  await expect(page.locator("img")).toHaveCount(0);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
});

test("keyboard users can focus and scroll the actual narrow avatar table", async ({
  page,
}) => {
  await page.setViewportSize({ width: 240, height: 720 });
  await page.goto(pathToFileURL(path.join(directory, "hostile.html")).href);
  const region = page.getByRole("region", {
    name: "Scrollable avatar state table",
  });
  await page.locator("#avatar").focus();
  await page.keyboard.press("Tab");
  await expect(region).toBeFocused();
  expect(
    await region.evaluate((node) => node.scrollWidth > node.clientWidth),
  ).toBe(true);
  await page.keyboard.press("ArrowRight");
  await expect
    .poll(() => region.evaluate((node) => node.scrollLeft))
    .toBeGreaterThan(0);
  expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
});
