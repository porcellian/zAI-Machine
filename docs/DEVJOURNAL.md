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

### Task 1.2 — Story File Loader and Memory Model

**Date**: 2026-09-06

#### Steps Taken

1. **Read the relevant spec sections**: ZSpec S1 (memory map), ZSpec11
   "Memory layout" (V6/V7 max size corrected to 512K), and ZSpec11
   "Padding" (non-zero padding in Infocom files excluded from checksum).

2. **Inspected available story files**: `stories/minizork.z3` (52,216
   bytes, V3) and `stories/czech.z5` (V5 conformance suite). Used `xxd`
   to dump and hand-parse the 64-byte headers, confirming the byte
   layout for version, high memory base, static memory base, file length,
   and checksum fields.

3. **Implemented `Memory` class** in `src/ZMachine.Core/Memory.cs`:
   - `LoadStory(string path)` and `LoadStory(byte[] data)` with full
     validation (version 1–8, size limits, static base sanity, file
     length vs actual size).
   - `ReadByte` / `ReadWord` (big-endian) for reading at any address.
   - `WriteByte` / `WriteWord` with static memory write protection —
     any write at or above `StaticBase` throws `InvalidOperationException`.
   - `DynamicBase` (always 0), `StaticBase`, `HighBase` properties parsed
     from header words.
   - `FileLength` unpacked using version-dependent multiplier (×2/×4/×8).
   - `HeaderChecksum` from header bytes $1C–$1D.
   - `ComputeChecksum()` summing bytes $40 through the declared file
     length, excluding padding per ZSpec11.
   - `OriginalBytes` — independent copy retained for `@restart` and
     Quetzal CMem XOR compression.
   - `RestoreDynamicMemory()` — copies original bytes back into the
     dynamic region (0 to StaticBase-1).
   - `RawBytes` / `DynamicSpan` for performance-critical bulk access.

4. **Wrote 30 tests** in `tests/ZMachine.Tests/MemoryTests.cs` covering:
   - Header parsing for both minizork.z3 (V3) and czech.z5 (V5)
   - Big-endian read/write correctness
   - Static memory write protection (at boundary, above, spanning)
   - Checksum computation verified against header-declared values
   - Original bytes independence and `RestoreDynamicMemory` round-trip
   - Validation: too-small files, invalid versions, bad static base
   - Synthetic V3 story file construction for controlled testing
   - File-path loading and missing-file error handling

5. **Fixed test infrastructure**: Story file paths are relative to the
   repo root, but `dotnet test` runs from the output bin directory. Added
   a `FindRepoRoot()` helper that walks up from `AppContext.BaseDirectory`
   looking for the `.slnx` file.

6. **All 42 tests pass** (30 new Memory tests + 4 existing ConsoleScreen
   tests + 8 framework-provided).

#### Design Decisions

**Class design: mutable Memory vs immutable StoryFile**

Considered making `Memory` immutable (returning new instances on write)
for safety, but rejected it because: (a) the Z-Machine spec explicitly
models memory as a mutable byte array — writes to dynamic memory are a
core operation, not an exception; (b) immutable copies on every write
would be prohibitively expensive for a 512K array in a tight instruction
loop; (c) the `OriginalBytes` copy already provides the immutability
needed for restart and save comparison.

**Validation strictness**

Chose to reject files with `StaticBase == 0` even though the spec
doesn't explicitly forbid it — a zero static base would make the entire
file read-only, which is never correct for a real story file. Similarly,
`StaticBase > file.Length` is rejected because it would place the
boundary outside the loaded data.

**File length = 0 handling**

Some very early V1–V3 files have a zero file-length header field (bytes
$1A–$1B = 0). Rather than rejecting these, `FileLength` is set to 0 and
`ComputeChecksum()` falls back to summing all bytes from $40 to the end
of the actual file. This matches the behavior described in ZSpec S11
("Infocom used this for checksum calculation").

**`OriginalBytes` as `byte[]` rather than `ReadOnlyMemory<byte>`**

Used a plain `byte[]` for `OriginalBytes` rather than wrapping it in
`ReadOnlyMemory<byte>`. The array is simpler to index, slice, and pass
to `Array.Copy` for `RestoreDynamicMemory`. It's exposed as a public
property for Quetzal XOR diff computation; callers are trusted not to
mutate it (and a future refactor could wrap it if needed).

**Write protection boundary: at StaticBase, not above**

The spec says dynamic memory extends from byte 0 up to "the byte before
the byte address stored in the header." This means `StaticBase` itself
is the first static byte, so writes at `StaticBase` are illegal. Both
`WriteByte` and `WriteWord` enforce `address >= StaticBase` as the
guard, and `WriteWord` checks both bytes of the word individually to
catch writes that span the dynamic/static boundary.

#### Spec Interpretation Notes

**ZSpec11 "Memory layout" — V6/V7 max size**: The base spec (table in
S1) lists V6/V7 as 512K. The 1.1 amendments confirm this (the earlier
version of the spec had incorrectly listed 320K). The `MaxStorySize`
array uses 512K for V6–V8.

