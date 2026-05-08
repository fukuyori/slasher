# Phase 12 RPA Expansion Plan

Phase 12 starts after Phase 11 commit `7caa579` (`Complete Slasher phase 11`).

The goal is to add practical RPA packages while preserving Slasher's primary purpose: AI-driven Windows app development and testing with strong evidence, logs, screenshots, and structured errors.
Distributed peer namespace work is tracked separately in
`peer-network-model.md`. Phase 12 packages should still be designed so their
data models can later move behind portable Slasher resources.

## Current Baseline

Phase 11 completed these foundations:

- server-side script runner with run artifacts
- Web UI and MCP run/report readback
- native window/control tree inspection and element actions
- image matching
- Selenium WebDriver browser automation for Edge, Chrome, and Firefox
- browser screenshots, links, tabs/windows, downloads, selected options, and console logs

Do not duplicate these mechanisms in Phase 12. New packages should call into the same script execution, artifact, and error model.
Security policy for destructive actions, credentials, remote access, and
redaction is tracked in `security-policy.md` and should be treated as a gate
for the relevant slices.
Peer export of any Phase 12 resource must wait until the peer namespace policy
rules are implemented.

## 0.2.5 Local Foundation Slice

The local Phase 12 foundation slice is implemented:

- `POST /data/csv/read`
- `POST /data/csv/to-json`
- `POST /data/json/read`
- `POST /data/json/query`
- `POST /data/json/write`
- `POST /data/excel/workbook`
- `POST /data/excel/read`
- destructive file/folder operations require `allowDestructive=true` unless
  `dryRun=true`
- destructive file/folder operations return a `FileOperationPlan` for dry-runs
- `POST /watchers/files`
- `GET /watchers/files`
- `GET /watchers/files/{watcherId}/events`
- `POST /watchers/files/{watcherId}/stop`

This slice deliberately stays local and HTTP-oriented. Script bindings, MCP
tools, scheduling, credentials/secrets, report export, and peer export remain
future Phase 12 work.

## Phase 12 Priority Order

1. CSV package
2. JSON package
3. Excel package
4. Safer destructive action policy
5. File/folder watcher package
6. Scheduling hooks
7. Credentials/secrets
8. Report export/distribution

Browser DevTools/network capture is useful, but it is a separate browser-testing follow-up and should not block the first RPA data packages.

## Package Design Rules

- Add commands as library-style script actions, not special syntax.
- Choose names that can later map cleanly to Numadora modules such as
  current Numadora modules such as `slasher_csv`, `slasher_json`, and
  `slasher_excel`.
- Keep HTTP API, script, MCP, and docs aligned in the same change.
- Keep package result shapes portable enough to be exposed as future namespace
  resources.
- Return structured objects that work with existing variables, arrays, assertions, and logs.
- Every command must produce an execution event when run through scripts.
- Destructive operations must expose enough parameters to be auditable.
- Prefer .NET parsers/libraries over ad hoc string manipulation.

## CSV Package

Initial HTTP slice implemented. Script and write/append support remain future
work.

Script commands:

```slasher
csv read "data/input.csv" as rows
csv write "artifacts/output.csv" rows
csv append "artifacts/output.csv" row
```

API candidates:

- `POST /data/csv/read` implemented
- `POST /data/csv/to-json` implemented
- `POST /data/csv/write` future
- `POST /data/csv/append` future

Result shape:

```json
{
  "path": "data/input.csv",
  "rows": [
    { "Name": "Alice", "Score": "10" }
  ],
  "rowCount": 1,
  "columns": ["Name", "Score"]
}
```

Acceptance checks:

- handles UTF-8 with BOM and without BOM
- preserves headers
- reports row count and column names
- script assignment works: `csv read ... as rows`
- failure includes path and parse error details

## JSON Package

Script commands:

```slasher
json read "data/config.json" as config
json write "artifacts/config-out.json" config
json get config "browser.name" as browserName
```

API candidates:

- `POST /data/json/read` implemented
- `POST /data/json/write` implemented
- `POST /data/json/query` implemented

Acceptance checks:

- uses `System.Text.Json`
- supports object and array roots
- preserves numbers, booleans, strings, arrays, and objects
- reports invalid JSON with line/byte position when available

## Excel Package

Initial read-only HTTP slice implemented.

Script commands:

```slasher
excel read "data/book.xlsx" sheet "Sheet1" as rows
excel write "artifacts/book-out.xlsx" sheet "Results" rows
```

Implementation note:

- Choose and document the package before implementation.
- Keep formula handling conservative at first.

Acceptance checks:

- reads `.xlsx` implemented
- selects sheet by name implemented
- maps first row to headers implemented
- writes simple rows to a new workbook future
- returns row count, columns, and sheet name

## Safer Destructive Action Policy

Apply before broadening delete/overwrite automation.
Detailed policy: `security-policy.md`.

Policy requirements:

- destructive commands must report target paths/window/process metadata
- recursive delete/move must validate resolved absolute paths
- support dry-run where practical
- support explicit `force` only where meaningful
- never delete generated paths outside the intended directory

Initial targets:

- file delete implemented
- folder delete implemented
- folder copy overwrite implemented
- close all windows

## File/Folder Watchers

Initial HTTP slice implemented.

Script idea:

```slasher
watch file "downloads/*.csv" timeout 30000 as downloaded
watch folder "inbox" created "*.xlsx" timeout 60000 as workbook
```

Acceptance checks:

- supports persistent watcher start/list/events/stop
- returns path, event type, timestamps
- script timeout-oriented commands remain future work

## Verification For Each Slice

Run at minimum:

```powershell
dotnet build src\Slasher\Slasher.csproj -o artifacts\build-check
dotnet test
node --check scripts\slasher-mcp.mjs
node --check src\Slasher\wwwroot\app.js
```

For every new package, add one script smoke test and, where practical, one unit test.

## Documentation Checklist

For each command slice, update:

- `README.md`
- `docs/ai-agent-guide.md`
- `docs/implementation-roadmap.md`
- this Phase 12 plan if priorities or command shapes change

## Suggested First Task

Start with CSV read/write because it exercises structured data, arrays/objects, script variables, API requests, MCP tool shape, and artifact-friendly verification without adding browser or Windows UI complexity.
