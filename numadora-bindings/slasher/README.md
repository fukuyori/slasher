# Slasher Numadora Bindings

This directory contains Slasher-owned Numadora host binding notes.

N0 originally considered `.numai` interface files, but the current Numadora
prototype does not load `.numai`. The active N0 path is therefore to express the
first Slasher surface as ordinary Numadora modules and function calls.

Current checked stubs live in:

```text
scripts/numadora-samples/
  slasher_app.numa
  slasher_window.numa
  slasher_input.numa
  slasher_io.numa
  slasher_test.numa
```

Future host binding work should keep the same Numadora-facing module and
function shape unless the Numadora runtime itself changes.