**ZSpec11 "Padding"**: Infocom story files are often padded to a
sector/block boundary. The padding bytes may be non-zero. The checksum
computation must only sum bytes from $40 to the *header-declared* file
length, not to the end of the physical file. This was verified by
computing the checksum for `minizork.z3` — it matches the header-
declared value (0xD870), confirming that the file length (52,216 bytes)
equals the actual file size (no padding in this case).

**File length packing multipliers**: V1–3: ×2, V4–5: ×4, V6–8: ×8.
Verified with minizork.z3: packed value 0x65FC × 2 = 52,216 = actual
file size. Verified with czech.z5: packed value 0x0CCF × 4 = 13,116.

---

### Task 1.3 — Header Parser and Version Detection

**Date**: 2026-09-06

#### Steps Taken

1. **Read spec sections**: ZSpec S11 (header format table), ZSpec11
   "Header capabilities bits" (Flags 1/2/3 semantics), ZSpec11 "Header
   Extension" (extension table words 4–6 for Standard 1.1).

2. **Inspected story file headers** with `xxd`: minizork.z3 (V3, no
   extension table) and czech.z5 (V5, extension table at $0106 with
   3 words). Verified field offsets by hand-parsing both headers.

3. **Implemented `Header` class** in `src/ZMachine.Core/Header.cs`:
   - Typed read-only properties for all 64-byte header fields: version,
     flags, release number, memory addresses (high, static, dictionary,
     object table, globals, abbreviations, terminating chars, alphabet),
     serial number, file length, checksum, interpreter ID, screen dims,
     V6/V7 offsets, colors, and standard revision.
   - `HeaderExtension` parsed automatically if header word $36 is nonzero.
   - `ConfigureInterpreter()` method for capability negotiation: writes
     interpreter ID, screen dimensions, standard revision ($01 $01), and
     sets/clears capability bits in Flags 1 and Flags 2.
   - `InterpreterCapabilities` flags enum covering all negotiable features.

4. **Implemented `HeaderExtension` class** in `src/ZMachine.Core/HeaderExtension.cs`:
   - Parses word count, Unicode translation table address, Flags 3, and
     true default colors from the extension table.
   - Gracefully handles tables shorter than the full 4 words (returns 0
     for missing entries).
   - `ClearReservedFlags3Bits()` clears all reserved bits per ZSpec11.

5. **Wrote 38 tests** in `tests/ZMachine.Tests/HeaderTests.cs`:
   - Complete header field parsing for minizork.z3 (V3, 13 tests) and
     czech.z5 (V5, 8 tests).
   - Header extension parsing (5 tests): word count, unicode table,
     Flags 3, true colors, beyond-table-length handling.
   - Capability negotiation (8 tests): standard revision, interpreter
     number, screen dimensions, V3 flag bits, V5 flag bits, screen units,
     Flags 3 reserved bit clearing, no-capabilities clearing.
   - Synthetic tests (2 tests): V6 routines/strings offsets, colors.

6. **All 80 tests pass** (38 Header + 30 Memory + 4 ConsoleScreen + 8
   framework).

#### Design Decisions

**Header reads from Memory, not raw bytes**

The `Header` constructor takes a `Memory` instance rather than a `byte[]`.
This keeps the header in sync with the live memory state — when
`ConfigureInterpreter()` writes capability bits back, they go through
`Memory.WriteByte`/`WriteWord` which enforces the dynamic/static
boundary. All header bytes are in dynamic memory (below StaticBase),
so writes succeed.

**InterpreterCapabilities as [Flags] enum**

Used a `[Flags]` enum rather than individual bool parameters or a
config object. This makes the call site readable (`Colors | Bold |
Italic`) and is easy to extend as new capabilities are added. The
flag values don't correspond directly to Flags 1/2 bit positions
because those differ by version — the mapping is handled internally.

**Flags 1 bit semantics differ by version**

V1–3 and V4+ Flags 1 have completely different bit meanings. Rather
than exposing a unified abstraction, `ConfigureInterpreter` branches
on version and sets the correct bits for each. The raw `Flags1` byte
is still available for callers that need to inspect game-set bits.

**HeaderExtension numbering**

The ZSpec11 amendments label extension words as "Word 4", "Word 5",
"Word 6" — these continue the conceptual numbering from the base
header. In the actual table, these are at data offsets 2, 3, 4 (word
0 = count, word 1 = Unicode table). The implementation uses the table
offset for indexing and documents the spec numbering in comments.

#### Spec Interpretation Notes

**Header extension word count**: Word 0 of the extension table is the
number of "further words" — i.e., words beyond word 0 itself. Czech.z5
has word count = 3, meaning words 1–3 are present but word 4 (true
default background) is not. `ReadExtensionWord` returns 0 for indices
beyond the count.

