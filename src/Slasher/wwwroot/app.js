const state = {
  selectedWindow: null,
  stopScript: false,
  scriptRunning: false,
  scriptAbortController: null,
  lastRun: null,
  recentRuns: [],
  variables: {}
};

const $ = (id) => document.getElementById(id);
const sampleScript = `# One command per line.
# Blank lines and lines starting with # are skipped.
start notepad.exe
wait 800
app select notepad as app
set message "hello from Slasher script"
text "hello from Slasher script"
text "\${message}"
capture selected`;

function log(message, payload) {
  const time = new Date().toLocaleTimeString();
  const suffix = payload === undefined ? "" : `\n${JSON.stringify(payload, null, 2)}`;
  $("log").textContent = `[${time}] ${message}${suffix}\n\n${$("log").textContent}`;
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    headers: {
      "Content-Type": "application/json",
      ...(options.headers || {})
    },
    ...options
  });

  const text = await response.text();
  const body = text ? JSON.parse(text) : null;
  if (!response.ok) {
    const message = body?.message || body?.title || body?.diagnostics?.[0]?.message || response.statusText;
    const error = new Error(message);
    error.body = body;
    error.status = response.status;
    throw error;
  }

  return body;
}

function formJson(form) {
  const data = new FormData(form);
  const result = {};
  for (const [key, value] of data.entries()) {
    if (value === "") {
      continue;
    }

    const input = form.elements[key];
    result[key] = input?.type === "number" ? Number(value) : value;
  }

  return result;
}

function selectWindow(windowInfo) {
  state.selectedWindow = windowInfo;
  state.variables.selected = windowInfo;
  $("selected-target").textContent = `${windowInfo.title || "(no title)"} ${windowInfo.handle} pid=${windowInfo.processId}`;

  for (const item of document.querySelectorAll(".window-item")) {
    item.classList.toggle("selected", item.dataset.handle === windowInfo.handle);
  }
}

async function refreshWindows() {
  const filter = $("window-filter").value.trim();
  const path = filter ? `/windows?title=${encodeURIComponent(filter)}` : "/windows";
  const windows = await api(path);
  const list = $("window-list");
  list.textContent = "";

  for (const win of windows) {
    const item = document.createElement("button");
    item.type = "button";
    item.className = "window-item";
    item.dataset.handle = win.handle;
    item.innerHTML = `
      <span class="window-title"></span>
      <span class="window-meta"></span>
    `;
    item.querySelector(".window-title").textContent = win.title || "(no title)";
    item.querySelector(".window-meta").textContent =
      `${win.handle} | ${win.processName || "unknown"}:${win.processId} | ${win.bounds.width}x${win.bounds.height}`;
    item.addEventListener("click", () => selectWindow(win));
    list.appendChild(item);
  }

  log(`Loaded ${windows.length} windows`);
}

function getVariableValue(path) {
  const parts = path.replace(/\[(\d+)\]/g, ".$1").split(".");
  let value = state.variables[parts[0]];
  for (const part of parts.slice(1)) {
    if (value === undefined || value === null || typeof value !== "object") {
      throw new Error(`Variable '${path}' is not available.`);
    }

    value = value[part];
  }

  if (value === undefined) {
    throw new Error(`Variable '${path}' is not available.`);
  }

  return value;
}

function getVariablePath(path) {
  const value = getVariableValue(path);
  return typeof value === "string" ? value : JSON.stringify(value);
}

function expandVariables(input) {
  return input.replace(/\$\{([A-Za-z_][A-Za-z0-9_.\[\]-]*)\}/g, (_, name) => String(getVariablePath(name)));
}

function assignVariable(name, value) {
  if (!/^[A-Za-z_][A-Za-z0-9_]*$/.test(name)) {
    throw new Error("Variable names must start with a letter or underscore and contain only letters, digits, and underscores.");
  }

  state.variables[name] = value;
  log(`Variable ${name} assigned`, value);
  return value;
}

function splitAssignmentSuffix(tokens) {
  if (tokens.length >= 3 && tokens.at(-2).toLowerCase() === "as") {
    return { tokens: tokens.slice(0, -2), variableName: tokens.at(-1) };
  }

  return { tokens, variableName: null };
}

function requireArray(name) {
  const value = getVariableValue(name);
  if (!Array.isArray(value)) {
    throw new Error(`Variable '${name}' is not an array.`);
  }

  return value;
}

function requireTarget() {
  if (!state.selectedWindow) {
    throw new Error("Select a window first.");
  }

  return state.selectedWindow.handle;
}

function parseCommandLine(input) {
  const tokens = [];
  let current = "";
  let quote = null;
  let escaping = false;

  for (const ch of input.trim()) {
    if (escaping) {
      current += ch;
      escaping = false;
      continue;
    }

    if (ch === "\\") {
      escaping = true;
      continue;
    }

    if (quote) {
      if (ch === quote) {
        quote = null;
      } else {
        current += ch;
      }
      continue;
    }

    if (ch === '"' || ch === "'") {
      quote = ch;
      continue;
    }

    if (/\s/.test(ch)) {
      if (current) {
        tokens.push(current);
        current = "";
      }
      continue;
    }

    current += ch;
  }

  if (escaping) {
    current += "\\";
  }

  if (quote) {
    throw new Error("Unclosed quote in command.");
  }

  if (current) {
    tokens.push(current);
  }

  return tokens;
}

