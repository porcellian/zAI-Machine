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

### Task 2.2 — Branch and Store Result Mechanics

**Date**: 2026-09-06

#### Steps Taken

1. **Read spec sections**: ZSpec S4.5–S4.6 (store byte encoding), ZSpec
   S4.7 (branch offset encoding), ZSpec S6.3–S6.4 (variable numbering —
   stack, locals, globals), ZSpec11 "Indirect variable references" (the
   seven opcodes where variable 0 peeks/replaces instead of push/pop),
   ZSpec11 "@jump" (branch target = address_after_branch + offset - 2).

2. **Implemented `MachineState` class** in
   `src/ZMachine.Core/MachineState.cs`:
   - Evaluation stack, locals array (1–15), and globals via Memory.
   - `ReadVariable(byte)` / `WriteVariable(byte, ushort)`: variable 0
     pops/pushes, 1–15 = locals, 16–255 = globals at memory address.
   - `ReadVariableIndirect` / `WriteVariableIndirect`: variable 0 peeks/
     replaces the stack top (no push/pop). Non-zero variables behave
     identically to the normal accessors.
   - `StoreResult(byte variable, ushort value)`: delegates to WriteVariable.
   - `ExecuteBranch(bool condition, BranchInfo, int addressAfterBranch)`:
     static method returning a `BranchResult` — DontBranch, Jump(target),
     ReturnFalse, or ReturnTrue.

3. **Implemented `BranchResult` struct** and `BranchAction` enum to
   represent the four possible outcomes of branch evaluation, avoiding
   magic numbers or out-parameters in the execution engine.

4. **Wrote 30 tests** in `tests/ZMachine.Tests/MachineStateTests.cs`:
   - Stack: push, pop, LIFO order, underflow
   - Locals: read/write, independence, out-of-range errors
   - Globals: read/write, round-trip through memory, highest index (255)
   - Indirect references: peek vs pop, replace vs push, underflow
   - StoreResult: to stack, local, global
   - ExecuteBranch: condition true/false × branch-on-true/false, rfalse,
     rtrue, negative offset, offset 2 (self-jump), condition-not-met
     suppresses rfalse/rtrue

5. **All 143 tests pass** (30 MachineState + 33 decoder + 38 header +
   30 memory + 4 console + 8 framework).

#### Design Decisions

**MachineState as the central execution context**

Rather than making StoreResult and ExecuteBranch free-standing static
helpers, they live on `MachineState` which owns the stack, locals, and
memory reference. This avoids passing the execution context through
every call and gives the future execution engine (Task 7.1) a natural
home for the PC, call stack, and other runtime state.

**BranchResult as a discriminated result type**

`ExecuteBranch` returns a `BranchResult` struct with an `Action` enum
rather than modifying the PC directly. This keeps the branch logic
pure — the execution engine decides what "return from routine" means
without the branch evaluator needing to know about the call stack.

**Indirect variable semantics**

The seven indirect-reference opcodes (inc, dec, inc_chk, dec_chk, load,
store, pull) use a different semantic for variable 0: peek/replace
instead of pop/push. This is implemented as a separate pair of methods
(`ReadVariableIndirect`/`WriteVariableIndirect`) rather than a flag
parameter, because the distinction is always known at the opcode
dispatch level.

#### Spec Interpretation Notes

**Global variable layout**: ZSpec S6.4 says globals are "stored in a
table starting at the address given in the header" as "a table of
240 2-byte words." Global variable g (numbered 16–255) is at byte
address `globals_address + 2 * (g - 16)`. This means the globals table
occupies 480 bytes of dynamic memory.

**Branch target formula**: The spec says target = address_after_branch +
offset - 2. The "address_after_branch" is the byte address immediately
after the branch data bytes (1 or 2 bytes depending on the encoding).
This is `Instruction.NextAddress` after `DecodeBranch` has been called.
The `-2` exists because offset 2 means "jump to the next instruction"
(the default fall-through).

---

### Task 2.3 — Packed Address Calculations

**Date**: 2026-09-06

#### Steps Taken

1. **Read spec section** ZSpec S1.2.3 (packed addresses) and verified
   the formulas against zork1.z3: first `call_vs` instruction at $4F05
   targets packed address $2A39, which unpacks to $5472 (× 2 for V3).
   Confirmed by dumping $5472 — first byte is $03 (3 local variables),
   a valid V3 routine header.

2. **Implemented `AddressHelper`** in `src/ZMachine.Core/AddressHelper.cs`:
   - `UnpackRoutineAddress(packed, version, routinesOffset)` — for call
     targets and V6 initial PC.
   - `UnpackStringAddress(packed, version, stringsOffset)` — for
     print_paddr and abbreviation entries.
   - Both use the same multiplier logic but with separate offset
     parameters for V6–V7.

3. **Wrote 24 tests** in `tests/ZMachine.Tests/AddressHelperTests.cs`:
   - V1–3: × 2 (theory across all three versions), zero, max, zork1 call
   - V4–5: × 4 (theory), max
   - V6–7: × 4 + offset × 8 (routine, string, both, zero offset)
   - V8: × 8 (routine, string, max, offset ignored)
   - Consistency: routine = string outside V6–7

4. **All 167 tests pass**.

#### Design Decisions

**Two methods instead of one with a type parameter**

Routine and string unpacking only differ in V6–7 (where they use
different offsets from the header). Having two explicit methods makes
the call site clear about intent and prevents accidentally passing
the wrong offset. The implementations share the same switch expression
structure.

#### Spec Interpretation Notes

**V6–7 offset semantics**: The header stores `routinesOffset / 8` and
`stringsOffset / 8` as words at $28 and $2A respectively. The unpacking
formula is `packed × 4 + headerWord × 8`. The offset lets V6–7 games
place routines and strings in separate regions of the 512K address space,
each with its own packed address origin.

**V8 ignores offsets**: V8 uses `packed × 8` with no offset, even though
V8 has the same 512K limit as V6–7. The higher multiplier gives full
coverage: $FFFF × 8 = 524,280, just under 512K.

---

### Task 2.4 — Stack and Call Frame Model

**Date**: 2026-09-06

#### Steps Taken

1. **Created `CallFrame` class** (`src/ZMachine.Core/CallFrame.cs`) —
   represents a single routine invocation on the Z-Machine call stack.
   Each frame owns: return PC, store variable, discard-result flag,
   argument count, 0–15 local variables (1-indexed, slot 0 unused),
   and a per-frame evaluation stack.

