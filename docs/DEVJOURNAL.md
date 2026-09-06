# zAI-Machine — Development Journal

This document records the implementation steps, design decisions, alternatives
considered, and lessons learned during the development of zAI-Machine. It is
maintained alongside the source code and exported as a PDF at project completion.

---

## Phase 1: Project Foundation

### Task 1.1 — Solution Structure and Build Configuration

**Date**: 2026-09-06

#### Steps Taken

1. **Created the .NET solution** using `dotnet new sln`, which produced a
   `.slnx` file (the new XML-based solution format introduced in .NET 10).
   The original plan called for a `.sln` file, but `.slnx` is functionally
   equivalent and is the default for the SDK version available.

2. **Scaffolded four projects** matching the architecture defined in TASKS.md:
   - `src/ZMachine.Core` — class library, interpreter engine with no UI deps
   - `src/ZMachine.IO` — class library, I/O abstractions
   - `src/ZMachine.App` — console executable, will become the GUI host
   - `tests/ZMachine.Tests` — xUnit test project

3. **Set up project references** to enforce the dependency hierarchy:
   - Core has no project references (it is the leaf)
   - IO references Core (needs access to engine types)
   - App references both Core and IO (wires everything together)
   - Tests references Core and IO (not App — tests should exercise
     libraries, not the host executable directly)

4. **Created `Directory.Build.props`** at the solution root to centralize
   shared MSBuild properties: target framework, nullable reference types,
   implicit usings, language version, and `TreatWarningsAsErrors`. This
   avoids duplicating these settings across every `.csproj` file and makes
   framework upgrades a single-line change.

5. **Wrote I/O interface stubs** in `ZMachine.IO`:
   - `IScreen` — 12 methods covering text output, window management, cursor
     positioning, and styling (ZSpec S8)
   - `IInputStream` — `ReadLine` and `ReadChar` for keyboard input (ZSpec S10)
   - `ISoundEngine` — load/play/stop with the dual-channel model (ZSpec S9)

6. **Implemented `ConsoleScreen`** as the first `IScreen` backend, using ANSI
   escape codes for basic styling (reverse video, bold, italic) and
   `Console.SetCursorPosition` for cursor movement.

7. **Wrote 4 smoke tests** in `ZMachine.Tests` verifying that `ConsoleScreen`
   doesn't throw on basic operations: screen size, split/unsplit, text styles,
   and buffer mode toggling.

8. **Verified the milestone**: `dotnet run --project src/ZMachine.App` prints
   "zAI-Machine ready" with version info and screen dimensions.

#### Design Decisions

**Target framework: net10.0 instead of net8.0**

TASKS.md specifies ".NET 8+". The development machine has only the .NET 10 SDK
and runtime installed — no .NET 8 runtime is present. While the .NET 10 SDK can
*build* for `net8.0` targets, the test runner and application require the
corresponding runtime to *execute*. Rather than requiring a .NET 8 runtime
installation, we target `net10.0` which satisfies the "8+" requirement and
matches the available tooling. The `Directory.Build.props` makes this a
single-line change if we ever need to retarget.

**Solution format: .slnx vs .sln**

The `dotnet new sln` command on .NET 10 SDK produces `.slnx` (XML-based) by
default rather than the traditional `.sln` (text-based). Both are fully
supported by `dotnet build`, Visual Studio, Rider, and VS Code. We accepted
the default since there's no functional difference and `.slnx` is the
forward-looking format.

**Directory layout: src/ and tests/**

Projects are organized under `src/` and `tests/` rather than flat at the
solution root. This keeps the root clean (specs, examples, stories, docs all
have their own directories) and follows the conventional .NET layout for
multi-project solutions.

**Tests do not reference ZMachine.App**

The test project references Core and IO but not App. The App project is the
host executable — it wires things together but should not contain testable
logic. If integration tests need to exercise end-to-end behavior, they will
use the `TestHarness` class (Task 7.2) which operates through the library
interfaces, not through the App project.

#### Spec Interpretation Notes

No spec ambiguities encountered at this phase — Task 1.1 is pure scaffolding.
The `IScreen` interface design anticipates the version-dependent screen model
(V1–3 status line vs V4–5 split windows vs V6 independent windows) by keeping
methods generic enough to support all three models.

---