function requireNumber(value, name) {
  const number = Number(value);
  if (!Number.isFinite(number)) {
    throw new Error(`${name} must be a number.`);
  }

  return number;
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function appSelectPayload(args) {
  const match = ["exact", "contains"].includes((args.at(-1) || "").toLowerCase())
    ? args.at(-1).toLowerCase()
    : "contains";
  const nameParts = match === "contains" ? args : args.slice(0, -1);
  const name = nameParts.join(" ");
  if (!name) {
    throw new Error("app select requires an app/process name or title.");
  }

  return { name, match, focus: true };
}

async function selectApp(args) {
  const win = await api("/apps/select", {
    method: "POST",
    body: JSON.stringify(appSelectPayload(args))
  });
  selectWindow(win);
  log("Selected app window", win);
  return win;
}

function appStem(fileName) {
  const leaf = fileName.split(/[\\/]/).pop() || fileName;
  return leaf.replace(/\.[^.]+$/, "").toLowerCase();
}

function windowScore(win, startResult, stem, beforeHandles) {
  let score = 0;
  const processName = (win.processName || "").toLowerCase();
  const title = (win.title || "").toLowerCase();
  const className = (win.className || "").toLowerCase();

  if (isUtilityWindow(title, className)) {
    score -= 200;
  }

  if (win.handle === startResult.mainWindowHandle) {
    score += 200;
  }

  if (win.processId === startResult.processId) {
    score += 120;
  }

  if (processName === stem) {
    score += 90;
  } else if (processName.includes(stem) || stem.includes(processName)) {
    score += 60;
  }

  if (title.includes(stem)) {
    score += 30;
  }

  if (className === stem || className.includes(stem)) {
    score += 40;
  }

  if (!beforeHandles.has(win.handle)) {
    score += 25;
  }

  if (win.isVisible) {
    score += 80;
  } else {
    score -= 80;
  }

  if (win.bounds?.width >= 300 && win.bounds?.height >= 200) {
    score += 50;
  }

  if (!win.isMinimized) {
    score += 20;
  }

  return score;
}

function isUtilityWindow(title, className) {
  return title === "default ime"
    || title === "msctfime ui"
    || title.startsWith("gdi+ window")
    || className === "ime"
    || className === "msctfime ui"
    || className === "gdi+ hook window class";
}

async function selectStartedWindow(startResult, fileName, beforeWindows = []) {
  const stem = appStem(fileName || startResult.processName || "");
  const beforeHandles = new Set(beforeWindows.map((win) => win.handle));

  if (startResult.mainWindowHandle) {
    const win = await api(`/windows/${encodeURIComponent(startResult.mainWindowHandle)}`);
    selectWindow(win);
    return win;
  }

  for (let attempt = 0; attempt < 28; attempt++) {
    await sleep(250);
    const windows = await api("/windows");
    const scored = windows
      .map((win) => ({ win, score: windowScore(win, startResult, stem, beforeHandles) }))
      .filter((item) => item.score >= 60)
      .sort((a, b) => b.score - a.score);

    if (scored.length > 0) {
      selectWindow(scored[0].win);
      return scored[0].win;
    }
  }

  return null;
}

async function focusSelectedTarget() {
  if (!state.selectedWindow) {
    return null;
  }

  const result = await api(`/windows/${encodeURIComponent(state.selectedWindow.handle)}/focus`, { method: "POST" });
  await sleep(150);
  return result;
}

function showCommandHelp() {
  $("command-help-panel").hidden = false;
  log("Commands", {
    examples: [
      "refresh",
      "activate Notepad",
      "foreground",
      "select 0x123456",
      "app select notepad",
      "select app notepad",
      "foreground as win",
      "set message \"hello\"",
      "text \"${message}\"",
      "text \"${selected.title}\"",
      "start notepad.exe",
      "focus",
      "title",
      "wait window Notepad 10000",
      "capture selected",
      "capture full",
      "state restore",
      "move 80 80 900 640",
      "keys CTRL+S",
      "text \"hello from Slasher\"",
      "click 400 300 left",
      "primaryclick 400 300",
      "secondaryclick 400 300",
      "doubleclick 400 300",
      "rightclick 400 300",
      "drag 100 100 500 500 600",
      "contextmenu 400 300",
      "scroll -120",
      "clipboard assign \"hello\"",
      "clipboard get",
      "file open C:\\\\temp\\\\a.txt",
      "folder create C:\\\\temp\\\\work",
      "browser launch https://example.com",
      "browser launch edge https://example.com",
      "browser launch chrome https://example.com",
      "browser launch firefox https://example.com",
      "browser select edge",
      "mouse wheel 400 300 120",
      "close"
    ]
  });
}

function scriptLines(text) {
  return text
    .split(/\r?\n/)
    .map((line, index) => ({ number: index + 1, text: line.trim() }))
    .filter((line) => line.text && !line.text.startsWith("#"));
}

function setScriptRunning(running) {
  state.scriptRunning = running;
  $("script-run").disabled = running;
  $("script-check").disabled = running;
  $("script-stop").disabled = !running;
  $("script-status-text").textContent = running ? "running" : "idle";
}

function saveScript() {
  localStorage.setItem("slasher.script", $("script-input").value);
  log("Saved script in this browser");
}

function loadSavedScript() {
  const saved = localStorage.getItem("slasher.script");
  if (saved !== null) {
    $("script-input").value = saved;
  }
}

async function runScript() {
  if (state.scriptRunning) {
    return;
  }

  const script = $("script-input").value;
  if (scriptLines(script).length === 0) {
    log("Script is empty");
    return;
  }

  state.stopScript = false;
  state.scriptAbortController = new AbortController();
  setScriptRunning(true);
  log("Server script started");

  try {
    const response = await api("/scripts/run", {
      method: "POST",
      body: JSON.stringify({ script }),
      signal: state.scriptAbortController.signal
    });
    await renderScriptRunResponse(response);
  } catch (error) {
    if (error.name === "AbortError") {
      log("Script stopped by user");
      $("script-status-text").textContent = "stopped";
      return;
    }

    if (error.body?.run) {
      await renderScriptRunResponse(error.body);
    } else {
      log(`Script failed: ${error.message}`, error.body);
    }
  } finally {
    state.scriptAbortController = null;
    setScriptRunning(false);
  }
}

async function checkScript() {
  if (state.scriptRunning) {
    return;
  }

  const script = $("script-input").value;
  if (scriptLines(script).length === 0) {
    log("Script is empty");
    return;
  }

  $("script-check").disabled = true;
  $("script-status-text").textContent = "checking";
  try {
    const response = await api("/scripts/check", {
      method: "POST",
      body: JSON.stringify({ script })
    });
    log(`Script check passed (${response.lines.length} lines)`, response.lines);
    $("script-status-text").textContent = "check passed";
  } catch (error) {
    log("Script check failed", error.body?.diagnostics || error.body || { message: error.message });
    $("script-status-text").textContent = "check failed";
  } finally {
    $("script-check").disabled = false;
  }
}

async function renderScriptRunResponse(response) {
  state.lastRun = response;
  const status = response.run?.status || (response.ok ? "passed" : "failed");
  const eventCount = response.events?.length ?? response.run?.eventCount ?? 0;
  $("script-status-text").textContent = status;
  renderRunReport(response);
  log(`Script ${status}: ${response.run?.runId || "(no run id)"} (${eventCount} events)`, {
    artifactRoot: response.run?.artifactRoot,
    error: response.error
  });

  if (response.run?.selectedTarget?.kind === "window" && response.run.selectedTarget.handle) {
    selectWindow(response.run.selectedTarget);
  }

  const screenshot = latestScriptScreenshot(response);
  if (screenshot) {
    await loadScriptScreenshot(response.run.runId, screenshot.path);
  }

  refreshRuns().catch((error) => log(error.message));
}

function renderRunReport(response) {
  const summary = $("run-summary");
  const events = $("run-events");
  events.textContent = "";

  if (!response?.run) {
    summary.textContent = "No script run yet";
    return;
  }

  const run = response.run;
  summary.textContent = "";
  summary.append(
    summaryLine("Status", run.status),
    summaryLine("Run ID", run.runId),
    summaryLine("Artifact root", run.artifactRoot),
    summaryLine("Events", String(response.events?.length ?? run.eventCount ?? 0))
  );
  summary.appendChild(renderRunArtifactLinks(run));

  if (response.error) {
    summary.append(summaryLine("Error", `${response.error.code}: ${response.error.message}`));
    summary.appendChild(renderDiagnostics(response.error.details));
  }

  for (const event of response.events || []) {
    events.appendChild(renderRunEvent(run.runId, event));
  }
}

function summaryLine(label, value) {
  const line = document.createElement("div");
  const strong = document.createElement("strong");
  strong.textContent = `${label}: `;
  line.append(strong, value || "-");
  return line;
}

function renderRunArtifactLinks(run) {
  const row = document.createElement("div");
  row.className = "run-links";
  if (run.runId) {
    row.appendChild(runLink("HTML report", `/automation/runs/${encodeURIComponent(run.runId)}/report`));
  }

  if (run.artifacts?.summary) {
    row.appendChild(runLink("summary.txt", `/automation/runs/${encodeURIComponent(run.runId)}/summary`));
  }

  if (run.artifacts?.scriptLog || run.artifacts?.logs) {
    row.appendChild(runLink("script.log", `/automation/runs/${encodeURIComponent(run.runId)}/logs/script`));
  }

  if (run.artifacts?.events) {
    row.appendChild(runLink("events.jsonl", `/automation/runs/${encodeURIComponent(run.runId)}/events`));
  }

  if (run.artifacts?.run) {
    row.appendChild(runLink("run.json", `/automation/runs/${encodeURIComponent(run.runId)}`));
  }

  return row;
}

async function refreshRuns() {
  const response = await api("/automation/runs?limit=10");
  state.recentRuns = response?.runs || [];
  renderRecentRuns();
}

function renderRecentRuns() {
  const list = $("recent-runs");
  list.textContent = "";

  const title = document.createElement("div");
  title.className = "recent-runs-title";
  title.textContent = "Recent Runs";
  list.appendChild(title);

  if (state.recentRuns.length === 0) {
    const empty = document.createElement("div");
    empty.className = "recent-runs-empty";
    empty.textContent = "No completed runs";
    list.appendChild(empty);
    return;
  }

  for (const run of state.recentRuns) {
    list.appendChild(renderRecentRun(run));
  }
}

function renderRecentRun(run) {
  const item = document.createElement("div");
  item.className = "recent-run";

  const title = document.createElement("div");
  title.className = "recent-run-title";

  const status = document.createElement("span");
  status.className = `pill${run.status === "failed" ? " failed" : ""}`;
  status.textContent = run.status || "unknown";

  const name = document.createElement("span");
  name.className = "recent-run-name";
  name.textContent = run.name || run.runId || "(no run id)";
  name.title = run.runId || "";

  const meta = document.createElement("span");
  meta.className = "recent-run-meta";
  meta.textContent = [
    run.eventCount !== undefined ? `${run.eventCount} events` : null,
    run.durationMs !== undefined ? `${run.durationMs} ms` : null
  ].filter(Boolean).join(" | ");

  title.append(status, name);
  if (meta.textContent) {
    title.appendChild(meta);
  }
  item.appendChild(title);

  const links = document.createElement("div");
  links.className = "recent-run-links";

  const load = document.createElement("button");
  load.type = "button";
  load.textContent = "Load";
  load.addEventListener("click", () => loadRun(run.runId).catch((error) => log(error.message)));
  links.appendChild(load);

  if (run.runId) {
    links.append(
      runLink("HTML report", `/automation/runs/${encodeURIComponent(run.runId)}/report`),
      runLink("summary.txt", `/automation/runs/${encodeURIComponent(run.runId)}/summary`),
      runLink("script.log", `/automation/runs/${encodeURIComponent(run.runId)}/logs/script`),
      runLink("run.json", `/automation/runs/${encodeURIComponent(run.runId)}`)
    );
  }

  item.appendChild(links);
  return item;
}

async function loadRun(runId) {
  if (!runId) {
    throw new Error("Run ID is missing");
  }

  const run = await api(`/automation/runs/${encodeURIComponent(runId)}`);
  const eventResponse = await api(`/automation/runs/${encodeURIComponent(runId)}/events`);
  const response = {
    ok: run.status === "passed",
    run,
    events: eventResponse?.events || [],
    error: run.error || null
  };
  renderRunReport(response);
  state.lastRun = response;

  const screenshot = latestScriptScreenshot(response);
  if (screenshot) {
    await loadScriptScreenshot(runId, screenshot.path);
  }

  log(`Loaded run ${runId}`, { status: run.status, events: response.events.length });
}

function runLink(label, href) {
  const link = document.createElement("a");
  link.className = "pill link-pill";
  link.href = href;
  link.target = "_blank";
  link.rel = "noreferrer";
  link.textContent = label;
  return link;
}

function renderRunEvent(runId, event) {
  const item = document.createElement("div");
  item.className = `run-event${event.ok ? "" : " failed"}`;

  const title = document.createElement("div");
  title.className = "run-event-title";

  const sequence = document.createElement("strong");
  sequence.textContent = `#${event.sequence}`;
  const status = document.createElement("span");
  status.className = `pill${event.ok ? "" : " failed"}`;
  status.textContent = event.ok ? "ok" : "failed";
  const action = document.createElement("span");
  action.className = "pill";
  action.textContent = event.action || "script";
  title.append(sequence, status, action);

  if (event.step) {
    const step = document.createElement("span");
    step.textContent = event.step;
    title.append(step);
  }

  item.appendChild(title);

  const source = event.source;
  if (source?.file || source?.line || event.durationMs !== undefined) {
    const meta = document.createElement("div");
    meta.className = "event-meta";
    meta.textContent = [
      source?.file ? `${source.file}:${source.line ?? "?"}` : null,
      event.durationMs !== undefined ? `${event.durationMs} ms` : null,
      event.target?.title ? `target=${event.target.title}` : null
    ].filter(Boolean).join(" | ");
    item.appendChild(meta);
  }

  if (event.error) {
    const error = document.createElement("div");
    error.className = "event-error";
    error.textContent = `${event.error.code}: ${event.error.message}`;
    item.appendChild(error);
    item.appendChild(renderDiagnostics(event.error.details));
  }

  const visibleEvidence = (event.evidence || []).filter((evidence) =>
    evidence.kind === "screenshot" || evidence.kind === "attachment");
  if (visibleEvidence.length > 0) {
    const evidenceRow = document.createElement("div");
    evidenceRow.className = "evidence-row";
    for (const evidence of visibleEvidence) {
      if (evidence.kind === "screenshot") {
        const button = document.createElement("button");
        button.type = "button";
        button.textContent = evidence.role || "screenshot";
        button.title = evidence.path;
        button.addEventListener("click", () => loadScriptScreenshot(runId, evidence.path).catch((error) => log(error.message)));
        evidenceRow.appendChild(button);
      } else {
        const link = runLink(
          evidence.role || "attachment",
          `/automation/runs/${encodeURIComponent(runId)}/artifacts/content?path=${encodeURIComponent(evidence.path)}`);
        link.title = evidence.path;
        evidenceRow.appendChild(link);
      }
    }
    item.appendChild(evidenceRow);
  }

  return item;
}

function renderDiagnostics(details) {
  const diagnostics = details?.diagnostics || [];
  const selected = details?.selectedWindow;
  const foreground = details?.foregroundWindow;
  const panel = document.createElement("div");
  panel.className = "diagnostics";

  if (diagnostics.length === 0 && !selected && !foreground) {
    panel.hidden = true;
    return panel;
  }

  if (diagnostics.length > 0) {
    const title = document.createElement("div");
    title.className = "diagnostics-title";
    title.textContent = "Diagnostics";
    panel.appendChild(title);
    for (const diagnostic of diagnostics) {
      const item = document.createElement("div");
      item.className = `diagnostic ${diagnostic.severity || "info"}`;
      const code = document.createElement("strong");
      code.textContent = diagnostic.code || "diagnostic";
      item.append(code, `: ${diagnostic.message || ""}`);
      panel.appendChild(item);
    }
  }

  const windows = [
    selected ? `selected=${formatDiagnosticWindow(selected)}` : null,
    foreground ? `foreground=${formatDiagnosticWindow(foreground)}` : null
  ].filter(Boolean);
  if (windows.length > 0) {
    const meta = document.createElement("div");
    meta.className = "diagnostic-windows";
    meta.textContent = windows.join(" | ");
    panel.appendChild(meta);
  }

  return panel;
}

function formatDiagnosticWindow(window) {
  const title = window.title || "(no title)";
  const process = window.processName || "?";
  return `${title} [${process} ${window.handle || ""}]`;
}

function clearRunReport() {
  state.lastRun = null;
  $("run-summary").textContent = "No script run yet";
  $("run-events").textContent = "";
}

function latestScriptScreenshot(response) {
  const events = response.events || [];
  for (const event of [...events].reverse()) {
    const evidence = [...(event.evidence || [])].reverse().find((item) =>
      item.kind === "screenshot"
      && (item.role === "after-preview" || item.role === "after" || item.role === "error-preview" || item.role === "error"));
    if (evidence) {
      return evidence;
    }
  }

  return null;
}

async function loadScriptScreenshot(runId, path) {
  const content = await api(`/automation/runs/${encodeURIComponent(runId)}/artifacts/content?path=${encodeURIComponent(path)}`);
  $("shot").src = `data:${content.mimeType};base64,${content.base64Content}`;
  $("shot").parentElement.classList.add("has-shot");
  log("Loaded script screenshot", { path, mimeType: content.mimeType, length: content.length });
}

async function runScriptBlock(lines, start, end) {
  let index = start;
  while (index < end) {
    const line = lines[index];
    if (state.stopScript) {
      log(`Script stopped before line ${line.number}`);
      return end;
    }

    const control = firstWord(line.text);
    if (control === "else" || control === "endif" || control === "endrepeat" || control === "endwhile" || control === "endforeach") {
      return index;
    }

    $("script-status-text").textContent = `line ${line.number}: ${line.text}`;

    if (control === "if") {
      index = await runIfBlock(lines, index, end);
      continue;
    }

    if (control === "repeat") {
      index = await runRepeatBlock(lines, index, end);
      continue;
    }

    if (control === "while") {
      index = await runWhileBlock(lines, index, end);
      continue;
    }

    if (control === "foreach") {
      index = await runForeachBlock(lines, index, end);
      continue;
    }

    log(`script:${line.number} ${line.text}`);
    await runCommandLine(line.text);
    index++;
  }

  return index;
}

async function runIfBlock(lines, index, end) {
  const line = lines[index];
  const match = findBlockEnd(lines, index, end, "if", ["endif"], ["else"]);
  if (!match.end) {
    throw new Error(`if at line ${line.number} is missing endif.`);
  }

  const condition = line.text.replace(/^if\s*/i, "");
  if (evaluateCondition(condition)) {
    await runScriptBlock(lines, index + 1, match.elseIndex ?? match.end);
  } else if (match.elseIndex !== null) {
    await runScriptBlock(lines, match.elseIndex + 1, match.end);
  }

  return match.end + 1;
}

async function runRepeatBlock(lines, index, end) {
  const line = lines[index];
  const match = findBlockEnd(lines, index, end, "repeat", ["endrepeat"]);
  if (!match.end) {
    throw new Error(`repeat at line ${line.number} is missing endrepeat.`);
  }

  const count = requireNumber(expandVariables(line.text.replace(/^repeat\s*/i, "") || "0"), "repeat count");
  if (count < 0) {
    throw new Error("repeat count must be zero or positive.");
  }

  for (let i = 0; i < count; i++) {
    state.variables.index = i;
    state.variables.iteration = i + 1;
    await runScriptBlock(lines, index + 1, match.end);
    if (state.stopScript) {
      break;
    }
  }

  return match.end + 1;
}

async function runWhileBlock(lines, index, end) {
  const line = lines[index];
  const match = findBlockEnd(lines, index, end, "while", ["endwhile"]);
  if (!match.end) {
    throw new Error(`while at line ${line.number} is missing endwhile.`);
  }

  const condition = line.text.replace(/^while\s*/i, "");
  for (let i = 0; evaluateCondition(condition); i++) {
    if (i >= 1000) {
      throw new Error("while loop exceeded 1000 iterations.");
    }

    state.variables.index = i;
    state.variables.iteration = i + 1;
    await runScriptBlock(lines, index + 1, match.end);
    if (state.stopScript) {
      break;
    }
  }

  return match.end + 1;
}

async function runForeachBlock(lines, index, end) {
  const line = lines[index];
  const match = findBlockEnd(lines, index, end, "foreach", ["endforeach"]);
  if (match.end === null) {
    throw new Error(`foreach at line ${line.number} is missing endforeach.`);
  }

  const tokens = parseCommandLine(line.text);
  if (tokens.length < 4 || tokens[2].toLowerCase() !== "in") {
    throw new Error("foreach syntax is: foreach item in arrayName");
  }

  const itemName = tokens[1];
  const arrayName = tokens.slice(3).join(" ").replace(/^\$\{|\}$/g, "");
  const items = requireArray(arrayName);
  for (let i = 0; i < items.length; i++) {
    assignVariable(itemName, items[i]);
    state.variables.index = i;
    state.variables.iteration = i + 1;
    await runScriptBlock(lines, index + 1, match.end);
    if (state.stopScript) {
      break;
    }
  }

  return match.end + 1;
}

function firstWord(text) {
  return (parseCommandLine(text)[0] || "").toLowerCase();
}

function findBlockEnd(lines, start, end, opener, closers, middle = []) {
  let depth = 0;
  let elseIndex = null;
  for (let i = start + 1; i < end; i++) {
    const word = firstWord(lines[i].text);
    if (word === opener) {
      depth++;
      continue;
    }

    if (closers.includes(word)) {
      if (depth === 0) {
        return { end: i, elseIndex };
      }

      depth--;
      continue;
    }

    if (depth === 0 && middle.includes(word) && elseIndex === null) {
      elseIndex = i;
    }
  }

  return { end: null, elseIndex };
}

function evaluateCondition(expression) {
  const tokens = parseCommandLine(expandVariables(expression));
  if (tokens.length === 0) {
    return false;
  }

  let negate = false;
  if (tokens[0].toLowerCase() === "not") {
    negate = true;
    tokens.shift();
  }

  let result;
  const keyword = (tokens[0] || "").toLowerCase();
  if (keyword === "exists") {
    result = variableExists(tokens[1] || "");
  } else if (keyword === "empty") {
    result = !truthy(tokens.slice(1).join(" "));
  } else if (tokens.length === 1) {
    result = truthy(tokens[0]);
  } else {
    result = compareValues(tokens[0], tokens[1], tokens.slice(2).join(" "));
  }

  return negate ? !result : result;
}

function variableExists(path) {
  try {
    getVariablePath(path.replace(/^\$\{|\}$/g, ""));
    return true;
  } catch {
    return false;
  }
}

function truthy(value) {
  const text = String(value ?? "").trim().toLowerCase();
  return text !== "" && text !== "false" && text !== "0" && text !== "null";
}

function compareValues(left, operator, right) {
  const leftNumber = Number(left);
  const rightNumber = Number(right);
  const numeric = Number.isFinite(leftNumber) && Number.isFinite(rightNumber);
  const a = numeric ? leftNumber : String(left);
  const b = numeric ? rightNumber : String(right);

  switch (operator.toLowerCase()) {
    case "==":
    case "=":
    case "eq":
      return a === b;
    case "!=":
    case "<>":
    case "ne":
      return a !== b;
    case ">":
      return a > b;
    case ">=":
      return a >= b;
    case "<":
      return a < b;
    case "<=":
      return a <= b;
    case "contains":
      return String(left).includes(String(right));
    case "startswith":
      return String(left).startsWith(String(right));
    case "endswith":
      return String(left).endsWith(String(right));
    default:
      throw new Error(`Unknown condition operator '${operator}'.`);
  }
}

async function runFileCommand(args) {
  const subcommand = (args[0] || "").toLowerCase();
  const path = args[1];
  const destination = args[2];

  if (!subcommand || !path) {
    throw new Error("file requires a subcommand and path.");
  }

  const operations = {
    copy: ["/files/copy", { path, destination, overwrite: args.includes("--overwrite") }],
    delete: ["/files/delete", { path }],
    rename: ["/files/rename", { path, destination, overwrite: args.includes("--overwrite") }],
    open: ["/files/open", { path }],
    print: ["/files/print", { path }]
  };

  if (subcommand === "name") {
    const result = { name: path.split(/[\\/]/).pop() || path };
    log("File name", result);
    return result;
  }

  if (subcommand === "path") {
    const result = { path };
    log("File path", result);
    return result;
  }

  if (subcommand === "info" || subcommand === "attributes") {
    const result = await api(`/files/info?path=${encodeURIComponent(path)}`);
    log("File info", result);
    return result;
  }

  const operation = operations[subcommand];
  if (!operation) {
    throw new Error("file supports copy, delete, rename, open, print, name, path, info.");
  }

  if ((subcommand === "copy" || subcommand === "rename") && !destination) {
    throw new Error(`file ${subcommand} requires a destination.`);
  }

  const [endpoint, payload] = operation;
  const result = await api(endpoint, { method: "POST", body: JSON.stringify(payload) });
  log(`File ${subcommand}`, result);
  return result;
}

async function runFolderCommand(args) {
  const subcommand = (args[0] || "").toLowerCase();
  const path = args[1];
  const destination = args[2];

  if (!subcommand || !path) {
    throw new Error("folder requires a subcommand and path.");
  }

  if (subcommand === "info" || subcommand === "attributes") {
    const result = await api(`/files/info?path=${encodeURIComponent(path)}`);
    log("Folder info", result);
    return result;
  }

  const operations = {
    create: ["/folders/create", { path }],
    copy: ["/folders/copy", { path, destination, overwrite: args.includes("--overwrite") }],
    delete: ["/folders/delete", { path, recursive: args.includes("--recursive") || args.includes("-r") }],
    rename: ["/folders/rename", { path, destination }],
    open: ["/folders/open", { path }],
    zip: ["/folders/zip", { path, destination, overwrite: args.includes("--overwrite") }],
    unzip: ["/folders/unzip", { path, destination, overwrite: args.includes("--overwrite") }]
  };

  const operation = operations[subcommand];
  if (!operation) {
    throw new Error("folder supports create, copy, delete, rename, open, zip, unzip, info.");
  }

  if (["copy", "rename", "zip", "unzip"].includes(subcommand) && !destination) {
    throw new Error(`folder ${subcommand} requires a destination.`);
  }

  const [endpoint, payload] = operation;
  const result = await api(endpoint, { method: "POST", body: JSON.stringify(payload) });
  log(`Folder ${subcommand}`, result);
  return result;
}

async function runApplicationCommand(args) {
  const subcommand = (args[0] || "").toLowerCase();
  if (subcommand === "select") {
    return selectApp(args.slice(1));
  }

  if (subcommand === "open" || subcommand === "start") {
    return runCommandLine(`start ${args.slice(1).join(" ")}`);
  }

  if (subcommand === "close") {
    const target = args[1];
    if (!target) {
      throw new Error("app close requires a process name or process id.");
    }

    const processId = Number(target);
    const payload = Number.isFinite(processId)
      ? { processId, force: args.includes("--force") }
      : { processName: target, force: args.includes("--force") };
    const result = await api("/apps/close", { method: "POST", body: JSON.stringify(payload) });
    log("Application close", result);
    return result;
  }

  throw new Error("app supports select, open/start, and close.");
}

async function runBrowserCommand(args) {
  const subcommand = (args[0] || "").toLowerCase();
  if (subcommand === "launch" || subcommand === "open") {
    if (!args[1]) {
      throw new Error("browser launch requires a URL or browser name.");
    }

    const browser = browserSpec(args[1]);
    const url = browser ? (args.slice(2).join(" ") || "about:blank") : args.slice(1).join(" ");
    const payload = browser
      ? { fileName: browser.executable, arguments: url, useShellExecute: true }
      : { fileName: url, useShellExecute: true };
    const result = await api("/apps/start", {
      method: "POST",
      body: JSON.stringify(payload)
    });
    if (browser) {
      await sleep(1000);
      const selected = await api("/apps/select", {
        method: "POST",
        body: JSON.stringify({ name: browser.processName, focus: true })
      });
      state.selected = selected;
      log(`Browser launch ${browser.name}`, { started: result, selected });
      return selected;
    }

    log("Browser launch", result);
    return result;
  }

  if (subcommand === "select" || subcommand === "activate") {
    const browser = browserSpec(args[1] || "");
    if (!browser) {
      throw new Error("browser select requires edge, chrome, or firefox.");
    }

    const selected = await api("/apps/select", {
      method: "POST",
      body: JSON.stringify({ name: browser.processName, focus: true })
    });
    state.selected = selected;
    log(`Browser selected ${browser.name}`, selected);
    return selected;
  }

  if (subcommand === "go" || subcommand === "navigate" || subcommand === "address") {
    const url = args.slice(1).join(" ");
    if (!url) {
      throw new Error("browser go requires a URL.");
    }

    await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "CTRL+L" }) });
    await api("/input/text", { method: "POST", body: JSON.stringify({ text: url }) });
    const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "ENTER" }) });
    log("Browser address navigation", { url });
    return result;
  }

  if (subcommand === "back") {
    const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "ALT+LEFT" }) });
    log("Browser back keystroke sent");
    return result;
  }

  if (subcommand === "forward") {
    const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "ALT+RIGHT" }) });
    log("Browser forward keystroke sent");
    return result;
  }

  if (subcommand === "refresh" || subcommand === "reload") {
    const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "CTRL+R" }) });
    log("Browser refresh keystroke sent");
    return result;
  }

  if (subcommand === "new-tab" || subcommand === "newtab") {
    const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "CTRL+T" }) });
    log("Browser new-tab keystroke sent");
    return result;
  }

  if (subcommand === "close") {
    const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "CTRL+W" }) });
    log("Browser close keystroke sent");
    return result;
  }

  throw new Error("browser supports launch/open, select, go/address, back, forward, refresh, close, and new-tab.");
}

