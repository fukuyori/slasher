const state = {
  selectedWindow: null,
  screens: [],
  steps: 0,
  autoRunStarted: false
};

const $ = (id) => document.getElementById(id);
const actionButtons = ["run-all", "start-notepad", "type-text", "capture-screen", "run-data"];

function setStatus(title, detail, kind = "ready") {
  const state = $("showcase-state");
  state.classList.toggle("is-busy", kind === "busy");
  state.classList.toggle("is-error", kind === "error");
  state.querySelector("strong").textContent = title;
  state.querySelector("span").textContent = detail;
}

function setBusy(isBusy) {
  for (const id of actionButtons) {
    $(id).disabled = isBusy;
  }
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
    const error = new Error(body?.message || response.statusText);
    error.body = body;
    throw error;
  }

  return body;
}

async function post(path, body) {
  return api(path, { method: "POST", body: JSON.stringify(body) });
}

function addStep(message) {
  state.steps += 1;
  const item = document.createElement("li");
  item.textContent = `${String(state.steps).padStart(2, "0")}  ${message}`;
  $("activity-list").prepend(item);
}

async function checkHealth() {
  try {
    await api("/health");
    $("health-dot").className = "ok";
    $("health-text").textContent = "ready";
    await loadScreens();
    setStatus("Ready", "Slasher is running. The showcase can control local windows and read prepared data.");
    startAutoRunIfRequested();
  } catch (error) {
    $("health-dot").className = "error";
    $("health-text").textContent = "offline";
    setStatus("Server offline", "Start Slasher at http://127.0.0.1:5055, then reload this page.", "error");
    addStep(`Health check failed: ${error.message}`);
  }
}

async function loadScreens() {
  const screens = await api("/screens");
  state.screens = screens;
  const select = $("screen-select");
  select.textContent = "";

  const all = document.createElement("option");
  all.value = "";
  all.textContent = "Virtual screen";
  select.appendChild(all);

  for (const screen of screens) {
    const option = document.createElement("option");
    option.value = String(screen.index);
    const primary = screen.isPrimary ? " primary" : "";
    option.textContent = `${screen.index}: ${screen.deviceName}${primary} (${screen.bounds.width}x${screen.bounds.height})`;
    select.appendChild(option);
  }

  if (screens.length > 0) {
    addStep(`Detected ${screens.length} monitors`);
  }
}

function selectWindow(windowInfo) {
  state.selectedWindow = windowInfo;
  $("selected-window").textContent = `${windowInfo.title || "(no title)"}  ${windowInfo.handle}`;
}

async function refreshWindows(selectNotepad = false) {
  const windows = await api("/windows");
  const list = $("window-list");
  list.textContent = "";

  for (const win of windows.slice(0, 8)) {
    const item = document.createElement("button");
    item.type = "button";
    item.className = "window-item";
    item.innerHTML = "<strong></strong><small></small>";
    item.querySelector("strong").textContent = win.title || "(no title)";
    item.querySelector("small").textContent = `${win.processName || "unknown"}:${win.processId}`;
    item.addEventListener("click", () => selectWindow(win));
    list.appendChild(item);
  }

  if (selectNotepad) {
    const notepad = windows.find(win => (win.processName || "").toLowerCase().includes("notepad")
      || (win.title || "").toLowerCase().includes("notepad"));
    if (notepad) {
      selectWindow(notepad);
    }
  }
}

async function startNotepad() {
  setStatus("Starting Notepad", "Launching the app and searching for its window.", "busy");
  await post("/apps/start", { fileName: "notepad.exe" });
  addStep("Notepad started");
  await wait(800);
  await refreshWindows(true);
  setStatus("Notepad ready", "The target window is selected.");
}

async function typeText() {
  setStatus("Typing text", "Focusing the selected window and sending visible input.", "busy");
  if (!state.selectedWindow) {
    await refreshWindows(true);
  }

  if (!state.selectedWindow) {
    throw new Error("No window selected.");
  }

  await post(`/windows/${encodeURIComponent(state.selectedWindow.handle)}/focus`, {});
  await post("/input/text", { text: $("demo-text").value });
  addStep("Text was typed into the selected window");
  setStatus("Text sent", "The selected app should now contain the demo text.");
}