2. **Created `CallStack` class** (`src/ZMachine.Core/CallStack.cs`) —
   manages a stack of `CallFrame`s. Provides PushFrame, PopFrame,
   CurrentFrame (nullable peek), FrameCount (for CATCH/THROW), and
   GetFramesBottomUp (for Quetzal serialization).

3. **Refactored `MachineState`** — replaced the flat `Stack<ushort>` and
   standalone `Locals` array with a `CallStack` property. Variable
   read/write methods now delegate to `CurrentFrame.EvalStack` and
   `CurrentFrame.Locals`. Operations on variables 0–15 throw
   `InvalidOperationException` if no frame is active.

4. **Updated all existing tests** — the 30 existing `MachineStateTests`
   were refactored to push an initial call frame in the test helper
   (simulating the main routine). Assertions that referenced `state.Stack`
   were changed to access `state.CallStack.CurrentFrame!.EvalStack`.

5. **Added 23 new tests** covering:
   - Frame isolation: nested frames get independent eval stacks and locals
   - Pop restores outer frame's stack and locals
   - No-frame behavior: stack/local ops throw, globals still work
   - CallFrame property preservation (ReturnPC, StoreVariable, etc.)
   - CallStack operations (push/pop count, empty state, bottom-up enumeration)
   - Indirect references across frame boundaries

#### Design Decisions

**Per-frame eval stack vs shared stack with frame markers**

The Z-Machine spec says each routine invocation has its own evaluation
stack (ZSpec S6.3). Two implementation approaches:
- A single shared stack with frame-boundary markers (how many values
  belong to each frame). Quetzal serialization needs this information.
- Per-frame `Stack<ushort>` on each `CallFrame`.

Chose per-frame stacks for simplicity and correctness — each frame's
stack is naturally isolated, and there's no risk of one frame accidentally
accessing another's values. Quetzal serialization can enumerate each
frame's stack directly via `GetFramesBottomUp()`.

**Locals array size 16 with 1-indexed access**

The spec says locals are numbered 1–15 (variable numbers 1–15), so a
16-element array with slot 0 unused maps cleanly: `Locals[variableNumber]`.
This avoids off-by-one errors at every access site. The wasted 2 bytes
per frame are insignificant.

**Throwing on out-of-range local access**

Reading/writing a local beyond `LocalCount` throws rather than silently
returning 0. This catches bugs in the decoder or execution engine early.
The spec doesn't define behavior for accessing non-existent locals,
so failing fast is the safest choice.

#### Spec Interpretation Notes

**Global variables don't need a frame**: Variables 16–255 map directly
to memory at `globalsAddress + 2*(g-16)`. They're machine-wide, not
per-routine, so they work even without an active call frame. This is
important because global access happens during initialization before
the first routine call.

**CATCH/THROW use frame count**: Quetzal S6.2 specifies that CATCH
returns the current frame count and THROW unwinds to a target count.
The `FrameCount` property on `CallStack` supports this directly.

---

## Phase 3: Text System

### Task 3.1 — Z-Character Decoding and Alphabet Tables

**Date**: 2026-09-06

#### Steps Taken

1. **Created `TextDecoder` class** (`src/ZMachine.Core/TextDecoder.cs`) —
   decodes Z-strings (packed 5-bit Z-characters) into readable strings.
   Handles the three default alphabet tables (A0 lowercase, A1 uppercase,
   A2 punctuation/digits), shift characters, the 10-bit ZSCII escape
   sequence, and abbreviation expansion with recursion guards.

2. **Implemented version-dependent shift semantics**:
   - V1–2: z-chars 2,3 are single-shifts; z-chars 4,5 are shift-locks
   - V3+: z-chars 4,5 are single-shifts; z-chars 1,2,3 are abbreviation triggers
   - V1: z-char 1 is a newline (not an abbreviation)
   - V2: only z-char 1 triggers abbreviation lookup

3. **Implemented custom alphabet tables** (V5+): when header word $34
   is non-zero, reads 78 bytes (3×26) as custom alphabets replacing the
   defaults. Each byte is interpreted as a ZSCII code.

4. **Verified against real story files** — decoded object short names from
   minizork.z3 ("forest", "torch", "lunch", "Up a Tree", "Kitchen",
   "Sandy Beach", "brave adventurer") and zork1.z3 ("ZORK owner's manual").

5. **Wrote 23 tests** covering:
   - Real story file decoding (7 tests with minizork and zork1)
   - Synthetic Z-string packing and end-bit detection (3 tests)
   - 10-bit ZSCII escape sequences (2 tests)
   - Abbreviation expansion and recursion guard (3 tests)
   - V1/V2 version-specific behavior (3 tests)
   - Custom alphabet tables (2 tests)
   - Edge cases: empty/padded strings, all spaces, trailing shifts (3 tests)

#### Design Decisions

**Constructor takes addresses, not Header**

The TextDecoder constructor takes `memory`, `version`, `abbreviationTableAddress`,
and `alphabetTableAddress` rather than a `Header` object. This keeps the
decoder testable with synthetic memory (no need to construct a full valid
header) and avoids a circular dependency path if Header ever needs to
decode text.

**Pre-extract all Z-chars, then decode**

The decoder first reads all 16-bit words into a flat list of Z-characters,
then walks the list to produce output. The alternative — decoding on the
fly as words are read — would complicate the shift/abbreviation state
machine. Since Z-strings are short (typically a few words), the list
allocation is negligible.

**V1 A2 alphabet as a separate table**

V1 uses a different A2 alphabet from V2+. Rather than branching inside
the character lookup, a separate `V1A2` table is selected at construction
time. This keeps the hot decode loop branch-free for the common case.

#### Spec Interpretation Notes

**V1–2 shift semantics differ from V3+**: In V1–2, z-chars 2 and 3 are
single-shift characters (shift to next/previous alphabet), while 4 and 5
are shift-locks (sticky shifts). In V3+, this is reversed: 4 and 5 are
single-shifts, and 1, 2, 3 become abbreviation triggers instead.
ZSpec11 "Encoded text" clarifies that even in V3+, consecutive 4/5 codes
should NOT be treated as shift-locks — only as repeated single-shifts.

**Abbreviation recursion is illegal**: ZSpec S3.3 explicitly states that
an abbreviation string must not itself use abbreviations. The decoder
throws on recursive expansion rather than silently producing garbage.

