#!/usr/bin/env node

const baseUrl = (process.env.SLASHER_URL || "http://127.0.0.1:5055").replace(/\/$/, "");
let buffer = Buffer.alloc(0);

const tools = [
  {
    name: "slasher_get_status",
    description: "Check whether the local Slasher HTTP server is reachable.",
    inputSchema: {
      type: "object",
      properties: {},
      additionalProperties: false
    }
  },
  {
    name: "slasher_list_windows",
    description: "List top-level Windows windows, optionally filtering by title or process id.",
    inputSchema: {
      type: "object",
      properties: {
        title: { type: "string" },
        processId: { type: "number" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_foreground_window",
    description: "Get the current foreground window.",
    inputSchema: {
      type: "object",
      properties: {},
      additionalProperties: false
    }
  },
  {
    name: "slasher_start_app",
    description: "Start a Windows application and return its process/window metadata.",
    inputSchema: {
      type: "object",
      properties: {
        fileName: { type: "string" },
        arguments: { type: "string" },
        workingDirectory: { type: "string" },
        useShellExecute: { type: "boolean" }
      },
      required: ["fileName"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_select_app",
    description: "Select and optionally focus the best matching app window by process name or window title.",
    inputSchema: {
      type: "object",
      properties: {
        name: { type: "string" },
        match: { type: "string", enum: ["contains", "exact"] },
        focus: { type: "boolean" }
      },
      required: ["name"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_focus_window",
    description: "Focus a window by handle.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" }
      },
      required: ["handle"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_set_window_state",
    description: "Set a window state: hide, show, minimize, maximize, or restore.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        state: { type: "string", enum: ["hide", "show", "minimize", "maximize", "restore"] }
      },
      required: ["handle", "state"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_move_window",
    description: "Move or resize a window by handle.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        x: { type: "number" },
        y: { type: "number" },
        width: { type: "number" },
        height: { type: "number" }
      },
      required: ["handle", "x", "y", "width", "height"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_close_window",
    description: "Request that a window close.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" }
      },
      required: ["handle"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_send_keys",
    description: "Send a key chord such as CTRL+S to the current foreground app.",
    inputSchema: {
      type: "object",
      properties: {
        keys: { type: "string" },
        delayMs: { type: "number" }
      },
      required: ["keys"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_type_text",
    description: "Type text into the current foreground app.",
    inputSchema: {
      type: "object",
      properties: {
        text: { type: "string" },
        delayMs: { type: "number" }
      },
      required: ["text"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_mouse",
    description: "Send mouse input. Actions: move, click, doubleclick, down, up, wheel.",
    inputSchema: {
      type: "object",
      properties: {
        action: { type: "string", enum: ["move", "click", "doubleclick", "down", "up", "wheel"] },
        x: { type: "number" },
        y: { type: "number" },
        button: { type: "string", enum: ["left", "right", "middle"] },
        wheelDelta: { type: "number" }
      },
      required: ["action"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_drag_mouse",
    description: "Drag and drop from one screen coordinate to another.",
    inputSchema: {
      type: "object",
      properties: {
        fromX: { type: "number" },
        fromY: { type: "number" },
        toX: { type: "number" },
        toY: { type: "number" },
        button: { type: "string", enum: ["left", "right", "middle"] },
        durationMs: { type: "number" },
        steps: { type: "number" }
      },
      required: ["fromX", "fromY", "toX", "toY"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_context_menu",
    description: "Open a context menu at a screen coordinate and return a screenshot observation.",
    inputSchema: {
      type: "object",
      properties: {
        x: { type: "number" },
        y: { type: "number" },
        delayMs: { type: "number" }
      },
      required: ["x", "y"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_capture",
    description: "Capture the full virtual desktop or a specific window. Returns an MCP image content item.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_match_image",
    description: "Search the selected/full screen for a BMP template image and return match score and bounds.",
    inputSchema: {
      type: "object",
      properties: {
        templatePath: { type: "string" },
        handle: { type: "string" },
        threshold: { type: "number" },
        maxWidth: { type: "number" },
        maxHeight: { type: "number" },
        step: { type: "number" }
      },
      required: ["templatePath"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_open",
    description: "Open a Selenium WebDriver browser session for edge, chrome, or firefox.",
    inputSchema: {
      type: "object",
      properties: {
        browser: { type: "string", enum: ["edge", "chrome", "firefox"] },
        url: { type: "string" },
        headless: { type: "boolean" },
        downloadDirectory: { type: "string" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_find",
    description: "Find a browser DOM element using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_title",
    description: "Read the current WebDriver browser title.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_url",
    description: "Read the current WebDriver browser URL.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_click",
    description: "Click a browser DOM element using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_hover",
    description: "Move the WebDriver pointer over a browser DOM element.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_double_click",
    description: "Double-click a browser DOM element using WebDriver actions.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_right_click",
    description: "Right-click a browser DOM element using WebDriver actions.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_clear",
    description: "Clear a browser DOM element using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_submit",
    description: "Submit a browser form element using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_type",
    description: "Type text into a browser DOM element using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: {
        ...browserSelectorProperties(),
        text: { type: "string" },
        clear: { type: "boolean" }
      },
      required: ["using", "value", "text"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_press",
    description: "Send keys to the active browser page or a selected DOM element. Examples: ENTER, TAB, CTRL+A.",
    inputSchema: {
      type: "object",
      properties: {
        keys: { type: "string" },
        using: { type: "string", enum: ["css", "xpath", "id", "name", "tag", "class", "link", "partialLink"] },
        value: { type: "string" },
        sessionId: { type: "string" },
        timeoutMs: { type: "number" }
      },
      required: ["keys"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_upload",
    description: "Set a local file path on an input[type=file] element.",
    inputSchema: {
      type: "object",
      properties: {
        ...browserSelectorProperties(),
        path: { type: "string" }
      },
      required: ["using", "value", "path"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_drag",
    description: "Drag one DOM element and drop it on another DOM element.",
    inputSchema: {
      type: "object",
      properties: {
        using: { type: "string", enum: ["css", "xpath", "id", "name", "tag", "class", "link", "partialLink"] },
        value: { type: "string" },
        targetUsing: { type: "string", enum: ["css", "xpath", "id", "name", "tag", "class", "link", "partialLink"] },
        targetValue: { type: "string" },
        sessionId: { type: "string" },
        timeoutMs: { type: "number" }
      },
      required: ["using", "value", "targetUsing", "targetValue"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_select_option",
    description: "Select an option from a select element by text, value, or index.",
    inputSchema: {
      type: "object",
      properties: {
        ...browserSelectorProperties(),
        selectBy: { type: "string", enum: ["text", "value", "index"] },
        option: { type: "string" },
        clear: { type: "boolean" }
      },
      required: ["using", "value", "selectBy", "option"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_selected_options",
    description: "Read selected options from a select element.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_wait_download",
    description: "Wait for a completed file download in a local directory.",
    inputSchema: {
      type: "object",
      properties: {
        directory: { type: "string" },
        pattern: { type: "string" },
        timeoutMs: { type: "number" },
        stableMs: { type: "number" }
      },
      required: ["directory"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_logs",
    description: "Read WebDriver browser logs, including console messages where supported.",
    inputSchema: {
      type: "object",
      properties: {
        sessionId: { type: "string" },
        type: { type: "string" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_text",
    description: "Read DOM element text using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_attribute",
    description: "Read a DOM element attribute using Selenium-style selector strategies.",
    inputSchema: {
      type: "object",
      properties: {
        ...browserSelectorProperties(),
        attribute: { type: "string" }
      },
      required: ["using", "value", "attribute"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_wait",
    description: "Wait for a browser DOM element to become visible.",
    inputSchema: {
      type: "object",
      properties: browserSelectorProperties(),
      required: ["using", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_wait_text",
    description: "Wait until a browser DOM element's text matches an expected value.",
    inputSchema: {
      type: "object",
      properties: {
        ...browserSelectorProperties(),
        text: { type: "string" },
        match: { type: "string", enum: ["contains", "exact", "startsWith", "endsWith"] }
      },
      required: ["using", "value", "text"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_js",
    description: "Execute JavaScript in the current WebDriver browser session.",
    inputSchema: {
      type: "object",
      properties: {
        script: { type: "string" },
        sessionId: { type: "string" }
      },
      required: ["script"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_cookies",
    description: "List cookies in the current WebDriver browser session.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_storage_get",
    description: "Read localStorage or sessionStorage in the current browser session.",
    inputSchema: {
      type: "object",
      properties: {
        storage: { type: "string", enum: ["local", "session"] },
        key: { type: "string" },
        sessionId: { type: "string" }
      },
      required: ["storage", "key"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_storage_set",
    description: "Write localStorage or sessionStorage in the current browser session.",
    inputSchema: {
      type: "object",
      properties: {
        storage: { type: "string", enum: ["local", "session"] },
        key: { type: "string" },
        value: { type: "string" },
        sessionId: { type: "string" }
      },
      required: ["storage", "key", "value"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_screenshot",
    description: "Capture the current WebDriver browser viewport as PNG.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_links",
    description: "List links from the current WebDriver browser page.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_windows",
    description: "List WebDriver browser window/tab handles.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_new_window",
    description: "Open a new WebDriver browser tab or window.",
    inputSchema: {
      type: "object",
      properties: {
        type: { type: "string", enum: ["tab", "window"] },
        url: { type: "string" },
        sessionId: { type: "string" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_switch_window",
    description: "Switch to a WebDriver browser window/tab by handle or index.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        index: { type: "number" },
        sessionId: { type: "string" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_close_window",
    description: "Close the current WebDriver browser tab/window and switch to the first remaining handle.",
    inputSchema: {
      type: "object",
      properties: { sessionId: { type: "string" } },
      additionalProperties: false
    }
  },
  {
    name: "slasher_browser_close",
    description: "Close the current or specified Selenium WebDriver browser session.",
    inputSchema: {
      type: "object",
      properties: {
        sessionId: { type: "string" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_element_tree",
    description: "Read the foreground or specified window's native child window/control tree with handles, titles, class names, control ids, and bounds.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        maxDepth: { type: "number" },
        maxChildren: { type: "number" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_find_elements",
    description: "Find native child window/control elements by title, class name, or control id.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        title: { type: "string" },
        className: { type: "string" },
        controlId: { type: "number" },
        match: { type: "string", enum: ["contains", "exact"] },
        maxDepth: { type: "number" },
        maxResults: { type: "number" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_click_element",
    description: "Find the first matching native child control and click its center point.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        title: { type: "string" },
        className: { type: "string" },
        controlId: { type: "number" },
        match: { type: "string", enum: ["contains", "exact"] },
        maxDepth: { type: "number" },
        button: { type: "string", enum: ["left", "right", "middle"] }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_element_exists",
    description: "Return whether a native child window/control exists by title, class name, or control id.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        title: { type: "string" },
        className: { type: "string" },
        controlId: { type: "number" },
        match: { type: "string", enum: ["contains", "exact"] },
        maxDepth: { type: "number" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_element_text",
    description: "Return the title/text of the first matching native child window/control.",
    inputSchema: {
      type: "object",
      properties: {
        handle: { type: "string" },
        title: { type: "string" },
        className: { type: "string" },
        controlId: { type: "number" },
        match: { type: "string", enum: ["contains", "exact"] },
        maxDepth: { type: "number" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_check_script",
    description: "Validate an inline Slasher script without executing GUI actions. Use this before running generated or edited scripts.",
    inputSchema: {
      type: "object",
      properties: {
        script: { type: "string" }
      },
      required: ["script"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_check_script_file",
    description: "Validate a .slasher script file inside the Slasher workspace without executing GUI actions.",
    inputSchema: {
      type: "object",
      properties: {
        path: { type: "string" }
      },
      required: ["path"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_run_script",
    description: "Run a Slasher command script through the local server and return the structured run report, assertions, artifact paths, and screenshot evidence.",
    inputSchema: {
      type: "object",
      properties: {
        script: { type: "string" },
        stopOnError: { type: "boolean" }
      },
      required: ["script"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_run_script_file",
    description: "Run a .slasher script file inside the Slasher workspace and return the structured run report and evidence.",
    inputSchema: {
      type: "object",
      properties: {
        path: { type: "string" },
        stopOnError: { type: "boolean" }
      },
      required: ["path"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_list_runs",
    description: "List recent Slasher automation runs so an AI agent can recover prior reports after interruption.",
    inputSchema: {
      type: "object",
      properties: {
        limit: { type: "number" }
      },
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_run",
    description: "Fetch a Slasher automation run report, optionally including its event timeline.",
    inputSchema: {
      type: "object",
      properties: {
        runId: { type: "string" },
        includeEvents: { type: "boolean" }
      },
      required: ["runId"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_run_log",
    description: "Fetch logs/script.log for a Slasher automation run.",
    inputSchema: {
      type: "object",
      properties: {
        runId: { type: "string" }
      },
      required: ["runId"],
      additionalProperties: false
    }
  },
  {
    name: "slasher_get_artifact",
    description: "Fetch a run artifact by path. Images are returned as MCP image content; text artifacts are returned as text.",
    inputSchema: {
      type: "object",
      properties: {
        runId: { type: "string" },
        path: { type: "string" }
      },
      required: ["runId", "path"],
      additionalProperties: false
    }
  }
];

process.stdin.on("data", (chunk) => {
  buffer = Buffer.concat([buffer, chunk]);
  processMessages();
});

async function processMessages() {
  while (true) {
    const headerEnd = buffer.indexOf("\r\n\r\n");
    if (headerEnd < 0) {
      return;
    }

    const header = buffer.subarray(0, headerEnd).toString("utf8");
    const match = /^Content-Length:\s*(\d+)$/im.exec(header);
    if (!match) {
      buffer = Buffer.alloc(0);
      return;
    }

    const length = Number(match[1]);
    const bodyStart = headerEnd + 4;
    const bodyEnd = bodyStart + length;
    if (buffer.length < bodyEnd) {
      return;
    }

    const body = buffer.subarray(bodyStart, bodyEnd).toString("utf8");
    buffer = buffer.subarray(bodyEnd);

    let message;
    try {
      message = JSON.parse(body);
    } catch (error) {
      continue;
    }

    handleMessage(message).catch((error) => {
      if (message.id !== undefined) {
        sendError(message.id, -32603, error.message);
      }
    });
  }
}

async function handleMessage(message) {
  if (message.id === undefined) {
    return;
  }

  switch (message.method) {
    case "initialize":
      sendResult(message.id, {
        protocolVersion: message.params?.protocolVersion || "2024-11-05",
        capabilities: {
          tools: {}
        },
        serverInfo: {
          name: "slasher-mcp",
          version: "0.1.0"
        }
      });
      return;

    case "ping":
      sendResult(message.id, {});
      return;

    case "tools/list":
      sendResult(message.id, { tools });
      return;

    case "tools/call":
      sendResult(message.id, await callTool(message.params?.name, message.params?.arguments || {}));
      return;

    default:
      sendError(message.id, -32601, `Unknown method: ${message.method}`);
  }
}

async function callTool(name, args) {
  switch (name) {
    case "slasher_get_status":
      return textResult(await getJson("/health"));

    case "slasher_list_windows": {
      const params = new URLSearchParams();
      if (args.title) params.set("title", args.title);
      if (args.processId !== undefined) params.set("processId", String(args.processId));
      const suffix = params.toString() ? `?${params}` : "";
      return textResult(await getJson(`/windows${suffix}`));
    }

    case "slasher_get_foreground_window":
      return textResult(await getJson("/windows/foreground"));

    case "slasher_start_app":
      return textResult(await postJson("/apps/start", {
        fileName: args.fileName,
        arguments: args.arguments,
        workingDirectory: args.workingDirectory,
        useShellExecute: args.useShellExecute ?? true
      }));

    case "slasher_select_app":
      return textResult(await postJson("/apps/select", {
        name: args.name,
        match: args.match || "contains",
        focus: args.focus ?? true
      }));

    case "slasher_focus_window":
      return textResult(await postJson(`/windows/${encodeURIComponent(args.handle)}/focus`, {}));

    case "slasher_set_window_state":
      return textResult(await postJson(`/windows/${encodeURIComponent(args.handle)}/state`, { state: args.state }));

    case "slasher_move_window":
      return textResult(await postJson(`/windows/${encodeURIComponent(args.handle)}/move`, {
        x: args.x,
        y: args.y,
        width: args.width,
        height: args.height
      }));

    case "slasher_close_window":
      return textResult(await postJson(`/windows/${encodeURIComponent(args.handle)}/close`, {}));

    case "slasher_send_keys":
      return textResult(await postJson("/input/keys", { keys: args.keys, delayMs: args.delayMs || 0 }));

    case "slasher_type_text":
      return textResult(await postJson("/input/text", { text: args.text, delayMs: args.delayMs || 0 }));

    case "slasher_mouse":
      return textResult(await postJson("/input/mouse", {
        action: args.action,
        x: args.x,
        y: args.y,
        button: args.button || "left",
        wheelDelta: args.wheelDelta || 0
      }));

    case "slasher_drag_mouse":
      return textResult(await postJson("/input/mouse/drag", {
        fromX: args.fromX,
        fromY: args.fromY,
        toX: args.toX,
        toY: args.toY,
        button: args.button || "left",
        durationMs: args.durationMs ?? 400,
        steps: args.steps ?? 24
      }));

    case "slasher_get_context_menu": {
      const result = await postJson("/input/mouse/context-menu", {
        x: args.x,
        y: args.y,
        delayMs: args.delayMs ?? 250
      });
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({
              x: result.x,
              y: result.y,
              foregroundWindow: result.foregroundWindow,
              observation: result.observation,
              screenshot: {
                mimeType: result.screenshot.mimeType,
                width: result.screenshot.width,
                height: result.screenshot.height
              }
            }, null, 2)
          },
          {
            type: "image",
            data: result.screenshot.base64Image,
            mimeType: result.screenshot.mimeType
          }
        ]
      };
    }

    case "slasher_capture": {
      const shot = await postJson("/screenshot", { handle: args.handle });
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({ mimeType: shot.mimeType, width: shot.width, height: shot.height }, null, 2)
          },
          {
            type: "image",
            data: shot.base64Image,
            mimeType: shot.mimeType
          }
        ]
      };
    }

    case "slasher_match_image":
      return textResult(await postJson("/screen/image-match", {
        templatePath: args.templatePath,
        handle: args.handle,
        threshold: args.threshold ?? 0.98,
        maxWidth: args.maxWidth,
        maxHeight: args.maxHeight,
        step: args.step ?? 1
      }));

    case "slasher_browser_open":
      return textResult(await postJson("/browser/open", {
        browser: args.browser || "edge",
        url: args.url || "about:blank",
        headless: args.headless || false,
        downloadDirectory: args.downloadDirectory
      }));

    case "slasher_browser_find":
      return textResult(await postJson("/browser/find", browserSelectorBody(args)));

    case "slasher_browser_title": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await getJson(`/browser/title${suffix}`));
    }

    case "slasher_browser_url": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await getJson(`/browser/url${suffix}`));
    }

    case "slasher_browser_click":
      return textResult(await postJson("/browser/click", browserSelectorBody(args)));

    case "slasher_browser_hover":
      return textResult(await postJson("/browser/hover", browserSelectorBody(args)));

    case "slasher_browser_double_click":
      return textResult(await postJson("/browser/double-click", browserSelectorBody(args)));

    case "slasher_browser_right_click":
      return textResult(await postJson("/browser/right-click", browserSelectorBody(args)));

    case "slasher_browser_type":
      return textResult(await postJson("/browser/type", {
        ...browserSelectorBody(args),
        text: args.text || "",
        clear: args.clear ?? true
      }));

    case "slasher_browser_press":
      return textResult(await postJson("/browser/press", {
        keys: args.keys,
        using: args.using,
        value: args.value,
        sessionId: args.sessionId,
        timeoutMs: args.timeoutMs ?? 5000
      }));

    case "slasher_browser_upload":
      return textResult(await postJson("/browser/upload", {
        ...browserSelectorBody(args),
        path: args.path
      }));

    case "slasher_browser_drag":
      return textResult(await postJson("/browser/drag", {
        using: args.using || "css",
        value: args.value,
        targetUsing: args.targetUsing || "css",
        targetValue: args.targetValue,
        sessionId: args.sessionId,
        timeoutMs: args.timeoutMs ?? 5000
      }));

    case "slasher_browser_select_option":
      return textResult(await postJson("/browser/select-option", {
        ...browserSelectorBody(args),
        selectBy: args.selectBy || "text",
        option: args.option,
        clear: args.clear || false
      }));

    case "slasher_browser_selected_options":
      return textResult(await postJson("/browser/selected-options", browserSelectorBody(args)));

    case "slasher_browser_wait_download":
      return textResult(await postJson("/browser/downloads/wait", {
        directory: args.directory,
        pattern: args.pattern || "*",
        timeoutMs: args.timeoutMs ?? 30000,
        stableMs: args.stableMs ?? 500
      }));

    case "slasher_browser_logs": {
      const params = new URLSearchParams();
      if (args.sessionId) params.set("sessionId", args.sessionId);
      if (args.type) params.set("type", args.type);
      const suffix = params.toString() ? `?${params}` : "";
      return textResult(await getJson(`/browser/logs${suffix}`));
    }

    case "slasher_browser_clear":
      return textResult(await postJson("/browser/clear", browserSelectorBody(args)));

    case "slasher_browser_submit":
      return textResult(await postJson("/browser/submit", browserSelectorBody(args)));

    case "slasher_browser_text":
      return textResult(await postJson("/browser/text", browserSelectorBody(args)));

    case "slasher_browser_attribute":
      return textResult(await postJson("/browser/attribute", {
        ...browserSelectorBody(args),
        attribute: args.attribute
      }));

    case "slasher_browser_wait":
      return textResult(await postJson("/browser/wait", browserSelectorBody(args)));

    case "slasher_browser_wait_text":
      return textResult(await postJson("/browser/wait-text", {
        ...browserSelectorBody(args),
        text: args.text || "",
        match: args.match || "contains"
      }));

    case "slasher_browser_js":
      return textResult(await postJson("/browser/js", {
        script: args.script,
        sessionId: args.sessionId
      }));

    case "slasher_browser_cookies": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await getJson(`/browser/cookies${suffix}`));
    }

    case "slasher_browser_storage_get":
      return textResult(await postJson(`/browser/storage/${encodeURIComponent(args.storage || "local")}/get`, {
        key: args.key,
        sessionId: args.sessionId
      }));

    case "slasher_browser_storage_set":
      return textResult(await postJson(`/browser/storage/${encodeURIComponent(args.storage || "local")}/set`, {
        key: args.key,
        value: args.value || "",
        sessionId: args.sessionId
      }));

    case "slasher_browser_screenshot": {
      const shot = await postJson("/browser/screenshot", { sessionId: args.sessionId });
      return {
        content: [
          {
            type: "text",
            text: JSON.stringify({ mimeType: shot.mimeType, width: shot.width, height: shot.height }, null, 2)
          },
          {
            type: "image",
            data: shot.base64Image,
            mimeType: shot.mimeType
          }
        ]
      };
    }

    case "slasher_browser_links": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await getJson(`/browser/links${suffix}`));
    }

    case "slasher_browser_windows": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await getJson(`/browser/windows${suffix}`));
    }

    case "slasher_browser_new_window":
      return textResult(await postJson("/browser/new-window", {
        type: args.type || "tab",
        url: args.url,
        sessionId: args.sessionId
      }));

    case "slasher_browser_switch_window":
      return textResult(await postJson("/browser/switch-window", {
        handle: args.handle,
        index: args.index,
        sessionId: args.sessionId
      }));

    case "slasher_browser_close_window": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await postJson(`/browser/close-window${suffix}`, {}));
    }

    case "slasher_browser_close": {
      const suffix = args.sessionId ? `?sessionId=${encodeURIComponent(args.sessionId)}` : "";
      return textResult(await postJson(`/browser/close${suffix}`, {}));
    }

    case "slasher_get_element_tree": {
      const params = new URLSearchParams();
      if (args.handle) params.set("handle", args.handle);
      if (args.maxDepth !== undefined) params.set("maxDepth", String(args.maxDepth));
      if (args.maxChildren !== undefined) params.set("maxChildren", String(args.maxChildren));
      const suffix = params.toString() ? `?${params}` : "";
      return textResult(await getJson(`/elements/tree${suffix}`));
    }

    case "slasher_find_elements": {
      const params = elementSearchParams(args);
      const suffix = params.toString() ? `?${params}` : "";
      return textResult(await getJson(`/elements/find${suffix}`));
    }

    case "slasher_click_element":
      return textResult(await postJson("/elements/click", {
        handle: args.handle,
        title: args.title,
        className: args.className,
        controlId: args.controlId,
        match: args.match || "contains",
        maxDepth: args.maxDepth ?? 8,
        button: args.button || "left"
      }));

    case "slasher_element_exists": {
      const params = elementSearchParams(args);
      const suffix = params.toString() ? `?${params}` : "";
      return textResult(await getJson(`/elements/exists${suffix}`));
    }

    case "slasher_get_element_text": {
      const params = elementSearchParams(args);
      const suffix = params.toString() ? `?${params}` : "";
      return textResult(await getJson(`/elements/text${suffix}`));
    }

    case "slasher_check_script": {
      const check = await postJsonAllowError("/scripts/check", {
        script: args.script
      });
      return checkScriptResult(check);
    }

    case "slasher_check_script_file": {
      const check = await postJsonAllowError("/scripts/check", {
        path: args.path
      });
      return checkScriptResult(check);
    }

    case "slasher_run_script": {
      const run = await postJsonAllowError("/scripts/run", {
        script: args.script,
        stopOnError: args.stopOnError ?? true,
        name: "mcp-script-run"
      });
      return runScriptResult(run);
    }

    case "slasher_run_script_file": {
      const run = await postJsonAllowError("/scripts/run-file", {
        path: args.path,
        stopOnError: args.stopOnError ?? true,
        name: "mcp-script-file-run"
      });
      return runScriptResult(run);
    }

    case "slasher_list_runs": {
      const limit = Number.isFinite(args.limit) ? Math.trunc(args.limit) : 20;
      const response = await getJson(`/automation/runs?limit=${encodeURIComponent(limit)}`);
      return textResult({
        count: response.runs?.length ?? 0,
        runs: (response.runs || []).map(summarizeRunReport)
      });
    }

    case "slasher_get_run": {
      const run = await getJson(`/automation/runs/${encodeURIComponent(args.runId)}`);
      if (args.includeEvents === false) {
        return textResult({ run: summarizeRunReport(run), fullRun: run });
      }

      const events = await getJson(`/automation/runs/${encodeURIComponent(args.runId)}/events`);
      return textResult({
        run: summarizeRunReport(run),
        events: events.events || [],
        nextInspection: buildInspectionHints(run)
      });
    }

    case "slasher_get_run_log": {
      return textResult(await getText(`/automation/runs/${encodeURIComponent(args.runId)}/logs/script`));
    }

    case "slasher_get_artifact": {
      return artifactResult(args.runId, args.path);
    }

    default:
      throw new Error(`Unknown tool: ${name}`);
  }
}

function checkScriptResult(check) {
  const summary = {
    ok: check.ok,
    lineCount: check.lines?.length ?? 0,
    diagnostics: check.diagnostics || []
  };

  return {
    content: [
      {
        type: "text",
        text: JSON.stringify({
          summary,
          check
        }, null, 2)
      }
    ],
    isError: !check.ok
  };
}

async function runScriptResult(run) {
  const evidence = findMostRelevantEvidence(run);
  const summary = buildRunSummary(run, evidence);
  const content = [
    {
      type: "text",
      text: `${formatRunSummary(summary)}\n\nFull JSON:\n${JSON.stringify(run, null, 2)}`
    }
  ];

  if (evidence?.path && evidence?.mimeType?.startsWith("image/")) {
    try {
      const artifact = await getJson(`/automation/runs/${encodeURIComponent(run.run.runId)}/artifacts/content?path=${encodeURIComponent(evidence.path)}`);
      content.push({
        type: "image",
        data: artifact.base64Content,
        mimeType: artifact.mimeType
      });
    } catch (error) {
      content[0].text = `${content[0].text}\n\nFailed to load evidence image: ${error.message}`;
    }
  }

  return { content };
}

function buildRunSummary(run, evidence) {
  const report = run.run || {};
  const error = run.error || report.error || null;
  const failedEvent = findFailedEvent(run);
  return {
    ok: !!run.ok,
    status: report.status,
    runId: report.runId,
    name: report.name,
    eventCount: run.events?.length ?? report.eventCount ?? 0,
    failedEventSequence: report.failedEventSequence ?? failedEvent?.sequence ?? null,
    artifactRoot: report.artifactRoot,
    artifacts: {
      run: report.artifacts?.run,
      events: report.artifacts?.events,
      summary: report.artifacts?.summary,
      report: report.artifacts?.report,
      screenshots: report.artifacts?.screenshots,
      logs: report.artifacts?.logs,
      scriptLog: report.artifacts?.scriptLog || (report.artifacts?.logs ? `${report.artifacts.logs}/script.log` : undefined),
      attachments: report.artifacts?.attachments
    },
    error: error
      ? {
          code: error.code,
          message: error.message,
          action: error.action,
          source: error.source,
          expected: error.expected,
          actual: error.actual,
          diagnostics: error.details?.diagnostics || [],
          selectedWindow: error.details?.selectedWindow || null,
          foregroundWindow: error.details?.foregroundWindow || null
        }
      : null,
    failedEvent: failedEvent
      ? {
          sequence: failedEvent.sequence,
          action: failedEvent.action,
          step: failedEvent.step,
          source: failedEvent.source,
          error: failedEvent.error
            ? {
                code: failedEvent.error.code,
                message: failedEvent.error.message
              }
            : null
        }
      : null,
    mostRelevantEvidence: evidence || null,
    nextInspection: [
      report.artifacts?.report ? `Open ${report.artifacts.report} for the HTML timeline.` : null,
      report.artifacts?.summary ? `Read ${report.artifacts.summary} for the compact text summary.` : null,
      (report.artifacts?.scriptLog || report.artifacts?.logs) ? `Read ${report.artifacts?.scriptLog || `${report.artifacts.logs}/script.log`} when logs or agent notes are relevant.` : null,
      evidence?.path ? `Inspect ${evidence.path} for the most relevant visual evidence.` : null
    ].filter(Boolean)
  };
}

function formatRunSummary(summary) {
  const lines = [
    `Slasher script run: ${summary.status || "unknown"} (${summary.ok ? "ok" : "failed"})`,
    `Run ID: ${summary.runId || "-"}`,
    `Events: ${summary.eventCount}`,
    `Artifacts:`,
    `- run: ${summary.artifacts.run || "-"}`,
    `- events: ${summary.artifacts.events || "-"}`,
    `- summary: ${summary.artifacts.summary || "-"}`,
    `- report: ${summary.artifacts.report || "-"}`,
    `- screenshots: ${summary.artifacts.screenshots || "-"}`,
    `- logs: ${summary.artifacts.logs || "-"}`,
    `- scriptLog: ${summary.artifacts.scriptLog || "-"}`,
    `- attachments: ${summary.artifacts.attachments || "-"}`
  ];

  if (summary.error) {
    lines.push(
      "",
      `Error: ${summary.error.code}: ${summary.error.message}`,
      `Action: ${summary.error.action || "-"}`,
      `Source: ${formatSource(summary.error.source)}`
    );

    if (summary.error.diagnostics?.length > 0) {
      lines.push("", "Diagnostics:");
      for (const diagnostic of summary.error.diagnostics) {
        lines.push(`- ${diagnostic.code || "diagnostic"}: ${diagnostic.message || ""}`);
      }
    }

    const selected = formatWindowSummary(summary.error.selectedWindow);
    const foreground = formatWindowSummary(summary.error.foregroundWindow);
    if (selected || foreground) {
      lines.push(
        "",
        `Selected Window: ${selected || "-"}`,
        `Foreground Window: ${foreground || "-"}`
      );
    }
  }

  if (summary.failedEvent) {
    lines.push(
      "",
      `Failed Event: #${summary.failedEvent.sequence} ${summary.failedEvent.action || "-"}`,
      `Step: ${summary.failedEvent.step || "-"}`,
      `Event Source: ${formatSource(summary.failedEvent.source)}`
    );
  }

  if (summary.mostRelevantEvidence) {
    lines.push(
      "",
      `Most Relevant Evidence: ${summary.mostRelevantEvidence.kind}:${summary.mostRelevantEvidence.role}`,
      `Evidence Path: ${summary.mostRelevantEvidence.path}`,
      `Evidence MIME: ${summary.mostRelevantEvidence.mimeType || "-"}`
    );
  }

  if (summary.nextInspection.length > 0) {
    lines.push("", "Next Inspection:");
    for (const item of summary.nextInspection) {
      lines.push(`- ${item}`);
    }
  }

  return lines.join("\n");
}

function summarizeRunReport(report) {
  return {
    status: report.status,
    runId: report.runId,
    name: report.name,
    mode: report.mode,
    entryPoint: report.entryPoint,
    startedAt: report.startedAt,
    endedAt: report.endedAt,
    durationMs: report.durationMs,
    eventCount: report.eventCount,
    failedEventSequence: report.failedEventSequence,
    artifactRoot: report.artifactRoot,
    artifacts: {
      run: report.artifacts?.run,
      events: report.artifacts?.events,
      summary: report.artifacts?.summary,
      report: report.artifacts?.report,
      logs: report.artifacts?.logs,
      scriptLog: report.artifacts?.scriptLog || (report.artifacts?.logs ? `${report.artifacts.logs}/script.log` : undefined),
      screenshots: report.artifacts?.screenshots,
      attachments: report.artifacts?.attachments
    },
    error: report.error
      ? {
          code: report.error.code,
          message: report.error.message,
          action: report.error.action,
          source: report.error.source,
          diagnostics: report.error.details?.diagnostics || []
        }
      : null
  };
}

function elementSearchParams(args) {
  const params = new URLSearchParams();
  if (args.handle) params.set("handle", args.handle);
  if (args.title) params.set("title", args.title);
  if (args.className) params.set("className", args.className);
  if (args.controlId !== undefined) params.set("controlId", String(args.controlId));
  if (args.match) params.set("match", args.match);
  if (args.maxDepth !== undefined) params.set("maxDepth", String(args.maxDepth));
  if (args.maxResults !== undefined) params.set("maxResults", String(args.maxResults));
  return params;
}

function browserSelectorProperties() {
  return {
    using: { type: "string", enum: ["css", "xpath", "id", "name", "tag", "class", "link", "partialLink"] },
    value: { type: "string" },
    sessionId: { type: "string" },
    timeoutMs: { type: "number" }
  };
}

function browserSelectorBody(args) {
  return {
    using: args.using || "css",
    value: args.value,
    sessionId: args.sessionId,
    timeoutMs: args.timeoutMs ?? 5000
  };
}

function buildInspectionHints(report) {
  return [
    report.artifacts?.report ? `Open ${report.artifacts.report} or GET /automation/runs/${report.runId}/report for the HTML timeline.` : null,
    report.artifacts?.summary ? `Read ${report.artifacts.summary} or call slasher_get_artifact with path ${report.artifacts.summary}.` : null,
    report.artifacts?.scriptLog ? `Read ${report.artifacts.scriptLog} or call slasher_get_run_log.` : null,
    report.failedEventSequence ? `Inspect failed event #${report.failedEventSequence}.` : null
  ].filter(Boolean);
}

async function artifactResult(runId, path) {
  const artifact = await getJson(`/automation/runs/${encodeURIComponent(runId)}/artifacts/content?path=${encodeURIComponent(path)}`);
  const metadata = {
    path: artifact.path,
    mimeType: artifact.mimeType,
    length: artifact.length
  };

  if (artifact.mimeType?.startsWith("image/")) {
    return {
      content: [
        {
          type: "text",
          text: JSON.stringify(metadata, null, 2)
        },
        {
          type: "image",
          data: artifact.base64Content,
          mimeType: artifact.mimeType
        }
      ]
    };
  }

  const decoded = Buffer.from(artifact.base64Content, "base64").toString("utf8");
  return {
    content: [
      {
        type: "text",
        text: `${JSON.stringify(metadata, null, 2)}\n\n${decoded}`
      }
    ]
  };
}

function formatSource(source) {
  if (!source) {
    return "-";
  }

  const file = source.file || "unknown";
  const line = source.line == null ? "" : `:${source.line}`;
  const func = source.function ? `#${source.function}` : "";
  return `${file}${line}${func}`;
}

function formatWindowSummary(window) {
  if (!window) {
    return null;
  }

  const title = window.title || "(no title)";
  const process = window.processName || "?";
  const handle = window.handle || "?";
  return `${title} [${process} ${handle}]`;
}

function findFailedEvent(run) {
  return (run.events || []).find((event) => event.ok === false) || null;
}

function findMostRelevantEvidence(run) {
  const errorEvidence = findBestScreenshot(run.error?.evidence);
  if (errorEvidence) {
    return errorEvidence;
  }

  for (const event of [...(run.events || [])].reverse()) {
    const evidence = findBestScreenshot(event.evidence);
    if (evidence) {
      return evidence;
    }
  }

  return null;
}

function findBestScreenshot(evidence) {
  const screenshots = (evidence || []).filter((item) => item.kind === "screenshot");
  return screenshots.find((item) => item.role?.endsWith("-preview")) || screenshots[0] || null;
}

async function getJson(path) {
  return requestJson("GET", path);
}

async function getText(path) {
  const response = await fetch(`${baseUrl}${path}`, {
    method: "GET",
    headers: authHeaders()
  });

  const text = await response.text();
  if (!response.ok) {
    let message = text;
    try {
      const data = text ? JSON.parse(text) : null;
      message = data?.message || data?.title || text;
    } catch {
      // Keep the raw response text for non-JSON errors.
    }

    throw new Error(message || `GET ${path} failed with ${response.status}`);
  }

  return text;
}

async function postJson(path, body) {
  return requestJson("POST", path, body);
}

async function postJsonAllowError(path, body) {
  return requestJson("POST", path, body, { allowError: true });
}

async function requestJson(method, path, body, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, {
    method,
    headers: {
      "Content-Type": "application/json",
      ...authHeaders()
    },
    body: body === undefined || method === "GET" ? undefined : JSON.stringify(body)
  });

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok && !options.allowError) {
    throw new Error(data?.message || data?.title || `${method} ${path} failed with ${response.status}`);
  }

  return data;
}

function authHeaders() {
  return process.env.SLASHER_API_KEY
    ? { Authorization: `Bearer ${process.env.SLASHER_API_KEY}` }
    : {};
}

function textResult(value) {
  return {
    content: [
      {
        type: "text",
        text: typeof value === "string" ? value : JSON.stringify(value, null, 2)
      }
    ]
  };
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function sendResult(id, result) {
  send({ jsonrpc: "2.0", id, result });
}

function sendError(id, code, message) {
  send({ jsonrpc: "2.0", id, error: { code, message } });
}

function send(message) {
  const body = JSON.stringify(message);
  process.stdout.write(`Content-Length: ${Buffer.byteLength(body, "utf8")}\r\n\r\n${body}`);
}