async function captureScreen() {
  setStatus("Capturing screen", "Taking a desktop screenshot for the preview board.", "busy");
  const selectedScreen = $("screen-select").value;
  const request = { maxWidth: 1440, maxHeight: 810 };
  if (selectedScreen !== "") {
    request.screenIndex = Number(selectedScreen);
  }

  const result = await post("/screenshot", request);
  $("capture-image").src = `data:${result.mimeType};base64,${result.base64Image}`;
  $("capture-image").parentElement.classList.add("has-image");
  $("capture-meta").textContent = selectedScreen === ""
    ? `${result.width} x ${result.height}`
    : `screen ${selectedScreen}, ${result.width} x ${result.height}`;
  addStep(selectedScreen === "" ? "Desktop evidence captured" : `Screen ${selectedScreen} captured`);
  setStatus("Capture displayed", "The preview board now shows the current desktop state.");
}

async function runDataDemo() {
  setStatus("Loading sample data", "Reading CSV, Excel, and JSON files prepared for the showcase.", "busy");
  const csv = await post("/data/csv/read", { path: "artifacts/demo/customers.csv", hasHeader: true });
  const excel = await post("/data/excel/read", { path: "artifacts/demo/workbook.xlsx", hasHeader: true });
  const config = await post("/data/json/query", { path: "artifacts/demo/config.json", pointer: "/source" });

  renderTable("csv-table", csv.headers, csv.rows);
  renderTable("excel-table", excel.headers, excel.rows);
  $("data-meta").textContent = `${csv.rows.length} customers, ${excel.rows.length} workbook rows, ${config.value}`;
  addStep("CSV, Excel, and JSON samples loaded");
  setStatus("Data board updated", "The visible tables now show the prepared sample files.");
}

function renderTable(id, headers, rows) {
  const table = $(id);
  const thead = table.querySelector("thead");
  const tbody = table.querySelector("tbody");
  thead.textContent = "";
  tbody.textContent = "";

  const headRow = document.createElement("tr");
  for (const header of headers) {
    const th = document.createElement("th");
    th.textContent = header;
    headRow.appendChild(th);
  }
  thead.appendChild(headRow);

  for (const row of rows) {
    const tr = document.createElement("tr");
    for (const cell of row) {
      const td = document.createElement("td");
      td.textContent = cell ?? "";
      tr.appendChild(td);
    }
    tbody.appendChild(tr);
  }
}

async function runAll() {
  setStatus("Running showcase", "The demo will launch Notepad, type text, capture the screen, and load data.", "busy");
  await startNotepad();
  await typeText();
  await captureScreen();
  await runDataDemo();
  setStatus("Showcase complete", "Visible automation and sample data are ready for presentation.");
}

function shouldAutoRun() {
  return new URLSearchParams(window.location.search).get("autorun") === "1";
}

function startAutoRunIfRequested() {
  if (!shouldAutoRun() || state.autoRunStarted) {
    return;
  }

  state.autoRunStarted = true;
  setBusy(true);
  setStatus("Auto-running showcase", "Launching the visible demo now.", "busy");
  wait(500)
    .then(runAll)
    .catch(error => {
      setStatus("Action failed", error.message, "error");
      addStep(error.message);
    })
    .finally(() => setBusy(false));
}

function wait(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function bind(id, handler) {
  $(id).addEventListener("click", async () => {
    setBusy(true);
    try {
      await handler();
    } catch (error) {
      setStatus("Action failed", error.message, "error");
      addStep(error.message);
    } finally {
      setBusy(false);
    }
  });
}

bind("run-all", runAll);
bind("start-notepad", startNotepad);
bind("type-text", typeText);
bind("capture-screen", captureScreen);
bind("run-data", runDataDemo);
$("clear-activity").addEventListener("click", () => {
  $("activity-list").textContent = "";
  state.steps = 0;
});

checkHealth();
refreshWindows(false).catch(error => addStep(`Window list failed: ${error.message}`));
