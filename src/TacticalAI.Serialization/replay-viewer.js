// File: replay-viewer.js
// Purpose: Navigate validated fictional game frames with safe DOM/SVG and explicit diagnostic limits.
// Author: Jason Stys. Variables: data is generated bounded JSON; selected avatar never becomes HTML or a selector.
// Declaration index: docs/CODE_INDEX.md records authored declarations.
"use strict";
const data = JSON.parse(document.getElementById("data").textContent);
const slider = document.getElementById("frame"),
  avatar = document.getElementById("avatar"),
  plot = document.getElementById("plot");
slider.max = String(data.frames.length - 1);
document.getElementById("notice").textContent = data.notice;

/** Build fixed SVG tags; all content is text and all geometry comes from bounded validated grid numbers. */
function svg(tag, attributes, label = "") {
  const node = document.createElementNS("http://www.w3.org/2000/svg", tag);
  for (const [key, value] of Object.entries(attributes))
    node.setAttribute(key, String(value));
  node.textContent = label;
  return node;
}
/** Show command identity without inferring the recording's policy or executing its action. */
function command(value) {
  return value
    ? `${value.kind}: ${value.actorId || "team"} → ${value.targetUnitId ?? `[${value.x},${value.y}]`}${value.abilityId ? ` / ${value.abilityId}` : ""}`
    : "none";
}
/** Render a single selected validated state; no future recorded command changes the board. */
function render() {
  const frame = data.frames[Number(slider.value)],
    selected = avatar.value;
  avatar.replaceChildren();
  for (const unit of frame.units) {
    const option = document.createElement("option");
    option.value = unit.id;
    option.textContent = `${unit.id} / ${unit.team}${unit.hitPoints ? "" : " / defeated"}`;
    avatar.append(option);
  }
  if (frame.units.some((unit) => unit.id === selected)) avatar.value = selected;
  document.getElementById("frame-label").textContent =
    `${frame.index} of ${data.frames.length - 1}`;
  document.getElementById("status").textContent =
    `Frame ${frame.index}; turn ${frame.turn}; active ${frame.activeTeam}; winner ${frame.winner ?? "not recorded"}.`;
  document.getElementById("previous").textContent =
    `Previous applied command: ${command(frame.previousCommand)}`;
  document.getElementById("next").textContent =
    `Next recorded command (hindsight): ${command(frame.nextRecordedCommand)}`;
  const bot = frame.defaultBot;
  document.getElementById("recommendation").textContent = bot
    ? `Current default-bot suggestion: ${command(bot.command)}. Score ${bot.score}; ${bot.candidatesEvaluated}/${data.evaluationBudget} candidates; ${bot.budgetExhausted ? "INCOMPLETE CANDIDATE SEARCH" : "candidate budget not exhausted"}. ${bot.explanation}`
    : "No default-bot suggestion for a terminal game state.";
  document.getElementById("hash").textContent =
    `Validated state SHA-256: ${frame.stateHash}`;
  const body = document.getElementById("units");
  body.replaceChildren();
  plot.replaceChildren();
  plot.setAttribute("viewBox", `0 0 ${data.width * 32} ${data.height * 32}`);
  data.cells.forEach((cell, index) =>
    plot.append(
      svg("rect", {
        x: (index % data.width) * 32,
        y: Math.floor(index / data.width) * 32,
        width: 32,
        height: 32,
        fill: cell.isWalkable
          ? cell.cover
            ? "#324f63"
            : "#172537"
          : "#080c12",
        stroke: "#718499",
      }),
    ),
  );
  for (const sight of frame.sight) {
    const partner =
      sight.first === avatar.value
        ? sight.second
        : sight.second === avatar.value
          ? sight.first
          : null;
    if (!partner || !sight.clear) continue;
    const from = frame.units.find((unit) => unit.id === avatar.value),
      to = frame.units.find((unit) => unit.id === partner);
    plot.append(
      svg("line", {
        x1: from.x * 32 + 16,
        y1: from.y * 32 + 16,
        x2: to.x * 32 + 16,
        y2: to.y * 32 + 16,
        stroke: "#7ce5b3",
        "stroke-width": 3,
      }),
    );
  }
  let ordinal = 0;
  for (const unit of frame.units) {
    ordinal++;
    plot.append(
      svg("circle", {
        cx: unit.x * 32 + 16,
        cy: unit.y * 32 + 16,
        r: 11,
        fill:
          unit.hitPoints === 0
            ? "#8994a3"
            : unit.team === "Blue"
              ? "#73b8ff"
              : "#ffb5b2",
      }),
    );
    plot.append(
      svg(
        "text",
        {
          x: unit.x * 32 + 16,
          y: unit.y * 32 + 20,
          fill: "#080c12",
          "text-anchor": "middle",
          "font-size": 12,
        },
        String(ordinal),
      ),
    );
    const row = document.createElement("tr");
    for (const value of [
      `${ordinal}. ${unit.id} / ${unit.team}`,
      `[${unit.x},${unit.y}]`,
      `${unit.hitPoints}/${unit.maxHitPoints}`,
      Object.entries(unit.cooldowns)
        .map(([key, value]) => `${key}: ${value}`)
        .join(", ") || "none",
    ]) {
      const cell = document.createElement("td");
      cell.textContent = value;
      row.append(cell);
    }
    body.append(row);
  }
}
slider.addEventListener("input", render);
avatar.addEventListener("change", render);
document.getElementById("download").addEventListener("click", () => {
  const url = URL.createObjectURL(
    new Blob([JSON.stringify(data, null, 2)], { type: "application/json" }),
  );
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = "fictional-game-replay-inspection.json";
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
});
render();
