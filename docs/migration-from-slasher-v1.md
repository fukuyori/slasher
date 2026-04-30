# Migration From Slasher v1

This guide explains how to move important `.slasher` scripts to Numadora
`.numa` scripts.

Slasher v1 compatibility is not a long-term goal. Current `.slasher` scripts
may stop working after the Numadora path covers the core automation loop. The
recommended path is to rewrite scripts into the current Numadora syntax rather
than preserving the v1 language surface.

Numadora is a general-purpose language with broad Windows-control capabilities.
This migration guide is only a bridge away from old scripts; it must not define
Numadora's future API design.

## Migration Policy

- New scripts should be `.numa`.
- Existing `.slasher` scripts should be ported only when they are still useful.
- Unimportant `.slasher` scripts may be deleted instead of migrated.
- The v1 runner may be removed or archived after `.numa` check/run covers the
  core AI-driven testing loop.
- Slasher should adapt its implementation to current Numadora, not add a
  Slasher-only compatibility parser.
- Old command names are examples of intent, not names that must be preserved.

## Current Target Style

Use current Numadora imports, functions, and alias-qualified calls:

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
    input.Text("hello")
    test.AssertForegroundTitle("contains", title)
END
```

Do not write new migration examples using `IMPORT slasher/app AS app`,
`.numai`, bare commands, or `app.start "..."` unless Numadora itself grows
those features.

## Common Rewrites

| Slasher v1 | Current Numadora target |
|---|---|
| `start notepad.exe` | `LET handle := app.Start("notepad.exe")` |
| `wait window "Notepad" 10000 as win` | `LET title := win.WaitForTitle("Notepad", 10000)` |
| `focus ${handle}` | `win.Focus(handle)` |
| `text "hello"` | `input.Text("hello")` |
| `wait 800` | `io.Wait(800)` |
| `log "message"` | `io.Log("message")` |
| `step "name"` | `io.Step("name")` |
| `assert foreground title contains "Notepad"` | `test.AssertForegroundTitle("contains", "Notepad")` |
| `include lib/common.slasher` | rewrite as a `.numa` module and `IMPORT module AS alias` |
| `function name ... endfunction` | `FUNC name(...) ... END` |
| `set name value` | `LET name := value` for new immutable values, or `VAR` when mutation is required |

The mapping is intentionally not one-to-one. Prefer the shape that Numadora can
check today and that still makes sense as a general Windows-control API.

## Porting Order

1. Identify smoke tests and AI-driven scenarios that still matter.
2. Rewrite one small script into `.numa` using the current target style.
3. Run `scripts/verify-numadora-n0.ps1` to confirm the baseline still checks.
4. Add a check-only test through Slasher once N2 is implemented.
5. Move shared helpers into Numadora modules only after the call shapes are
   stable.
6. Delete or archive unused `.slasher` files instead of carrying them forward.

## Unsupported v1 Features

The following should not block migration:

- implicit global command namespace
- unquoted command arguments
- `include` semantics
- v1 dynamic variable scopes
- v1-specific test command spelling
- line-oriented macro syntax

Where these features are important, rewrite the scenario into ordinary
Numadora functions first. Ergonomic macros can come later as a Numadora feature.

## Tooling

A future `slasher migrate` command may be useful, but it should generate drafts
and migration reports. It should not promise perfect automatic conversion.

Minimum useful behavior:

- add the initial `IMPORT` statements
- map common v1 commands to alias-qualified function calls
- preserve unsupported source lines as explicit TODO comments
- produce a report of manual risks

Porting tooling is optional and is not required before the v1 runner can be
removed.

## Verification

Current N0 verification:

```powershell
.\scripts\verify-numadora-n0.ps1
```

This checks the local Numadora baseline and the Slasher current-spec sample in
`scripts/numadora-samples/notepad-check.numa`.

After N2, migrated scripts should also pass the Slasher check endpoint. After
N3, important scripts should run through Slasher and produce the normal artifact
family.

## Related Documents

- `language-system.md`
- `slasher-script.md`
- `slasher-numadora-integration.md`
- `numadora-runtime-contract.md`
- `numadora-migration-plan.md`
