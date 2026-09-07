# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

zAI-Machine is a C# Z-Machine interpreter implementing Z-Machine Standard 1.1, Quetzal save format 1.4, and Blorb resource format 2.0.4. It supports Z-Machine versions 1–8 and offers seven selectable vintage GUI themes. See `TASKS.md` for the full 56-task implementation plan across 14 phases.

## Tech Stack

- **Runtime**: .NET 8+, C#
- **GUI**: Avalonia UI + SkiaSharp (pixel-precise bitmap rendering for retro themes)
- **Testing**: xUnit
- **Save format**: Quetzal 1.4 (IFF container)
- **Resource format**: Blorb 2.0.4 (IFF container)

## Planned Project Structure

```
zAI-Machine.sln
├── ZMachine.Core     # Interpreter engine — no UI dependencies
├── ZMachine.IO       # I/O abstractions (IScreen, IInputStream, ISoundEngine)
├── ZMachine.App      # Avalonia GUI host, themes, developer tools
└── ZMachine.Tests    # xUnit tests
```

## Build & Test Commands

Once the solution is scaffolded (Task 1.1):

```bash
dotnet build                              # Build all projects
dotnet test                               # Run all xUnit tests
dotnet test --filter "FullyQualifiedName~ClassName.MethodName"  # Single test
dotnet run --project ZMachine.App         # Launch the GUI application
```

## Specs

The three specification files in `specs/` are the authoritative references:

- `specs/ZSpec11.txt` — Z-Machine Standard 1.1 amendments (use notation `ZSpec S<section>` for base spec, `ZSpec11 "<topic>"` for 1.1 additions)
- `specs/savefile_14.txt` — Quetzal 1.4 save format (notation: `Quetzal S<section>`)
- `specs/blorb_format.txt` — Blorb 2.0.4 resource format (notation: `Blorb "<section title>"`)

## Code Standards

All code must include useful comments per the standards in `TASKS.md` § Code Standards:

- **XML doc comments** on every public class, interface, and method
- **Spec reference comments** citing the spec section being implemented (e.g., `// ZSpec S4.1 — Long form: bits 6,5 encode the two operand types`)
- **"Why" comments** for non-obvious logic, workarounds, and edge cases
- **No noise comments** — don't restate what well-named code already says

## Key Architecture Notes

- **Memory model**: Big-endian byte array divided into dynamic (writable), static (read-only at runtime), and high (code/strings) regions. A pristine copy of the original file is kept for restart and Quetzal XOR compression.
- **Instruction encoding**: Four forms (long, short, variable, extended) with version-dependent packed address calculations. Operands evaluated left-to-right.
- **Stack model**: Dual stack — a call stack of frames (each with locals and return address) plus a per-frame evaluation stack. Variable 0 is the stack pointer with special indirect-reference semantics for 7 opcodes.
- **Text**: 5-bit Z-characters packed 3 per 16-bit word, mapped through three alphabet tables. ZSCII codes 155–251 are extra characters overridable via Unicode translation table.
- **Screen model**: V1–3 status line, V4–5 split windows, V6 eight independent windows with full property sets. All rendering goes through `IScreen`/`IRenderer` abstractions.
- **IFF container**: Shared reader/writer used by both Quetzal (FORM type 'IFZS') and Blorb (FORM type 'IFRS').
- **Theme system**: Each vintage theme implements `ITheme` and provides a `ThemeConfig` (color palette, bitmap font, screen dimensions, chrome style). SkiaSharp renders to a back buffer; Avalonia hosts the canvas.

## Test Story Files

- `stories/` contains Infocom story files (.z3/.z4/.z5/.z6) for compatibility testing
- `stories/czech.z5` is the Z-Machine conformance test suite — run it to validate opcode correctness
- `stories/minizork.z3` is useful for quick smoke tests
- Visual references for GUI themes: `examples/zork_i_c64.png` (C64 Classic), `examples/zork_i_modC64.jpg` (Modern C64)

## Development Journal

A development journal (`docs/DEVJOURNAL.md`) must be maintained throughout implementation, recording steps taken, design decisions, alternatives considered, and spec interpretation notes. It is exported as PDF at project completion (Task 14.5).

## Git Workflow
When completing tasks from TASKS.md:
1. Create a new branch named 'feature/<task-number>-<brief-description> before starting work.
2. Make atomic commits with conventional commit messages:
- feat: for new features
- fix: for bug fixes
- docs: for documentation
- test: for tests
- refactor: for refactoring
3. After completing a task, create a pull request with:
- A descriptive title matching the task
- A summary of changes made
- Any testing notes or considerations
4. Update the task checkbox in TASKS.md and mark it complete.

## Testing Requirements
Before marking any task as complete:
1. Write unit tests for new functionality
2. Run the full test suite as described in Build & Test Commands
3. If tests fail:
- Analyze the failure output
- Fix the code (not the tests unless tests are incorrect)
- Re-run tests until all pass

