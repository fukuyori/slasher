# Slasher Docs

This directory is organized around the current Slasher implementation state,
Slasher's Numadora-based script direction, and AI-facing automation contracts.

## Start Here

- `ai-agent-guide.md` - practical guide for AI agents using Slasher
- `implementation-roadmap.md` - current status, completed tracks, and next work
- `language-system.md` - entry point for Slasher's Numadora-based script direction

## Architecture And Contracts

- `architecture.md` - server structure and ownership boundaries
- `ai-automation-contract.md` - action/result/report schema
- `ai-test-observability.md` - evidence, screenshots, logs, and failure reporting
- `security-policy.md` - security rules for powerful local PC automation

## Language

- `numadora-migration-plan.md` - implementation plan for using Numadora in Slasher scripts
- `numadora-runtime-contract.md` - Phase N0 runtime/check/run boundary contract
- `slasher-script.md` - current Numadora script profile used by Slasher
- `numadora-language-spec.md` - generic Numadora language specification
- `slasher-numadora-integration.md` - Slasher bindings and Numadora integration model
- `migration-from-slasher-v1.md` - migration from `.slasher` to `.numa`

## Active Planning

- `phase-12-rpa-expansion-plan.md` - next RPA package expansion plan

## Removed From Active Docs

The previous standalone Slasher Script compiler plan was removed because it
conflicted with the current Numadora-based direction. New language work should
start from `language-system.md`.