**10-bit ZSCII escape**: When in A2, z-char 6 signals that the next two
z-characters form a 10-bit ZSCII code (hi×32 + lo). This can represent
any ZSCII code 0–1023, though in practice only printable codes are used.

---

### Task 3.2 — Abbreviation Table Expansion

**Date**: 2026-09-06

#### Steps Taken

1. **Verified existing implementation** — the abbreviation expansion
   mechanism was already implemented in `TextDecoder` as part of Task
   3.1: entry indexing `(z-1)×32+x`, word address lookup, byte address
   conversion, recursive decode with recursion guard, and version-
   dependent trigger detection (V1: none, V2: z-char 1, V3+: 1,2,3).

2. **Added 6 real story file tests** verifying end-to-end abbreviation
   expansion against zork1.z3 object names:
   - "pair of hands" (abbreviation for "of ")
   - "The Troll Room" (two abbreviations: "The " and "Room")
   - "large bag" (abbreviation for "large ")
   - "On the Rainbow" (abbreviation for "the ")
   - "South of House" (abbreviation for "of ")
   - Multi-abbreviation verification test

3. **Decoded all 96 abbreviation entries** from zork1.z3 to verify the
   table structure and confirm common words ("the", "you", "is", "of",
   "Room", etc.) match expected Infocom vocabulary.

#### Design Decisions

**No new code needed — test-only task**

All abbreviation machinery was built in Task 3.1 because the text
decoder naturally handles abbreviation triggers as part of the Z-char
decode loop. Separating abbreviation handling into a separate class
would have added indirection without benefit — the abbreviation table
is just a lookup step within the same decode algorithm. Task 3.2
therefore focuses entirely on real-world test coverage.

#### Spec Interpretation Notes

**96 entries in V3+**: The abbreviation table has 96 entries (3 trigger
characters × 32 possible next-z-chars). Each entry is a word address
(not a byte address), so the byte address is entry × 2. Infocom games
use these entries for common words and phrases to compress text — in
zork1.z3, entries include "the ", "you ", "is ", "of ", "Room", and
game-specific words like "Cyclops " and "thief ".

**V2 has only 32 entries**: Only z-char 1 triggers abbreviation in V2,
so only entries 0–31 are used. V1 has no abbreviation support at all.

---

## Task 3.3 — ZSCII Character Set and Unicode Output

**Date**: 2026-09-06
**Branch**: `feature/3.3-zscii-unicode`
**Status**: Complete

### What Was Done

Implemented `ZsciiEncoder` — a bidirectional converter between ZSCII
character codes and Unicode code points. The class handles the full
ZSCII character set: ASCII range (32–126), newline (13), null (0),
and the "extra characters" range (155–251) which defaults to 69
accented Latin characters from ZSpec S3.8.5.

Key features:
- **Default extra characters**: 69 entries (ZSCII 155–223) covering
  Western European accented characters (ä, ö, ü, ß, etc.), ligatures
  (æ, œ), and special punctuation (£, ¡, ¿, », «).
- **Unicode translation table**: V5+ games can override the defaults
  via a table in the header extension (word 1). The table starts with
  a count byte followed by 16-bit Unicode code points.
- **Special quote characters**: ZSCII $27 (39) maps to U+2019 (right
  single quote/apostrophe), $60 (96) to U+2018 (left single quote).
  This follows ZSpec11's clarification that these should NOT be
  rendered as neutral ASCII quote and grave accent.
- **Control code filtering**: Unicode U+0000–U+001F and U+007F–U+009F
  are rejected as control codes per ZSpec11. Newline (U+000A) is
  handled before the filter since it maps to ZSCII 13.
- **BMP validation**: Only U+0000–U+FFFF supported (no non-BMP).
- **Reverse lookup**: `UnicodeToZscii()` builds a dictionary from the
  active extra characters table for O(1) reverse mapping.

### Design Decisions

**Returning `char?` vs exceptions**: `ZsciiToUnicode` returns `null`
for undefined codes rather than throwing. This lets callers decide
how to handle unmapped characters — some contexts require silent
filtering (output), others may want substitution ('?'). This is more
flexible than forcing a specific error policy at the encoder level.

**Newline before control code check**: In `UnicodeToZscii`, the newline
check (`\n` → 13) must precede the control code rejection because
U+000A falls in the control code range U+0000–U+001F. Without this
ordering, newlines would be incorrectly rejected.

**Control code replacement in custom tables**: If a Unicode translation
table contains a control code or non-BMP code point, that entry is
replaced with '?' rather than throwing. Games with malformed tables
should still be playable.

**Separate class from TextDecoder**: `ZsciiEncoder` is independent of
`TextDecoder` by design. `TextDecoder` operates on Z-characters (5-bit
codes packed into words), while `ZsciiEncoder` maps between ZSCII
(10-bit codes) and Unicode. They serve different layers: Z-char
decoding produces ZSCII codes, which then pass through `ZsciiEncoder`
for final Unicode output. Currently `TextDecoder` maps directly via
the alphabet tables, but a future refactoring could route 10-bit ZSCII
escapes and extra characters through `ZsciiEncoder`.

### Spec Interpretation Notes

**$27 and $60 confusion**: The Z-Machine spec inherits historic
confusion from ASCII/Latin-1. In the original ASCII standard, code $27
(decimal 39) is "apostrophe" and $60 (decimal 96) is "grave accent".
But ZSpec11 specifically clarifies that for the Z-Machine, $27 should
render as a right single quote (U+2019) and $60 as a left single quote
(U+2018). The grave accent interpretation is explicitly discouraged.
Infocom themselves used $27 almost exclusively as an apostrophe.

**Default table has 69 entries, not 97**: Although ZSCII codes 155–251
span 97 possible values, the standard default table only defines 69
entries (155–223). Codes 224–251 are undefined by default and return
null. A custom Unicode translation table can define fewer or more
entries as needed.

### Test Coverage (80 tests)

- ASCII range mapping (5 forward, 5 reverse)
- Special quote characters ($27/$60, 4 tests)
- Newline and null handling (3 tests)
- Default extra characters: individual spot checks (20 tests) plus
  full round-trip of all 69 entries
- Beyond-default-table returns null
- Undefined ZSCII codes (9 edge cases)
- Control code detection (6 true, 4 false)
- Control code rejection in UnicodeToZscii (6 tests)
- BMP code point validation (6 tests)
- Custom Unicode translation table (4 tests: override, reverse lookup,
  control code replacement, zero-address fallback)
