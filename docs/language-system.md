# Slasher Language System

This document is the entry point for Slasher's language direction.

The application is **Slasher**. Slasher uses the **Numadora** language for its
script surface.

The product direction is not "keep Slasher v1 syntax alive." The direction is:

- Slasher remains the application name and user-facing automation product.
- Slasher scripts should use Numadora as the unified general-purpose language.
- Numadora should be able to control Windows broadly through typed libraries
  and host capabilities.
- Slasher exposes Windows automation, evidence, Web, MCP, HTTP, and artifact
  features through Numadora-facing modules and host bindings.
- The v1 `.slasher` runner is removed from the public script surface.

Earlier standalone Slasher Script compiler planning has been removed from the
active docs so new work does not accidentally target a second language.

## Canonical Documents

Read these documents in this order:

1. `slasher-script.md`
   - Slasher's current Numadora script profile.
   - Defines how Slasher scripts use Numadora today.
   - Must not define a Slasher-specific language dialect.

2. `numadora-language-spec.md`
   - Generic Numadora language specification.
   - Owns core syntax, type system, modules, macros, errors, and standard
     library rules that are not specific to Slasher.

3. `slasher-numadora-integration.md`
   - Boundary between Numadora and the C# Slasher application/server.
   - Owns host binding strategy, JSON-RPC/HTTP integration, Windows automation
     module signatures, event logs, diagnostics, and implementation phases.

4. `numadora-migration-plan.md`
   - Implementation plan for adding `.numa` support to Slasher.
   - Owns phases, acceptance criteria, migration tooling, and deprecation gates.

5. `migration-from-slasher-v1.md`
   - Historical migration guide for old `.slasher` files.
   - Keep only as reference material for manual porting.

If an old note or issue mentions the standalone Slasher Script compiler, treat
that as superseded by this document and the Numadora specs below.

## Design Decisions

### File Extensions

| Extension | Meaning |
|---|---|
| `.numa` | Active Numadora scripts. |
| `.numai` | Future/reference Numadora interface files only; not part of the active N0 path. |

`.slasher` files are no longer accepted by Slasher script check/run APIs.

### Language Ownership

- Numadora owns the general-purpose language, module system, type system,
  runtime model, and syntax.
- Windows automation should be modeled as Numadora libraries and host
  capabilities, not as inherited Slasher v1 commands.
- Slasher owns the application behavior, Windows automation implementation,
  evidence model, API surfaces, and user-facing product experience.
- Slasher must not fork Numadora syntax for local convenience.
- Slasher-specific command forms are not part of the core direction.

### Script Style

`.numa` scripts should use ordinary Numadora imports and function calls. The
preferred spelling follows the current Numadora implementation:

- `IMPORT slasher_app AS app`
- `app.Start("notepad.exe")`
- `input.Text("hello")`

Do not introduce Slasher-only source rewriting to preserve older command-like
syntax. Keeping the module alias and normal function call visible makes
generated scripts easier to audit and reduces command-name collisions as the
Windows-control library grows.

Example:

```numadora
IMPORT slasher_app AS app
IMPORT slasher_window AS win
IMPORT slasher_input AS input

FUNC main()
    LET handle := app.Start("notepad.exe")
    LET title := win.WaitForTitle("Notepad", 10000)
    win.Focus(handle)
    input.Text("hello from Slasher")
END
```

The goal is not to reproduce v1 commands. The goal is to express Windows
automation in real Numadora syntax while gaining typed modules, structured
errors, imports, and normal Numadora tooling.

### Compatibility Policy

- New language work should target Numadora `.numa`.
- Existing `.slasher` scripts are no longer supported by Slasher script
  check/run APIs.
- Compatibility sugar should not steer the language design.
- Shared behavior should move into Numadora modules and host APIs.

## Implementation Track

The detailed implementation plan is `numadora-migration-plan.md`. At a high
level, the track is:

1. Align scripts and examples with the current Numadora syntax.
2. Define Windows automation module contracts in that syntax.
3. Add `.numa` check/run support.
4. Replace sample stub modules with host bindings backed by Slasher.
5. Add ergonomics only when they fit Numadora as a whole.
6. Port any still-useful historical examples to `.numa`.
7. Keep public script execution Numadora-only.

Phase 12 RPA package work can continue, but new package shapes should be chosen
so they can become Numadora modules without semantic churn. Implementation
examples should use the module names accepted by the current runtime, such as
`slasher_csv`.

## Open Questions

- Which Windows-control modules must be available before `.numa` is useful for
  real AI-driven tests.
- Whether host bindings should stay as source-level stub modules during early
  development or move immediately to runtime-provided functions.