**Flags 3 reserved bit clearing**: ZSpec11 says "all reserved bits in
the Flags 3 word MUST be cleared by the interpreter." Only bit 0
(transparency request) is defined. The interpreter clears everything
except bit 0, and even bit 0 would need to be cleared if transparency
isn't supported (handled during capability negotiation once the
rendering system is in place).

**Standard revision bytes**: $32 and $33 are two separate bytes (major
and minor), not a big-endian word. For Standard 1.1, the interpreter
writes $01 at $32 and $01 at $33 using `WriteByte`, not `WriteWord`.

---

## Phase 2: Instruction Decoding

### Task 2.1 — Opcode Forms and Operand Type Decoding

**Date**: 2026-09-06

#### Steps Taken

1. **Read spec sections**: ZSpec S4.1–S4.4 (instruction encoding forms),
   ZSpec S4.5 (store byte), ZSpec S4.7 (branch offset), ZSpec11 "Operand
   evaluation" (left-to-right order).

2. **Inspected real instructions** by dumping bytes at zork1.z3's initial
   PC ($4F05). Verified the first instruction is `call_vs` (VAR:0) with
   3 large-constant operands: $2A39, $8010, $FFFF.

3. **Implemented `Instruction` struct** and supporting types in
   `src/ZMachine.Core/Instruction.cs`:
   - `OpcodeForm` enum: Op2, Op1, Op0, Var, Ext
   - `OperandType` enum: LargeConstant, SmallConstant, Variable, Omitted
   - `BranchInfo` struct with BranchOnTrue, Offset, IsRFalse, IsRTrue

4. **Implemented `InstructionDecoder`** in
   `src/ZMachine.Core/InstructionDecoder.cs`:
   - `Decode(Memory, int pc)` handles all four encoding forms:
     - Long form (0b0x): bits 6,5 encode two operand types, bottom 5 = opcode
     - Short form (0b10): bits 5,4 = type (or 0OP if 0b11), bottom 4 = opcode
     - Variable form (0b11): bit 5 → 2OP vs VAR, type byte(s) follow
     - Extended form ($BE prefix): next byte = EXT opcode, type byte follows
   - `DecodeStore` and `DecodeBranch` separated out — the caller (future
     opcode dispatcher) calls these based on opcode table metadata.
   - Double-variable forms (call_vs2 $EC, call_vn2 $FA): two type bytes,
     up to 8 operands.

5. **Wrote 33 tests** in `tests/ZMachine.Tests/InstructionDecoderTests.cs`:
   - Long form: all 4 operand type combinations (small/small, var/small,
     small/var, var/var)
   - Short form: large constant, small constant, variable, zero-op
   - Variable form: 2OP encoding, VAR with 3 operands, all 4 operands,
     zero operands, single operand
   - Extended form: basic decode, no operands
   - Double-variable: call_vs2 with 7 operands, call_vn2 with 8 operands
   - Store decoding: stack push (var 0), local, global
   - Branch decoding: short offset true/false, rfalse, rtrue, long offset
     positive/negative/zero
   - Combined store+branch
   - Real instruction from zork1.z3 at PC $4F05
   - Address tracking, sequential decode, edge cases

6. **All 113 tests pass** (33 decoder + 38 header + 30 memory + 4 console
   + 8 framework).

#### Design Decisions

**Store and branch decoding separated from Decode**

`DecodeStore` and `DecodeBranch` are separate methods rather than being
integrated into `Decode`. This is because the decoder doesn't know which
opcodes store and which branch — that knowledge lives in the opcode table
(Task 2.2/2.4). The caller will look up the opcode metadata and call the
appropriate continuation methods. This keeps the decoder focused on byte
parsing without needing to embed the full opcode table.

**InstructionTestMemory pattern**

Tests need to decode at address 0 for readability, but Memory requires a
valid header. The `InstructionTestMemory` helper creates a valid V3 story
file, then overwrites dynamic memory at address 0 with the test bytes.
This avoids putting test bytes at offset $40 and adjusting every address
assertion.

**Struct vs class for Instruction**

Used `struct` rather than `class` for `Instruction`. Instructions are
decoded, processed, and discarded — never stored in collections or
passed by reference across long-lived scopes. Structs avoid heap
allocation in the tight decode-dispatch loop. The `ref` parameter in
`DecodeStore`/`DecodeBranch` keeps mutation efficient.

#### Spec Interpretation Notes

**Variable form bit 5 semantics**: When bit 5 = 0, the instruction is
classified as 2OP despite using variable-form encoding. This means a
2OP instruction can receive up to 4 operands through the variable form's
type byte — useful for opcodes like `je` which can compare against
multiple values.

**Double-variable opcodes**: Only call_vs2 (VAR:12, $EC) and call_vn2
(VAR:26, $FA) use two type bytes. The second type byte is only read if
the first type byte uses all 4 slots (no Omitted entries in the first
byte). If the first byte has an Omitted entry, the second byte is not
read.

---