function browserSpec(value) {
  const key = String(value || "").trim().toLowerCase();
  const specs = {
    edge: { name: "edge", executable: "msedge.exe", processName: "msedge" },
    msedge: { name: "edge", executable: "msedge.exe", processName: "msedge" },
    "msedge.exe": { name: "edge", executable: "msedge.exe", processName: "msedge" },
    "microsoft-edge": { name: "edge", executable: "msedge.exe", processName: "msedge" },
    chrome: { name: "chrome", executable: "chrome.exe", processName: "chrome" },
    "chrome.exe": { name: "chrome", executable: "chrome.exe", processName: "chrome" },
    "google-chrome": { name: "chrome", executable: "chrome.exe", processName: "chrome" },
    firefox: { name: "firefox", executable: "firefox.exe", processName: "firefox" },
    "firefox.exe": { name: "firefox", executable: "firefox.exe", processName: "firefox" },
    ff: { name: "firefox", executable: "firefox.exe", processName: "firefox" }
  };

  return specs[key] || null;
}

async function runCommandLine(input) {
  const expandedInput = expandVariables(input);
  const parsed = parseCommandLine(expandedInput);
  const assignment = splitAssignmentSuffix(parsed);
  const tokens = assignment.tokens;
  if (tokens.length === 0) {
    return;
  }

  const command = tokens[0].toLowerCase();
  const args = tokens.slice(1);
  log(expandedInput === input ? `> ${input}` : `> ${input}\n= ${expandedInput}`);

  const finish = (value) => {
    if (value !== undefined) {
      state.variables._ = value;
      if (assignment.variableName) {
        assignVariable(assignment.variableName, value);
      }
    } else if (assignment.variableName) {
      throw new Error("This command did not return a value to assign.");
    }

    return value;
  };

  switch (command) {
    case "help":
      showCommandHelp();
      return;

    case "refresh":
    case "windows":
      await refreshWindows();
      return;

    case "foreground": {
      const win = await api("/windows/foreground");
      selectWindow(win);
      log("Selected foreground window", win);
      return finish(win);
    }

    case "activate": {
      const title = args.join(" ");
      if (!title) {
        throw new Error("activate requires a window title.");
      }

      const win = await api("/windows/activate", {
        method: "POST",
        body: JSON.stringify({ title, match: "contains" })
      });
      selectWindow(win);
      log("Activated window", win);
      return finish(win);
    }

    case "title": {
      const result = await api("/windows/foreground/title");
      log("Active window title", result);
      return finish(result);
    }

    case "wait": {
      if ((args[0] || "").toLowerCase() === "window") {
        const timeout = Number(args.at(-1));
        const hasTimeout = Number.isFinite(timeout);
        const title = args.slice(1, hasTimeout ? -1 : undefined).join(" ");
        if (!title) {
          throw new Error("wait window requires a title.");
        }

        const win = await api("/windows/wait", {
          method: "POST",
          body: JSON.stringify({ title, timeoutMs: hasTimeout ? timeout : 10000 })
        });
        selectWindow(win);
        log("Window appeared", win);
        return finish(win);
      }

      const ms = requireNumber(args[0] || "1000", "milliseconds");
      await sleep(ms);
      log(`Waited ${ms} ms`);
      return;
    }

    case "select": {
      if ((args[0] || "").toLowerCase() === "app") {
        return finish(await selectApp(args.slice(1)));
      }

      if (!args[0]) {
        throw new Error("select requires a window handle.");
      }

      const win = await api(`/windows/${encodeURIComponent(args[0])}`);
      selectWindow(win);
      log("Selected window", win);
      return finish(win);
    }

    case "start": {
      if (!args[0]) {
        throw new Error("start requires a file name.");
      }

      const beforeWindows = await api("/windows");
      const result = await api("/apps/start", {
        method: "POST",
        body: JSON.stringify({
          fileName: args[0],
          arguments: args.slice(1).join(" ") || undefined
        })
      });
      log("Started app", result);
      const win = await selectStartedWindow(result, args[0], beforeWindows);
      if (win) {
        log("Selected started window", win);
        await focusSelectedTarget();
      } else {
        log("Started app, but no matching window was found. Use select or foreground before window-scoped commands.");
      }
      await refreshWindows();
      return finish(win || result);
    }

    case "focus": {
      const result = await api(`/windows/${encodeURIComponent(requireTarget())}/focus`, { method: "POST" });
      log("Focused window", result);
      return finish(result);
    }

    case "close": {
      if ((args[0] || "").toLowerCase() === "all") {
        const result = await api("/windows/close-all", {
          method: "POST",
          body: JSON.stringify({})
        });
        log("Close all requested", result);
        await refreshWindows();
        return finish(result);
      }

      const result = await api(`/windows/${encodeURIComponent(requireTarget())}/close`, { method: "POST" });
      log("Close requested", result);
      await refreshWindows();
      return finish(result);
    }

    case "state": {
      if (!args[0]) {
        throw new Error("state requires hide, show, minimize, maximize, or restore.");
      }

      return finish(await setState(args[0]));
    }

    case "restore":
    case "minimize":
    case "maximize":
    case "hide":
    case "show":
      return finish(await setState(command));

    case "move": {
      if (args.length < 4) {
        throw new Error("move requires x y width height.");
      }

      const result = await api(`/windows/${encodeURIComponent(requireTarget())}/move`, {
        method: "POST",
        body: JSON.stringify({
          x: requireNumber(args[0], "x"),
          y: requireNumber(args[1], "y"),
          width: requireNumber(args[2], "width"),
          height: requireNumber(args[3], "height")
        })
      });
      log("Moved window", result);
      await refreshWindows();
      return finish(result);
    }

    case "keys":
    case "key": {
      const keys = args.join("+");
      if (!keys) {
        throw new Error("keys requires a chord such as CTRL+S.");
      }

      await focusSelectedTarget();
      const result = await api("/input/keys", {
        method: "POST",
        body: JSON.stringify({ keys })
      });
      log("Sent keys", result);
      return finish(result);
    }

    case "text":
    case "type": {
      const text = args.join(" ");
      if (!text) {
        throw new Error("text requires content.");
      }

      await focusSelectedTarget();
      const result = await api("/input/text", {
        method: "POST",
        body: JSON.stringify({ text })
      });
      log("Typed text", result);
      return finish(result);
    }

    case "click": {
      if (args.length < 2) {
        throw new Error("click requires x y [button].");
      }

      const result = await api("/input/mouse", {
        method: "POST",
        body: JSON.stringify({
          action: "click",
          x: requireNumber(args[0], "x"),
          y: requireNumber(args[1], "y"),
          button: args[2] || "left"
        })
      });
      log("Sent mouse click", result);
      return finish(result);
    }

    case "primaryclick": {
      if (args.length < 2) {
        throw new Error("primaryclick requires x y.");
      }

      const result = await api("/input/mouse", {
        method: "POST",
        body: JSON.stringify({
          action: "click",
          x: requireNumber(args[0], "x"),
          y: requireNumber(args[1], "y"),
          button: "left"
        })
      });
      log("Sent primary click", result);
      return finish(result);
    }

    case "doubleclick": {
      if (args.length < 2) {
        throw new Error("doubleclick requires x y [button].");
      }

      for (let i = 0; i < 2; i++) {
        await api("/input/mouse", {
          method: "POST",
          body: JSON.stringify({
            action: "click",
            x: requireNumber(args[0], "x"),
            y: requireNumber(args[1], "y"),
            button: args[2] || "left"
          })
        });
        await sleep(80);
      }
      log("Sent mouse double click");
      return finish({ sent: true });
    }

    case "rightclick":
    case "secondaryclick": {
      if (args.length < 2) {
        throw new Error(`${command} requires x y.`);
      }

      const result = await api("/input/mouse", {
        method: "POST",
        body: JSON.stringify({
          action: "click",
          x: requireNumber(args[0], "x"),
          y: requireNumber(args[1], "y"),
          button: "right"
        })
      });
      log("Sent secondary click", result);
      return finish(result);
    }

    case "drag": {
      if (args.length < 4) {
        throw new Error("drag requires fromX fromY toX toY [durationMs] [button].");
      }

      const result = await api("/input/mouse/drag", {
        method: "POST",
        body: JSON.stringify({
          fromX: requireNumber(args[0], "fromX"),
          fromY: requireNumber(args[1], "fromY"),
          toX: requireNumber(args[2], "toX"),
          toY: requireNumber(args[3], "toY"),
          durationMs: args[4] === undefined ? 400 : requireNumber(args[4], "durationMs"),
          button: args[5] || "left"
        })
      });
      log("Dragged mouse", result);
      return finish(result);
    }

    case "contextmenu": {
      if (args.length < 2) {
        throw new Error("contextmenu requires x y [delayMs].");
      }

      const result = await api("/input/mouse/context-menu", {
        method: "POST",
        body: JSON.stringify({
          x: requireNumber(args[0], "x"),
          y: requireNumber(args[1], "y"),
          delayMs: args[2] === undefined ? 250 : requireNumber(args[2], "delayMs")
        })
      });
      $("shot").src = `data:${result.screenshot.mimeType};base64,${result.screenshot.base64Image}`;
      $("shot").parentElement.classList.add("has-shot");
      log("Opened context menu", {
        x: result.x,
        y: result.y,
        foregroundWindow: result.foregroundWindow,
        observation: result.observation
      });
      return finish(result);
    }

    case "scroll": {
      const result = await api("/input/mouse", {
        method: "POST",
        body: JSON.stringify({ action: "wheel", wheelDelta: requireNumber(args[0] || "-120", "wheelDelta") })
      });
      log("Scrolled mouse wheel", result);
      return finish(result);
    }

    case "mouse": {
      if (!args[0]) {
        throw new Error("mouse requires an action.");
      }

      const action = args[0].toLowerCase();
      const payload = { action };
      if (args[1] !== undefined) {
        payload.x = requireNumber(args[1], "x");
      }

      if (args[2] !== undefined) {
        payload.y = requireNumber(args[2], "y");
      }

      if (action === "wheel") {
        payload.wheelDelta = args[3] === undefined ? 120 : requireNumber(args[3], "wheelDelta");
      } else if (args[3]) {
        payload.button = args[3];
      }

      const result = await api("/input/mouse", {
        method: "POST",
        body: JSON.stringify(payload)
      });
      log("Sent mouse input", result);
      return finish(result);
    }

    case "capture":
    case "shot":
    case "screenshot": {
      const scope = (args[0] || "selected").toLowerCase();
      const previous = $("capture-selected").checked;
      $("capture-selected").checked = scope !== "full";
      try {
        return finish(await capture());
      } finally {
        $("capture-selected").checked = previous;
      }
    }

    case "clipboard": {
      const subcommand = (args[0] || "").toLowerCase();
      if (subcommand === "assign" || subcommand === "set") {
        const text = args.slice(1).join(" ");
        const result = await api("/clipboard", {
          method: "POST",
          body: JSON.stringify({ text })
        });
        log("Clipboard assigned", result);
        return finish(result);
      }

      if (subcommand === "get") {
        const result = await api("/clipboard");
        log("Clipboard text", result);
        return finish(result);
      }

      if (subcommand === "clear") {
        const result = await api("/clipboard", { method: "DELETE" });
        log("Clipboard cleared", result);
        return finish(result);
      }

      if (subcommand === "copy") {
        const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "CTRL+C" }) });
        log("Clipboard copy keystroke sent");
        return finish(result);
      }

      if (subcommand === "paste") {
        const result = await api("/input/keys", { method: "POST", body: JSON.stringify({ keys: "CTRL+V" }) });
        log("Clipboard paste keystroke sent");
        return finish(result);
      }

      throw new Error("clipboard requires assign, get, clear, copy, or paste.");
    }

    case "file":
      return finish(await runFileCommand(args));

    case "folder":
      return finish(await runFolderCommand(args));

    case "app":
    case "application":
      return finish(await runApplicationCommand(args));

    case "browser":
      return finish(await runBrowserCommand(args));

    case "recorder":
    case "ocr":
    case "image":
      throw new Error(`${command} commands are not implemented yet. Use screenshots and coordinate/window commands for now.`);

    case "delay": {
      const ms = requireNumber(args[0] || "1000", "milliseconds");
      await sleep(ms);
      log(`Delayed ${ms} ms`);
      return finish({ waitedMs: ms });
    }

    case "set":
    case "let": {
      const name = args[0];
      const valueArgs = args[1] === "=" ? args.slice(2) : args.slice(1);
      if (!name || valueArgs.length === 0) {
        throw new Error("set requires a variable name and value.");
      }

      return finish(assignVariable(name, valueArgs.join(" ")));
    }

    case "add":
    case "inc": {
      const name = args[0];
      const amount = requireNumber(args[1] || "1", "amount");
      const current = Number(state.variables[name] ?? 0);
      if (!name || !Number.isFinite(current)) {
        throw new Error("add requires a numeric variable name and optional amount.");
      }

      return finish(assignVariable(name, current + amount));
    }

    case "array": {
      const name = args[0];
      if (!name) {
        throw new Error("array requires a variable name.");
      }

      return finish(assignVariable(name, args.slice(1)));
    }

    case "push": {
      const name = args[0];
      const values = args.slice(1);
      if (!name || values.length === 0) {
        throw new Error("push requires an array name and one or more values.");
      }

      if (state.variables[name] === undefined) {
        assignVariable(name, []);
      }

      const items = requireArray(name);
      items.push(...values);
      log(`Array ${name} pushed`, items);
      return finish(items);
    }

    case "pop": {
      const name = args[0];
      if (!name) {
        throw new Error("pop requires an array name.");
      }

      const items = requireArray(name);
      const value = items.pop();
      log(`Array ${name} popped`, value);
      return finish(value);
    }

    case "get": {
      const name = args[0];
      const index = requireNumber(args[1], "index");
      if (!name) {
        throw new Error("get requires an array name and index.");
      }

      const items = requireArray(name);
      const value = items[index];
      if (value === undefined) {
        throw new Error(`Array '${name}' does not have index ${index}.`);
      }

      log(`Array ${name}[${index}]`, value);
      return finish(value);
    }

    case "length": {
      const name = args[0];
      if (!name) {
        throw new Error("length requires an array name.");
      }

      const items = requireArray(name);
      log(`Array ${name} length`, { length: items.length });
      return finish(items.length);
    }

    case "join": {
      const name = args[0];
      const separator = args[1] ?? ",";
      if (!name) {
        throw new Error("join requires an array name.");
      }

      const items = requireArray(name);
      const value = items.map((item) => typeof item === "string" ? item : JSON.stringify(item)).join(separator);
      log(`Array ${name} joined`, value);
      return finish(value);
    }

    case "unset": {
      if (!args[0]) {
        throw new Error("unset requires a variable name.");
      }

      delete state.variables[args[0]];
      log(`Variable ${args[0]} removed`);
      return finish({ removed: args[0] });
    }

    case "vars":
    case "variables": {
      const snapshot = { ...state.variables };
      log("Variables", snapshot);
      return finish(snapshot);
    }

    default:
      throw new Error(`Unknown command '${command}'. Try: help`);
  }
}