- Japanese CJK characters in custom table
- Edge cases: unmapped characters, adjacent ASCII codes, straight
  apostrophe and grave accent input mapping

---

## Task 3.4 — Text Encoding for Dictionary Lookup

**Date**: 2026-09-06
**Branch**: `feature/3.4-text-encoding`
**Status**: Complete

### What Was Done

Implemented `TextEncoder` — the reverse of `TextDecoder`. Where
`TextDecoder` unpacks Z-characters into readable text, `TextEncoder`
packs text into Z-character form for dictionary lookup (used by
`@tokenise` and `@read` to match player input against dictionary
entries).

Key features:
- **V1-3**: 4-byte output (2 words, 6 Z-characters)
- **V4+**: 6-byte output (3 words, 9 Z-characters)
- **Alphabet search order**: A0 first (no shift), then A1 (shift 4 in
  V3+), then A2 (shift 5 in V3+), then 10-bit ZSCII escape (shift +
  z-char 6 + high 5 bits + low 5 bits = 4 z-chars)
- **V1-2 shift-lock**: When consecutive characters share a non-A0
  alphabet, uses shift-lock (z-chars 4/5) instead of single-shift
  (z-chars 2/3). Requires tracking locked alphabet state across the
  encoding loop.
- **V1-2 truncation rule**: If truncation to 6 z-chars breaks a
  multi-z-char construction (shift without payload, or partial ZSCII
  escape), the end-bit of the last word is NOT set.
- **Padding**: Unused z-char slots filled with z-char 5.
- **Custom alphabet support**: V5+ custom alphabets via memory address.

### Design Decisions

**Stateful encoding for V1-2 shift-locks**: The initial implementation
encoded each character independently, which meant after a shift-lock the
next character would redundantly emit another shift. Refactored to track
`lockedAlphabet` across the encoding loop — when locked to A1, characters
in A1 emit directly without a shift prefix, matching how the decoder
works. This mirrors the `int lockedAlphabet` approach used in
`TextDecoder`.

**Separate IsTruncatedIncomplete pass**: The V1-2 truncation rule
requires knowing whether the full (untruncated) encoding would break a
multi-z-char construction at the truncation point. This is computed by a
separate pass that encodes without padding and walks the resulting z-char
list to find construction boundaries. This avoids complicating the main
encoding loop with truncation awareness.

**V1-2 shift direction**: Z-chars 2/4 shift "up" (A0→A1→A2→A0) and
3/5 shift "down" (A0→A2→A1→A0). The direction depends on the current
locked alphabet, not always from A0. Implemented as `(to - from + 3) % 3`
to compute delta 1 (up) vs delta 2 (down).

### Spec Interpretation Notes

**"Next two characters" for shift-lock**: ZSpec11 says "shift-lock
Z-characters 4 and 5 are used instead of single-shift 2 and 3 when
the next two characters come from the same alphabet." This means: when
encoding character at position i, if character at position i+1 is in
the same non-A0 alphabet, use shift-lock for character i. The lock
then covers both i and i+1 (and beyond if more follow).

**Zork I dictionary verification**: Verified encoding against real
zork1.z3 dictionary entries — "mailbox" (0x453F: 48 CE C4 F4),
"hello" (0x42DE: 35 51 C6 85), "north" (0x461F: 4E 97 E5 A5). The
"h2o" entry (34 AA D0 A5) exercises A2 shifts for digits.

### Test Coverage (30 tests)

- Basic V3 encoding: mailbox, hello, north against zork1 dictionary (3)
- Padding and truncation: short words, long words (4)
- V3 end bit and byte count (3)
- V4+ encoding: 6-byte output, 9 z-char capacity, end bit (4)
- Shift characters: uppercase (A1), digits (A2), comma, h2o, space (5)
- 10-bit ZSCII escape: '@' character, mixed text (2)
- V1-2 shift-lock: consecutive A1, consecutive A2, single non-A0,
  comparison with V3 single-shift (4)
- V1-2 truncation: incomplete shift end-bit unset, complete end-bit
  set, V3 always set (3)
- Real story file: multiple zork1 entries verified (1)
- Custom alphabet: reversed A0 in V5 (1)

---

## Phase 4: Object System

### Task 4.1 — Object Table and Tree Traversal

**Date**: 2026-09-06
**Branch**: `feature/4.1-object-table` (from `feature/3.4-text-encoding`)
**Files**: `src/ZMachine.Core/ObjectTable.cs`, `tests/ZMachine.Tests/ObjectTableTests.cs`

#### Design Decisions

**Constructor-based layout selection**: The `ObjectTable` constructor accepts
version and table address, computing all layout parameters (entry size, attribute
byte count, pointer size, defaults count) at construction time. This avoids
branching on version in every accessor method.

**Version-conditional pointer size**: V1-3 uses 1-byte parent/sibling/child
pointers (max 255 objects), V4+ uses 2-byte pointers (max 65535). A single
`_pointerSize` field drives `ReadByte` vs `ReadWord` in getters, keeping the
tree traversal code unified across versions.

**Insert/Remove as spec-mandated pair**: `InsertObject` first calls
`RemoveObject` to detach from any existing parent, then prepends as first
child — matching the semantics of `@insert_obj` exactly (ZSpec S12). The
old first child becomes the inserted object's sibling.

**RemoveObject sibling-chain walk**: Removing a non-first child requires
walking the parent's child chain to find the predecessor. This is O(n) in
sibling count, but Z-Machine games rarely have more than a dozen children
per container, so it's fine.

**Attribute bit layout**: Attribute 0 = top bit of first byte, attribute N
is at `byte[N/8]` bit `7-(N%8)`. This matches the spec's definition and
the big-endian storage model used throughout the Z-Machine.

#### Real Story File Exploration

Used Python to map the zork1.z3 object tree. Key findings:

- Object table at 0x02B0, property defaults (31 words = 62 bytes),
  entries start at 0x02EE, each 9 bytes.
- Object 82 is the root rooms container.
- Object 180 ("West of House"): parent=82, sibling=15, child=181.
- Object 181 ("door"): parent=180, sibling=160, child=0.
- Object 160 ("mailbox"): parent=180, sibling=0, child=161.
- Object 161 ("leaflet"): parent=160, sibling=0, child=0.
- Object 4 ("cretin") attrs byte 0 = 0x01, confirming attribute 7 set.

