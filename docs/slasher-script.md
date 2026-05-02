# Slasher Numadora Script Profile

This document defines the current Numadora script profile used by Slasher.

The guiding rule is simple: **Slasher is the application; Numadora is the
language used for Slasher scripts.** This profile must not become a
Slasher-specific dialect that drifts away from Numadora.

For the overall document map, see `language-system.md`. For the implementation
plan, see `numadora-migration-plan.md`.

## Current Profile

Use current Numadora syntax:

- sibling-style modules: `IMPORT module AS alias`
- module files named like `slasher_app.numa`
- exported functions called as normal functions: `app.Start("notepad.exe")`
- `FUNC main() ... END`
- `LET name := value`
- current type spelling: `Int`, `String`, `Bool`, `Array<T>`

Do not add Slasher-only source rewriting just to preserve v1 command syntax.
Old command spelling is migration evidence, not a design source.

## Minimal Script

```numadora
IMPORT slasher_app AS app
IMPORT slasher_window AS win
IMPORT slasher_input AS input
IMPORT slasher_io AS io
IMPORT slasher_test AS test

FUNC main()
    io.Step("open notepad")
    LET handle := app.Start("notepad.exe")

    LET title := win.WaitForTitle("Notepad", 10000)
    win.Focus(handle)

    io.Step("type text")
    input.Text("Slasher Numadora smoke")
    test.AssertForegroundTitle("contains", title)
END
```

The checked sample lives at:

```text
scripts/numadora-samples/notepad-check.numa
```

## Module Naming

For the first implementation, use module names that the current Numadora
prototype can import directly:

| Slasher area | Module |
|---|---|
| app/process | `slasher_app` |
| window | `slasher_window` |
| input | `slasher_input` |
| screen | `slasher_screen` |
| element | `slasher_element` |
| browser | `slasher_browser` |
| logging/steps/wait | `slasher_io` |
| message boxes | `slasher_dialog` |
| assertions | `slasher_test` |

Future Numadora module-path support may allow names such as `slasher/app`, but
Slasher should not depend on that before the runtime supports it.

## Function Naming

Use PascalCase exported function names to match the current Numadora examples:

```numadora
app.Start("notepad.exe")
win.WaitForTitle("Notepad", 10000)
win.Focus(handle)
input.Text("hello")
input.Keys("CTRL+S")
input.Mouse("move", 400, 300, "left")
input.Wheel(400, 300, 120)
input.Drag(400, 300, 500, 350, "left", 400, 24)
input.ContextMenu(400, 300, 250)
screen.Capture("full", 1280, 720)
element.Exists("foreground", "OK", "-", -1, "contains", 8, 1)
element.Find("foreground", "OK", "-", -1, "contains", 8, 20)
element.ReadText("foreground", "Status", "-", -1, "contains", 8, 1)
element.Tree("foreground", 2, 50)
browser.Current("-")
browser.Title("-")
browser.Url("-")
browser.Locate("css", "body", 5000, "-")
browser.DomText("css", "body", 5000, "-")
browser.Attribute("css", "body", "class", 5000, "-")
browser.Screenshot("-")
browser.Links("-")
browser.Windows("-")
test.AssertForegroundTitle("contains", "Notepad")
```

`input.Text(...)`, `input.Keys(...)`, `input.Mouse(...)`, `input.Wheel(...)`,
`input.Drag(...)`, and `input.ContextMenu(...)` are intentionally stricter in
run mode than in check mode. They require target identity and explicit
`allowInteractiveInput` approval before Slasher sends input to the foreground
application.

This is intentionally different from the v1 `.slasher` command style:

```text
start notepad.exe
text "hello"
```

The v1 form is not a compatibility requirement and should not guide new
Numadora API design.

## Host Bindings

During N0, Slasher modules are source-level stubs in
`scripts/numadora-samples/` so the current Numadora checker can validate the
script shape.

During N1/N2, those stubs should be replaced or backed by real Slasher host
bindings while keeping the same Numadora-facing function signatures.

The first implementation should prefer adapting Slasher to Numadora over
extending Numadora with Slasher-only syntax. If a feature is needed for broad
Windows control, specify it as a Numadora capability rather than as legacy
Slasher behavior.

## Error And Evidence Policy

Slasher must preserve the existing automation evidence model:

- run metadata
- event logs
- source file and line information
- structured errors
- screenshots and attachments when available
- HTML reports and artifact readback

The language syntax can change, but the evidence loop should not regress.

## Porting From v1

Important v1 scenarios should be re-expressed in `.numa`; unimportant
`.slasher` scripts may be deleted instead of migrated. Re-expression means
choosing the clearest Numadora API, not mechanically preserving old command
names.

Examples:

| v1 `.slasher` | `.numa` |
|---|---|
| `start notepad.exe` | `app.Start("notepad.exe")` |
| `wait 800` | `io.Wait(800)` |
| `text "hello"` | `input.Text("hello")` |
| `keys CTRL+S` | `input.Keys("CTRL+S")` |
| `mouse move 400 300` | `input.Mouse("move", 400, 300, "left")` |
| `mouse wheel 400 300 120` | `input.Wheel(400, 300, 120)` |
| `mouse drag 400 300 500 350 left 400 24` | `input.Drag(400, 300, 500, 350, "left", 400, 24)` |
| `mouse context-menu 400 300 250` | `input.ContextMenu(400, 300, 250)` |
| `foreground as win` | `LET win := win.Foreground()` once exposed |
| `assert foreground title contains Notepad` | `test.AssertForegroundTitle("contains", "Notepad")` |

## Open Items

- Decide whether host bindings are generated `.numa` stubs, runtime-provided
  functions, or both.
- Decide whether future Numadora module paths such as `slasher/app` are needed.
- Replace v1-oriented samples with `.numa` samples for the important smoke
  scenarios.