async function setState(nextState) {
  const handle = requireTarget();
  const result = await api(`/windows/${encodeURIComponent(handle)}/state`, {
    method: "POST",
    body: JSON.stringify({ state: nextState })
  });
  log(`Window state: ${nextState}`, result);
  return result;
}

async function capture() {
  const selectedOnly = $("capture-selected").checked;
  if (selectedOnly && !state.selectedWindow) {
    throw new Error("No selected window to capture. Select a window or use capture full.");
  }

  const payload = selectedOnly ? { handle: state.selectedWindow.handle } : {};
  const result = await api("/screenshot", {
    method: "POST",
    body: JSON.stringify(payload)
  });

  $("shot").src = `data:${result.mimeType};base64,${result.base64Image}`;
  $("shot").parentElement.classList.add("has-shot");
  log(`Captured ${result.width}x${result.height}`, { mimeType: result.mimeType });
  return result;
}

async function checkHealth() {
  try {
    await api("/health");
    $("health-dot").className = "dot ok";
    $("health-text").textContent = "ready";
  } catch (error) {
    $("health-dot").className = "dot error";
    $("health-text").textContent = "offline";
    log(error.message);
  }
}

function bind() {
  $("command-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const input = $("command-input").value.trim();
    try {
      await runCommandLine(input);
      $("command-input").select();
    } catch (error) {
      log(error.message);
    }
  });

  $("command-input").addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      $("command-form").requestSubmit();
    }
  });

  $("command-help").addEventListener("click", () => {
    $("command-help-panel").hidden = !$("command-help-panel").hidden;
    if (!$("command-help-panel").hidden) {
      showCommandHelp();
    }
  });

  $("script-run").addEventListener("click", () => runScript());
  $("script-check").addEventListener("click", () => checkScript());
  $("script-stop").addEventListener("click", () => {
    state.stopScript = true;
    state.scriptAbortController?.abort();
    $("script-status-text").textContent = "stopping";
  });
  $("script-save").addEventListener("click", saveScript);
  $("script-load-sample").addEventListener("click", () => {
    $("script-input").value = sampleScript;
    log("Loaded sample script");
  });
  $("script-clear").addEventListener("click", () => {
    $("script-input").value = "";
    log("Cleared script");
  });
  $("refresh-runs").addEventListener("click", () => refreshRuns().catch((error) => log(error.message)));
  $("clear-run-report").addEventListener("click", clearRunReport);

  $("refresh-windows").addEventListener("click", () => refreshWindows().catch((error) => log(error.message)));
  $("window-filter").addEventListener("keydown", (event) => {
    if (event.key === "Enter") {
      refreshWindows().catch((error) => log(error.message));
    }
  });

  $("use-foreground").addEventListener("click", async () => {
    try {
      const win = await api("/windows/foreground");
      selectWindow(win);
      log("Selected foreground window", win);
    } catch (error) {
      log(error.message);
    }
  });

  $("start-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      const beforeWindows = await api("/windows");
      const result = await api("/apps/start", {
        method: "POST",
        body: JSON.stringify(formJson(event.currentTarget))
      });
      log("Started app", result);
      const form = event.currentTarget;
      const win = await selectStartedWindow(result, form.elements.fileName.value, beforeWindows);
      if (win) {
        log("Selected started window", win);
        await focusSelectedTarget();
      }
      await refreshWindows();
    } catch (error) {
      log(error.message);
    }
  });

  $("focus").addEventListener("click", async () => {
    try {
      const result = await api(`/windows/${encodeURIComponent(requireTarget())}/focus`, { method: "POST" });
      log("Focused window", result);
    } catch (error) {
      log(error.message);
    }
  });

  $("restore").addEventListener("click", () => setState("restore").catch((error) => log(error.message)));
  $("minimize").addEventListener("click", () => setState("minimize").catch((error) => log(error.message)));
  $("maximize").addEventListener("click", () => setState("maximize").catch((error) => log(error.message)));

  $("close").addEventListener("click", async () => {
    try {
      const result = await api(`/windows/${encodeURIComponent(requireTarget())}/close`, { method: "POST" });
      log("Close requested", result);
      await refreshWindows();
    } catch (error) {
      log(error.message);
    }
  });

  $("move-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      const result = await api(`/windows/${encodeURIComponent(requireTarget())}/move`, {
        method: "POST",
        body: JSON.stringify(formJson(event.currentTarget))
      });
      log("Moved window", result);
      await refreshWindows();
    } catch (error) {
      log(error.message);
    }
  });

  $("keys-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await focusSelectedTarget();
      const result = await api("/input/keys", {
        method: "POST",
        body: JSON.stringify(formJson(event.currentTarget))
      });
      log("Sent keys", result);
    } catch (error) {
      log(error.message);
    }
  });

  $("text-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      await focusSelectedTarget();
      const result = await api("/input/text", {
        method: "POST",
        body: JSON.stringify(formJson(event.currentTarget))
      });
      log("Typed text", result);
    } catch (error) {
      log(error.message);
    }
  });

  $("mouse-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      const result = await api("/input/mouse", {
        method: "POST",
        body: JSON.stringify(formJson(event.currentTarget))
      });
      log("Sent mouse input", result);
    } catch (error) {
      log(error.message);
    }
  });

  $("capture").addEventListener("click", () => capture().catch((error) => log(error.message)));
  $("clear-log").addEventListener("click", () => {
    $("log").textContent = "";
  });
}

loadSavedScript();
bind();
checkHealth();
refreshWindows().catch((error) => log(error.message));
refreshRuns().catch((error) => log(error.message));