These values became test expectations for the real-story-file tests.

#### Synthetic Test Data Design

Created two synthetic helpers:

- `CreateSyntheticV3()`: V3 layout at address 0x02B0 (matching zork1 for
  mental mapping), 10 objects with zeroed attributes/pointers and dummy
  property tables. Used for insert/remove/attribute mutation tests.

- `CreateSyntheticV5()`: V5 layout at 0x0100 with 63 property defaults,
  14-byte entries, 2-byte pointers. Used to verify V4+ attribute range
  (0-47), property default count (63), and 2-byte pointer operations.

Both helpers follow the established Memory pattern: parameterless
constructor + `LoadStory(byte[])`, with valid version byte, minimum 64
bytes, and correct static base word at 0x0E.

### Test Coverage (33 tests)

- Real story tree traversal: parent/child/sibling chains for objects
  180, 181, 160, 161 (7)
- Object 0 null sentinel: throws on access (1)
- Attributes on real story: mailbox readable, cretin attr 7 set (2)
- Property defaults: readable 1-31 without crash (1)
- Property table address: mailbox has non-zero address with name (1)
- Synthetic insert: into empty parent, pushes existing child, moves
  between parents (3)
- Synthetic remove: first child, middle child, last child, only child,
  no-parent no-op (5)
- Multiple insertions: chain ordering verified (1)
- Attribute bit layout: attr 0 = top bit byte 0, attr 7 = bottom bit
  byte 0, attr 31 = bottom bit byte 3 (3)
- Attribute range validation: out-of-range throws (1)
- Attribute isolation: setting one doesn't affect neighbors (1)
- Set/clear/test round-trip (2)
- V5 two-byte pointers: large object numbers (1)
- V5 property defaults: 63 entries, 64 throws (1)
- V5 attributes: 48-bit range, attr 48 throws (1)
- Property default value: write/read round-trip (1)
- Property default range: 0 and 32 throw (1)

---

### Task 4.2 — Property System

**Date**: 2026-09-07
**Branch**: `feature/4.1-object-table` (extended, same branch as 4.1)
**Files**: `src/ZMachine.Core/ObjectTable.cs` (extended), `tests/ZMachine.Tests/ObjectTableTests.cs` (extended)

#### Design Decisions

**Extending ObjectTable rather than a separate class**: Properties are
tightly coupled to the object entry's property pointer, so the methods
belong on `ObjectTable`. The private `FindProperty` helper centralizes
property-list walking and size-byte decoding.

**V1-3 size byte layout**: `size_byte = 32*(data_len-1) + prop_number`.
Bottom 5 bits = property number, top 3 bits = data length minus one
(1-8 bytes). TASKS.md had the bits reversed (top 5 = prop, bottom 3 =
len) — caught by testing against zork1.z3 where property numbers are
5-bit values (up to 31).

**V4+ two-form size byte**: Bit 7=0 is the short form — bit 6 selects
1 or 2 byte data, bits 5-0 hold the property number. Bit 7=1 is the
long form — a second byte follows where bits 5-0 hold the data length
(0 means 64 bytes, per spec). The second byte also has bit 7 set, which
`GetPropertyLength` uses to distinguish which byte precedes the data.

**Early exit via descending order**: `FindProperty` returns 0 as soon as
it encounters a property number less than the target, since properties
are stored in strictly descending order.

**GetPropertyLength(0) = 0**: Explicitly required by ZSpec11 for
`@get_prop_len`. Implemented as a guard at the top of the method.

**GetProperty on large properties**: The spec says `@get_prop` on
properties with more than 2 bytes is undefined. We read the first 2
bytes as a word (or 1 byte if `data_len == 1`), matching Frotz behavior.

#### Real Story File Verification

Decoded zork1.z3 object 160 (mailbox) property list:
- Prop 18: 4 bytes, data at 0x1A40, first word = 0x453F
- Prop 17: 2 bytes, data at 0x1A45, value = 0x6E94
- Prop 16: 1 byte, data at 0x1A48, value = 0xF4
- Prop 10: 2 bytes, data at 0x1A4A, value = 0x000A

Object 180 (West of House) has 10 properties: 31→30→29→28→27→25→24→21→17→5.
Property 15 default = 0x0005 (only non-zero default in the table).

Also verified czech.z5 V4+ format: object 5 has property 7 in long form
(2-byte size header, 6 bytes data) and properties 5 and 4 in short form.

### Test Coverage (29 new property tests, 62 total ObjectTable tests)

- Real story property reads: mailbox 4-byte/2-byte/1-byte properties (3)
- Absent property returns default from defaults table (1)
- GetNextProperty from 0 returns first property (1)
- GetNextProperty full chain walk: mailbox (4 props), West of House (10 props) (2)
- GetPropertyAddress: present returns data address, absent returns 0 (2)
- GetPropertyLength: V3 size byte decoding for 1/2/4 byte props (1)
- GetPropertyLength(0) returns 0 per ZSpec11 (1)
- Short name address and length (2)
- Synthetic V3: get 1-byte/2-byte property, absent returns default (3)
- Synthetic V3: set 1-byte/2-byte property, set absent throws (3)
- Synthetic V3: GetNextProperty from 0, chain walk, not-found throws (3)
- Synthetic V3: GetPropertyLength from data address (1)
- V5 short form: 1-byte (bit 6=0) and 2-byte (bit 6=1) (2)
- V5 long form: 6-byte data read (1)
- V5 GetPropertyLength: short form and long form (2)
- V5 GetNextProperty full chain (1)

---

### Task 4.3 — Attribute System

**Date**: 2026-09-07
**Branch**: `feature/4.3-attribute-system` (from `feature/4.2-property-system`)
**Files**: `src/ZMachine.Core/ObjectTable.cs` (modified), `tests/ZMachine.Tests/ObjectTableTests.cs` (extended)

#### Design Decisions

**Warning instead of crash for out-of-range attributes**: The deliverable
specifies "out-of-range attributes produce a warning, not a crash."
Changed `ValidateAttribute` from throwing `ArgumentOutOfRangeException` to
returning `false` and raising a `Warning` event. `TestAttribute` returns
false, `SetAttribute`/`ClearAttribute` silently no-op. This matches the
spec's intent that buggy games should not crash the interpreter.

**Warning event pattern**: Added `event Action<string>? Warning` to
`ObjectTable`. This is a lightweight notification — no dependency on a
logging framework, easily subscribed to by tests and the future
interpreter loop.

**Attribute methods already existed from Task 4.1**: The core bit
manipulation logic (`TestAttribute`, `SetAttribute`, `ClearAttribute`,
`ValidateAttribute`) was implemented in Task 4.1 alongside the tree
operations. Task 4.3 focused on the behavioral change (warning vs crash)
and adding comprehensive real-data tests against zork1.z3.

#### Real Story File Verification

Verified raw attribute bytes for three zork1 objects:
- Object 4 ("cretin"): 0x01420002 → attrs 7, 9, 14, 30
- Object 160 ("mailbox"): 0x00041000 → attrs 13, 19
- Object 180 ("West of House"): 0x02400800 → attrs 6, 9, 20

Set/clear round-trip test on mailbox: set attr 0, verify attr 13 still
intact, clear attr 0, verify attr 13 still intact.

### Test Coverage (4 new tests, 66 total ObjectTable tests)

- Zork1 attribute bytes: mailbox (attrs 13, 19), cretin (attrs 7, 9,
  14, 30), West of House (attrs 6, 9, 20) — exact values verified (3)
- Zork1 set/clear round-trip with isolation check (1)
- Out-of-range: TestAttribute warns + returns false, SetAttribute warns
  + no-ops, ClearAttribute warns + no-ops (3, replacing 1 old throw test)
- V5 out-of-range: attr 48 warns + returns false (updated from throw)

---

## Phase 5: I/O and Screen Model

### Task 5.1 — Text Output Backend (Console)

**Date**: 2026-09-07
**Branch**: `feature/5.1-console-screen` (from `feature/4.3-attribute-system`)
**Files**: `src/ZMachine.IO/ConsoleScreen.cs` (rewritten), `tests/ZMachine.Tests/ConsoleScreenTests.cs` (rewritten)

#### Design Decisions

**TextWriter injection for testability**: The Phase 1 stub wrote directly
to `Console.Write()`, making output capture impossible. Refactored the
constructor to accept `TextWriter`, column/row dimensions, and an
`isTerminal` flag. Tests pass a `StringWriter` with `isTerminal: false`
to capture output without ANSI escape codes. The parameterless
constructor delegates to `Console.Out` for real terminal use.

**Word wrapping in buffer mode**: When `_bufferMode` is true, characters
accumulate in `_lineBuffer`. On each character, if `_cursorColumn +
buffer.Count > columns`, `FlushWithWordWrap()` finds the last space that
fits and breaks there. The space is consumed (not emitted), and the
remainder stays in the buffer. If no space fits and cursor is at column 0,
the word is force-broken at the screen width. This matches the spec
(ZSpec S8.4) and produces clean word-wrapped output at any width.

**Buffer mode off**: Characters pass through directly. A hard line break
is emitted when the cursor reaches the screen width, matching terminal
behavior for unbuffered output.

**Status line formatting**: Extracted as `public static FormatStatusLine`
for direct unit testing. Location is left-aligned, score/time is
right-aligned, padded with spaces. If the combined text exceeds the
width, it's truncated. Minimum 1 space of padding between the two parts.

**Upper window cursor tracking**: When window 1 is selected, each
character is written at `(_upperCursorLine, _upperCursorColumn)`, which
advances with each character. Characters beyond `_upperWindowLines` are
silently dropped. In terminal mode, ANSI cursor positioning codes are
emitted; in test mode, characters are written inline.

**ANSI codes gated by isTerminal**: All escape sequences (`\x1b[...`)
are suppressed in non-terminal mode. This includes `SetTextStyle`,
`EraseLine`, `EraseWindow`, `SetCursor`, and the status line
save/restore cursor sequences. Tests verify output content without
parsing ANSI noise.

#### Alternatives Considered

**Separate WordWrapper class**: Could have extracted word wrapping into
a standalone utility, but the wrapping logic is tightly coupled to the
screen's cursor column state. Keeping it as private methods on
ConsoleScreen avoids exposing internal state.

**InternalsVisibleTo for FormatStatusLine**: Initially made the method
`internal static`, but this requires assembly-level attributes. Made it
`public static` instead — it's a pure function with no side effects,
safe to expose.

### Test Coverage (28 tests, replacing 4 old smoke tests)

- Word wrapping: short line, exact width, wrap at space, multiple words,
  force-break long word, 80-column sentence, multiple prints accumulate,
  successive wraps, trailing space at boundary (13)
- Buffer mode off: no word wrapping (1)
- Buffer mode toggle: disable flushes buffer (1)
- Newline: flushes buffer, consecutive spaces preserved, empty print (3)
- Status line: left/right alignment, full width, truncation, minimum
  padding, ShowStatusLine output (5)
- Window management: split/set upper, unsplit, SetCursor (3)
- Text style: non-terminal suppresses ANSI (1)
- PrintChar: single characters, newline flushes (2)
- Screen size: returns constructor values (1)
- Erase: EraseWindow -1 unsplits, EraseLine non-terminal (2)

---

### Task 5.2 — Output Stream Management

**Date**: 2026-09-07
**Branch**: `feature/5.2-output-streams` (from `feature/5.1-console-screen`)
**Files**: `src/ZMachine.Core/OutputStreamManager.cs` (new), `tests/ZMachine.Tests/OutputStreamManagerTests.cs` (new)

#### Design Decisions

**Placed in Core, not IO**: `OutputStreamManager` depends on `Memory`
(for stream 3 table writes) and is used by the interpreter loop, so it
belongs in Core. The screen callback is an `Action<string>` delegate
rather than a direct `IScreen` dependency — the interpreter wires the
two together, keeping Core independent of IO.

**Delegate-based stream dispatch**: `ScreenPrint`, `TranscriptPrint`,
and `CommandPrint` are `Action<string>?` properties. This avoids
requiring stream 2/4 file writers to be provided at construction time;
they can be attached later when the game enables transcripting or
command recording.

**Stream 3 suppression**: Per ZSpec S7.2, when stream 3 is active ALL
other streams are suppressed. The `Print` method checks `_stream3Depth`
first and short-circuits to `WriteToStream3`. This is a hard behavioral
requirement — some games rely on stream 3 to capture text without
side effects on screen.

**Stream 3 nesting**: The spec allows up to 16 nesting levels. Each
`SelectStream(3, addr)` pushes a table address onto a fixed-size stack
and initializes the count word to 0. `SelectStream(-3)` pops one level.
Exceeding 16 levels throws — this is a game bug. Deselecting when not
active is a no-op (defensive).

**Stream 3 table format**: Word at offset 0 = character count (updated
incrementally), ZSCII bytes starting at offset 2. Each character is
written as a single byte. Multiple `Print` calls accumulate into the
same table.

### Test Coverage (22 tests)

- Stream 1: active by default, disable suppresses, re-enable works (3)
- Stream 2: inactive by default, enable receives text, both screen +
  transcript, disable stops, IsTranscriptActive property (5)
- Stream 4: inactive by default, enable receives text (2)
- Stream 3 capture: writes chars to memory table, count word correct,
  suppresses all other streams, resumes after deselect, initializes
  count to zero, multiple prints accumulate, IsStream3Active (7)
- Stream 3 nesting: two levels with separate tables, inner doesn't
  affect outer, max depth throws, deselect when inactive no-ops (4)
- PrintChar: dispatches to screen, captured by stream 3 (2)

---

### Task 5.3 — Dictionary and Lexical Analysis

**Date**: 2026-09-07
**Branch**: `feature/5.3-dictionary-tokenizer` (from `feature/5.2-output-streams`)
**Files**: `src/ZMachine.Core/Dictionary.cs` (new), `src/ZMachine.Core/Tokenizer.cs` (new), `tests/ZMachine.Tests/DictionaryTests.cs` (new)

#### Design Decisions

**Dictionary placed in Core**: The dictionary is a data structure read
from story memory — no IO dependency. The `Dictionary` class takes a
`Memory`, version, and `TextEncoder` at construction time. `Parse()` reads
the header (separators, entry length, entry count, entries start). `Lookup()`
encodes the word and searches.

**Binary search for sorted dictionaries**: ZSpec S13 says the entry count
is signed — positive = sorted, negative = unsorted. Most game dictionaries
are sorted. `Lookup` uses binary search for sorted (O(log n), efficient
for zork1's 697 entries) and linear scan for unsorted.

**CompareEntry byte-by-byte**: Dictionary entries are compared as raw byte
sequences (4 bytes V1-3, 6 bytes V4+). This avoids reconstructing words
from the dictionary — we just compare the encoded form from TextEncoder
against the entry's encoded bytes.

**Tokenizer as a separate class**: The tokenizer splits input, encodes
tokens, looks them up, and writes parse buffer entries. It depends on
`Dictionary` and `TextEncoder` but not on `Memory` for its core splitting
logic — `SplitIntoTokens` is a static method for testability.

**Token splitting**: Spaces delimit words but are not tokens. Dictionary
separators (e.g. comma, period, quote) are tokens in their own right and
are looked up in the dictionary. Adjacent separators each become separate
tokens. This matches the spec (ZSpec S13.1).

**Parse buffer layout**: Byte 0 = max words (game-set), byte 1 = word
count (set by tokenizer), then 4 bytes per word: dict address (word),
text length (byte), text position (byte). The `textBufferOffset` parameter
handles V1-4 (offset 1) vs V5+ (offset 2) positioning.

**skipUnrecognized flag**: When true (ZSpec11 "@tokenise" 4th operand),
unknown words are not written to the parse buffer. This allows games to
re-tokenize with a custom dictionary while preserving already-recognized
entries.

#### Pitfall: "xyzzy" in zork1

Initially used "xyzzy" as an "unknown word" test case — it turns out
zork1.z3 actually has "xyzzy" in its dictionary (entry 687 at 0x4DF1).
Changed to "qqqqq" which genuinely has no dictionary entry.

### Test Coverage (25 tests)

- Dictionary parsing: separators, entry length, count, entries start (4)
- Dictionary lookup: open, mailbox, north, take, look, west — all
  verified against known addresses (6)
- Lookup not found: "qqqqq" returns 0 (1)
- Lookup truncation: "mailbox" = "mailbo" (V3 6-char limit) (1)
- Token splitting: simple words, separator, leading/multiple spaces,
  separator only, empty input, adjacent separators (7)
- Tokenize "open mailbox": parse buffer with dict addresses, lengths,
  positions verified (1)
- Tokenize unknown word: dict address = 0 (1)
- Tokenize with separator: look,north splits to 3 tokens (1)
- Tokenize max words: truncation at limit (1)
- Skip unrecognized: unknown word omitted from parse buffer (1)
- Unsorted dictionary: linear search finds entry (1)

---

### Task 5.4 — Input System

**Date**: 2026-09-07

#### Steps Taken

1. **Updated `IInputStream` interface** to add timeout parameters and a
   `HasMore` property. The interface now exposes `ReadLine(int maxLength,
   int timeoutTenths = 0)` returning a tuple of text and terminating
   character, `ReadChar(int timeoutTenths = 0)` returning a ZSCII code,
   and `bool HasMore` for file exhaustion detection.

2. **Implemented `ConsoleInputStream`** (stream 0 — keyboard input).
   Handles both blocking and timed input using `Console.ReadKey`. Timed
   input uses a `Stopwatch`-based polling loop with 10ms sleep granularity,
   resetting the timer on each keypress per ZSpec S10.7. The `MapKeyToZscii`
   method maps `ConsoleKeyInfo` to ZSCII codes: cursor keys → 129-132,
   function keys F1-F12 → 133-144, numpad 0-9 → 145-154, plus enter (13),
   backspace (8), escape (27), and printable ASCII.

3. **Implemented `FileInputStream`** (stream 1 — file playback). Uses a
   `Queue<string>` of pre-loaded lines. Each `ReadLine` dequeues one line,
   and `HasMore` returns false when the queue is empty. `ReadChar` dequeues
   the first line and returns its first character. Truncation to `maxLength`
   is handled at read time.

4. **Implemented `InputStreamManager`** to handle `@input_stream` switching
   between stream 0 (keyboard) and stream 1 (file). When file input is
   exhausted after a read, the manager automatically reverts to stream 0.
   The manager itself implements `IInputStream` so the interpreter can treat
   it as a single input source.

5. **Implemented `ReadHandler`** for `@read` (sread/aread) opcode logic.
   Handles the V1-4 vs V5+ text buffer format divergence:
   - V1-4: text starts at byte 1, null-terminated
   - V5+: byte 1 = character count, text starts at byte 2, not null-terminated

   Input is lowercased before writing to the buffer (ZSpec S10 requirement).
   Tokenization delegates to the existing `Tokenizer` class with the
   appropriate text offset. Returns 13 (enter) as the terminating character.

#### Design Decisions

**`ReadHandler` in Core, not IO**: The `ReadHandler` lives in `ZMachine.Core`
because it operates on `Memory`, `Tokenizer`, and `Dictionary` — all Core
types. It doesn't need the `IInputStream` directly; the interpreter will
call `ReadLine` on the input stream and pass the resulting string to
`ProcessRead`. This keeps the Core layer free of IO dependencies.

**`ConsoleInputStream` timeout strategy**: Considered using `Task.Run` with
`Console.ReadKey` and `CancellationToken`, but `Console.ReadKey` is a
blocking call that can't be cancelled cleanly on all platforms. Instead,
the timed `ReadLine` polls `Console.KeyAvailable` in a loop with 10ms
sleep intervals and a `Stopwatch` for wall-clock accuracy. The timer
resets after each keypress per ZSpec S10.7 — the timeout is between
keypresses, not total.

**`InputStreamManager` implements `IInputStream`**: The manager wraps both
keyboard and file streams behind the same interface. This means the
interpreter only needs one `IInputStream` reference. Auto-fallback on
exhaustion happens inside `ReadLine`/`ReadChar` — the caller doesn't need
to check.

**`MapKeyToZscii` made public**: Originally `internal static`, but tests
in the separate test project need access. Since it's a pure, stateless
mapping function with no side effects, making it `public` is appropriate.

#### Lessons Learned

- **Timed input is platform-dependent**: `Console.ReadKey` blocks the
  calling thread and can't be interrupted. The polling approach works but
  adds ~10ms latency jitter. A GUI frontend (Avalonia) will use event-driven
  input instead, so this is acceptable for the console fallback.

- **V1-4 vs V5+ buffer format is easy to get wrong**: The off-by-one between
  "text at byte 1" (V1-4) and "text at byte 2 with count at byte 1" (V5+)
  must also be reflected in the `textBufferOffset` passed to the tokenizer
  so word positions in the parse buffer are correct relative to the text
  buffer start.

### Test Coverage (23 tests)

- FileInputStream: ReadLine returns first line, advances through lines,
  returns empty on exhaustion, truncates to max length, ReadChar returns
  first char, HasMore tracks state (6)
- InputStreamManager: defaults to stream 0, switches to stream 1, reverts
  on exhaustion, switches back to stream 0, ReadChar from file (5)
- ReadHandler V3: writes text buffer (lowercased, null-terminated),
  tokenizes "open mailbox" with correct dict addresses, returns 13,
  lowercases input (4)
- ReadHandler V5: writes text buffer (count byte + text, no null term),
  skip tokenization when parse addr = 0, truncates long input (3)
- Key mapping: enter→13, cursor keys→129-132, F1→133/F12→144,
  printable ASCII passthrough, escape→27 (5)

---

### Task 5.5 — Status Line and Window Management (V1–V5)

**Date**: 2026-09-07

#### Steps Taken

1. **Implemented `StatusLineHandler`** in Core. Reads the Z-Machine state
   needed to construct a V1-3 status line: global variable 0 → object
   number → short name via `ObjectTable.GetShortNameAddress` + `TextDecoder.
   DecodeZString`; globals 1/2 → score/turns or hours:minutes; header
   Flags 1 bit 1 → time vs score mode. `BuildStatusLine()` returns a
   `(Location, ScoreOrTime)` tuple for the caller to render.

2. **Implemented `WindowManager`** in IO. Coordinates between `IScreen`
   and Core types for window management:
   - `SplitWindow(lines)` — tracks upper window size, delegates to IScreen
   - `SetWindow(window)` — tracks current window, delegates to IScreen
   - `SetCursor(line, column)` — implements implicit split expansion per
     ZSpec11 "@set_cursor": if the cursor targets a line below the current
     split in the upper window, the split expands to include that line
   - `EraseWindow(window)` — delegates to IScreen, handles unsplit on -1
   - `ShowStatusLine(StatusLineHandler)` — V3 only; no-ops for V4+

3. **Verified against zork1.z3**: the status line correctly reads object
   180's short name as "West of House" using the full TextDecoder pipeline
   including abbreviation expansion.

#### Design Decisions

**WindowManager in IO, not Core**: `WindowManager` depends on both `IScreen`
(IO) and `StatusLineHandler` (Core). Since IO already references Core, this
avoids a circular dependency. The alternative — moving `IScreen` to Core —
would work architecturally (it's a pure interface), but is a larger refactor
that can be done later if needed.

**StatusLineHandler in Core**: It only depends on `Memory`, `ObjectTable`,
and `TextDecoder` — all Core types. It doesn't touch `IScreen` directly;
it builds the data, and `WindowManager` (or the interpreter) renders it.

**Implicit split in WindowManager, not ConsoleScreen**: The implicit split
is a Z-Machine semantic (ZSpec11 "@set_cursor") rather than a screen
rendering concern. Putting it in `WindowManager` keeps `ConsoleScreen`
as a pure rendering backend and makes the behavior testable with a mock
screen — which also ensures GUI backends get implicit split for free.

#### Lessons Learned

- **Status line needs the full text decoding pipeline**: Object short names
  are Z-encoded strings that may reference abbreviations. A minimal decoder
  won't suffice — the `TextDecoder` with its abbreviation table address is
  required. This was straightforward since TextDecoder was already built in
  Phase 3, but the dependency chain (Memory → ObjectTable → TextDecoder →
  abbreviation table from header) means the status line handler needs all
  of these wired up before it can produce output.

- **Flags 1 bit 1 is game-set, not interpreter-set**: The time/score
  distinction comes from the story file, not the interpreter. The handler
  reads it once at construction and caches it.

### Test Coverage (19 tests)

- StatusLineHandler score game: format score/turns, negative score,
  isTimeGame flag (3)
- StatusLineHandler time game: format hours:minutes, isTimeGame flag (2)
- StatusLineHandler location: zork1 "West of House" from object 180,
  full BuildStatusLine, object 0 returns empty (3)
- WindowManager split: tracks lines, unsplit resets to 0 (2)
- WindowManager set window: tracks current window (1)
- WindowManager set cursor: within split no expansion, below split
  expands implicitly, lower window no expansion (3)
- WindowManager erase: -1 unsplits, -2 keeps split, 0 clears lower (3)
- WindowManager status line: V3 delegates to screen, V5 does nothing (2)

---
