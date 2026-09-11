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

## Phase 6: Full Instruction Set

### Task 6.1 — Arithmetic, Logical, and Comparison Opcodes

**Date**: 2026-09-07

#### Steps Taken

1. **Implemented `ArithmeticOps`** in Core. Contains all arithmetic, bitwise,
   shift, comparison, and random opcodes as methods:
   - **Arithmetic** (`Add`, `Sub`, `Mul`, `Div`, `Mod`): All operate on
     unsigned 16-bit words interpreted as signed values via `(short)` cast.
     Overflow wraps naturally. Division/modulo by zero throws
     `DivideByZeroException` per spec.
   - **Bitwise** (`And`, `Or`, `Not`): Unsigned 16-bit operations.
   - **Shifts** (`LogShift`, `ArtShift`): Positive places = left shift,
     negative = right shift. Logical shift zero-fills on right shift;
     arithmetic shift sign-extends. Range validation: -15..+15.
   - **Comparisons** (`JumpEqual`, `JumpLessThan`, `JumpGreaterThan`,
     `JumpZero`, `Test`): Return bool for the branch condition. `JumpEqual`
     accepts 2-4 operands and branches if first equals any; 1 operand
     throws.
   - **Random**: Instance method (needs mutable RNG state). Positive range
     returns 1..range. Negative seeds deterministically. Zero re-randomizes.

#### Design Decisions

**Static vs instance methods**: Arithmetic, bitwise, shift, and comparison
methods are all `static` — they're pure functions with no state. `Random` is
an instance method because it maintains a `Random` object for seeding and
re-randomization. The `ArithmeticOps` class is instantiated once per
interpreter.

**Signed interpretation via `(short)` cast**: Rather than maintaining
separate signed/unsigned types, all values are stored as `ushort` and cast
to `short` where the spec requires signed interpretation (arithmetic,
comparisons). This matches how the Z-Machine works: unsigned storage with
signed interpretation. The cast handles two's complement naturally.

**Division rounding toward zero**: C#'s integer division already rounds
toward zero for both positive and negative operands, matching the spec
requirement (ZSpec11 "@div and @mod"). No special handling needed.

**`@not` as a single method**: V1-4 has `@not` as a 1OP instruction, V5+
moves it to the VAR table. The operation is identical — the version
distinction only matters for opcode decoding, not execution. One method
serves both.

#### Lessons Learned

- **`unchecked` required for negative ushort literals in tests**: C# won't
  implicitly convert a negative `short` to `ushort` at compile time without
  `unchecked`. Every test case with a negative Z-Machine value needs
  `unchecked((ushort)(short)-N)`.

### Test Coverage (63 tests)

- @add: positive, negative, overflow wrap, mixed sign (4)
- @sub: positive, negative result, underflow wrap (3)
- @mul: positive, negative×positive, overflow wrap, by zero (4)
- @div: positive, rounds toward zero (positive and negative), neg÷neg,
  division by zero throws (5)
- @mod: positive, negative dividend, negative divisor, no remainder,
  division by zero throws (5)
- @and, @or, @not: masks, sets, inverts (3)
- @log_shift: left, right zero-fill, high bit zero-fill, zero places,
  max left/right, out-of-range throws (7)
- @art_shift: left, right sign-extends, right positive zero-fill,
  high bit sign-extends, out-of-range throws (5)
- @je: 2 operands equal/not-equal, 3 operands match first/second/none,
  4 operands match third/none, 1 operand throws (8)
- @jl: true, false, equal, signed comparison (4)
- @jg: true, false, signed comparison (3)
- @jz: zero, non-zero, 0xFFFF (3)
- @test: all bits set, not all set, exact match, zero flags (4)
- @random: positive range in bounds, seed returns 0, seed deterministic,
  zero re-randomizes, range 1 always returns 1 (5)

---

### Task 6.2 — Variable, Memory, and Table Opcodes

**Date**: 2026-09-07

#### Steps Taken

1. **Implemented `VariableMemoryOps`** as a static class in Core with three
   groups of opcodes:

   **Variable opcodes** — all seven indirect-reference opcodes that use
   `MachineState.ReadVariableIndirect`/`WriteVariableIndirect` so that
   variable 0 peeks/replaces the stack top instead of pushing/popping:
   - `@load`/`@store`: read/write a variable by number
   - `@inc`/`@dec`: signed increment/decrement
   - `@inc_chk`/`@dec_chk`: increment/decrement + signed branch check
   - `@push`/`@pull`: stack push (direct) and pop-to-variable (indirect)

   **Memory opcodes** — array access using base+index addressing:
   - `@loadw`/`@loadb`: read word/byte at array + 2*index / array + index
   - `@storew`/`@storeb`: write word/byte at array + 2*index / array + index

   **Table opcodes**:
   - `@scan_table`: searches for a value in a table. Form byte bit 7
     selects word vs byte comparison; bottom 7 bits = entry length.
     Returns (address, found) tuple.
   - `@copy_table`: copies or zeroes memory. second=0 zeroes the source.
     Positive size copies backward when second > first to avoid overlap
     corruption. Negative size forces forward copy (allows fill patterns).

#### Design Decisions

**Static class**: Unlike `ArithmeticOps` (which is instantiated for
`@random`'s RNG state), `VariableMemoryOps` is fully static — none of
its methods need persistent state. They take `MachineState` or `Memory`
as parameters.

**@scan_table returns a tuple**: Rather than using out parameters or a
custom type, `ScanTable` returns `(ushort Address, bool Found)`. The
caller uses `Found` for the branch condition and `Address` for the store
result. This keeps the API clean and matches the opcode's dual output.

**@copy_table overlap handling**: The spec says positive size should copy
safely (no corruption on overlap). The implementation copies backward
when `second > first` (destination is ahead of source), which prevents
overwriting source bytes before they're read. Negative size forces
forward copy — this deliberately allows the overlap, enabling patterns
like filling a region by copying a single byte forward repeatedly.

### Test Coverage (38 tests)

- @load/@store: local variable, stack peek/replace (4)
- @inc/@dec: local, signed overflow/underflow, stack in-place (6)
- @inc_chk/@dec_chk: increment+branch, no-branch, signed comparison (6)
- @push/@pull: stack push order, pull to local, pull to stack (3)
- @loadw/@loadb: base address, with index (4)
- @storew/@storeb: base address, with index (4)
- @scan_table: word found/not-found, byte found/not-found, larger entries,
  first entry match (6)
- @copy_table: basic copy, zero table, overlap forward (positive size),
  negative size force forward, no overlap (5)

---

### Task 6.3 — Object Manipulation Opcodes

**Date**: 2026-09-07

#### Steps Taken

1. **Implemented `ObjectOps`** in Core. Thin opcode-level wrappers around
   the existing `ObjectTable` methods, adapting them to opcode calling
   conventions:

   **Tree opcodes**: `GetParent` (store), `GetChild` (store + branch if
   non-zero), `GetSibling` (store + branch if non-zero), `JumpIn`
   (branch if obj1's parent == obj2), `InsertObj`, `RemoveObj`.

   **Property opcodes**: `GetProp` (returns value or default), `GetPropAddr`
   (returns data address or 0), `GetPropLen` (0→0), `GetNextProp` (0 = first),
   `PutProp` (sets 1 or 2 byte property).

   **Attribute opcodes**: `TestAttr` (branch), `SetAttr`, `ClearAttr`.

   **Output**: `PrintObj` — decodes the short name Z-string via TextDecoder
   and returns it as a string for the caller to send to the output stream.

#### Design Decisions

**Thin wrappers, not duplicated logic**: `ObjectOps` delegates entirely to
`ObjectTable` for the actual tree/property/attribute operations. The value
it adds is adapting return types to opcode conventions — returning tuples
for store+branch opcodes (`GetChild` returns `(child, hasChild)`), booleans
for branch-only opcodes, and strings for `@print_obj`.

**`PrintObj` returns a string**: Rather than taking an `IScreen` or
`OutputStreamManager`, `PrintObj` returns the decoded name as a string.
The interpreter's opcode dispatcher is responsible for sending it to the
active output stream. This keeps `ObjectOps` free of IO dependencies.

#### Pitfall: object number assumptions

Initially assumed zork1 object 80 was "leaflet" — it's actually "South of
House". Also, the first property on object 180 is a 1-byte property, so
testing `@put_prop` with a 2-byte value (0x1234) only stored the low byte.
Fixed by walking properties to find a 2-byte one first.

### Test Coverage (23 tests)

- Tree: get parent, get child (with branch), get child no-children case,
  get sibling, jin true/false, insert obj moves object, remove obj
  detaches (8)
- Properties: get existing prop, get missing returns default, get prop addr
  existing/missing, get prop len 0→0, get prop len range, get next prop
  from 0, next prop descending order, put prop round-trip (9)
- Attributes: test attr, set+test, clear+test (3)
- @print_obj: "West of House", "small mailbox", "South of House" (3)

---

## Task 6.4 — Text Output Opcodes

**Date**: 2026-09-07
**Branch**: `feature/6.4-text-output-opcodes`
**Files**: `src/ZMachine.Core/TextOutputOps.cs`, `tests/ZMachine.Tests/TextOutputOpsTests.cs`

### Overview

Implemented all Z-Machine text output opcodes (ZSpec S15): `@print`, `@print_ret`,
`@print_addr`, `@print_paddr`, `@print_char`, `@print_num`, `@new_line`,
`@print_table` (V5+), `@print_unicode` (EXT, V5+), and `@encode_text` (V5+).
All output is routed through `OutputStreamManager` to respect stream selection.

### Design Decisions

**TextOutputOps as a non-static class**: Unlike `VariableMemoryOps` (static), this
class holds references to `Memory`, `TextDecoder`, `TextEncoder`, and
`OutputStreamManager`. These dependencies make a static design awkward — the
instance approach keeps the API clean.

**Print returns byte length, not void**: `@print` and `@print_ret` decode an inline
Z-string that immediately follows the opcode. The caller (future instruction
dispatcher) needs the byte length to advance PC past the string. Returning `int`
from `Print`/`PrintRet` provides this directly.

**PrintPAddr takes an already-unpacked address**: The packed-to-byte address
conversion depends on version and (for V6/V7) a strings offset. That logic lives
in `AddressHelper.UnpackStringAddress`. Rather than duplicating it, `PrintPAddr`
accepts the unpacked address. The instruction dispatcher will call
`AddressHelper` before `PrintPAddr`.

**PrintTable newline placement**: Newlines are emitted between rows, not after
the last row. This matches ZSpec S15 which describes printing a "rectangle" —
the final row has no trailing newline.

**PrintUnicode control code rejection**: Silently drops control codes (0-31,
127-159) rather than throwing. This is the defensive behavior recommended by
ZSpec11 for characters that cannot be printed.

**EncodeText caps at 6 bytes**: V5+ dictionary entries are 6 bytes (3 words,
9 Z-chars). The method writes at most 6 bytes to the destination regardless of
what `TextEncoder.EncodeForDictionary` returns, matching the spec.

### Lessons Learned

Synthetic test memory requires valid header fields. `Memory.LoadStory` validates
the static memory base (header $0E) — a zeroed header fails with "Invalid static
memory base $0000". The fix was setting $0E to $8000 to place the static/dynamic
boundary high enough that test writes to addresses $1000-$3100 land in dynamic
memory.

### Test Coverage (29 tests)

- @print: Z-string decoding + byte length return, byte length is word-aligned (2)
- @print_ret: prints string + newline, returns byte length (1)
- @print_addr / @print_paddr: byte address and unpacked address (2)
- @print_char: ASCII char, space (2)
- @print_num: positive, zero, negative (-42), min signed (-32768), max positive (5)
- @new_line: prints newline (1)
- @print_table: single row, multiple rows 2x3, skip bytes, height=1 no newline (4)
- @print_unicode: BMP char (é), Greek (α), reject low control, reject DEL,
  reject C1 range, accept space, accept 160 (7)
- @encode_text: writes nonzero bytes, from-offset equivalence, round-trip
  matches TextEncoder (3)
- Combined: char+newline, num between chars (2)

---

## Task 6.5 — Control Flow Opcodes

**Date**: 2026-09-07
**Branch**: `feature/6.5-control-flow-opcodes`
**Files**: `src/ZMachine.Core/ControlFlowOps.cs`, `tests/ZMachine.Tests/ControlFlowOpsTests.cs`

### Overview

Implemented all Z-Machine control flow opcodes (ZSpec S5, S15): routine calls
(`@call_1s`/`@call_2s`/`@call_vs`/`@call_vs2` and `_vn` discard variants),
returns (`@ret`, `@rtrue`, `@rfalse`, `@ret_popped`), flow control (`@jump`,
`@nop`, `@piracy`), exception-like `@catch`/`@throw`, `@check_arg_count`,
`@verify`, `@restart`, and `@quit`.

### Design Decisions

**Single Call method for all call variants**: Rather than separate methods for
`@call_1s`, `@call_2s`, etc., a single `Call(packedAddress, args, argCount,
storeVariable, discardResult, returnPC)` handles all variants. The caller
(instruction dispatcher) selects which operands to pass. This eliminates
code duplication since the variants differ only in operand count and
store/discard semantics.

**Packed address 0 returns false, not void**: ZSpec says calling address 0
does nothing and stores 0. The `Call` method returns `bool` — `false` for
address 0 — so the dispatcher knows to store 0 without entering a routine.

**V1–4 vs V5+ local initialisation**: V1–4 routines have initial values as
words after the local count byte, which the code reads and writes to the
frame's `Locals[]`. V5+ skips this entirely (locals start at 0, which is
the default for `ushort[]`). PC is set past the header in both cases.

**Catch/Throw use frame count**: Following Quetzal S6.1–S6.2, `@catch`
returns `CallStack.FrameCount` and `@throw` unwinds by popping frames
until the count matches, then performs a normal `Return`. This matches
Quetzal's serialisation model.

**Restart clears stack then reads initial PC from (restored) header**: The
dynamic memory restore happens first, then the call stack is cleared, then
the initial PC is read from the freshly-restored header word $06. This
ensures the initial PC reflects the original story file, not any runtime
modifications.

**Quit is a no-op method**: The actual halt is the execution loop's
responsibility. `Quit()` exists as a named entry point so the dispatcher
can pattern-match on it.

### Lessons Learned

Jump offset arithmetic: `target = addressAfterInstruction + offset - 2`.
The "- 2" is because ZSpec defines the offset relative to the branch data
itself (which is 2 bytes before the address-after-instruction in the common
case). Initially got a test assertion wrong by computing the expected value
incorrectly.

### Test Coverage (23 tests)

- Call/Return: address 0 returns false, V3 frame with initial values, V5
  locals init to 0, excess args ignored, return pops frame + stores result,
  discard result, rtrue, rfalse, ret_popped, nested call unwind (10)
- Jump: positive offset, negative offset (backwards) (2)
- Catch/Throw: catch returns frame count, throw unwinds and returns (2)
- check_arg_count: within range true, beyond range false (2)
- Piracy: always true (1)
- Nop: does nothing (1)
- Verify: zork1 checksum matches, corrupted file fails (2)
- Restart: restores dynamic memory, clears call stack, sets PC to initial (3)

---

## Task 6.6 — V5+ Screen and Style Opcodes

**Date**: 2026-09-07
**Branch**: `feature/6.6-screen-style-opcodes`
**Files**: `src/ZMachine.Core/ScreenStyleOps.cs`, `tests/ZMachine.Tests/ScreenStyleOpsTests.cs`

### Overview

Implemented V5+ screen and style opcodes (ZSpec S8, ZSpec11): `@set_text_style`
with cumulative combination, `@set_font` with previous-font return, `@set_colour`
with all standard and Standard 1.1 grey colours, `@get_cursor`, `@erase_line`,
`@buffer_mode`, `@check_unicode`, `@save_undo`/`@restore_undo` (single-level),
and the fixed-pitch header bit check.

### Design Decisions

**Callback-based screen delegation**: `ScreenStyleOps` lives in Core (no IO
dependency) and delegates rendering via `Action`/`Func` callbacks (`OnSetTextStyle`,
`OnSetFont`, `OnSetColour`, `OnGetCursor`, `OnEraseLine`, `OnBufferMode`). The
execution loop wires these to the `IScreen` backend at startup. This avoids
Core→IO coupling while keeping style state tracked centrally.

**Style 0 clears, non-zero combines**: Per ZSpec11, `@set_text_style 0` clears
all styles back to Roman. Non-zero values are OR'd into the accumulator. This
means `set_text_style 2; set_text_style 4` gives Bold+Italic (6), not just
Italic (4).

**SetFont returns 0 for unavailable fonts**: Font 2 (undefined) and fonts 5+
(reserved) immediately return 0 without changing state. Font 0 returns the
current font without changing it (ZSpec query convention).

**SetColour 0 = no change**: Colour 0 means "current", not black. This lets
games set only foreground or only background in a single call.

**Single-level undo via dynamic memory snapshot**: `SaveUndo` copies dynamic
memory (0..StaticBase) into a byte array. `RestoreUndo` writes it back and
clears the snapshot (one-shot). Returns 1 for save, 2 for restore, 0 for
failure — the different return values let the game distinguish "just saved"
from "just restored".

**Fixed-pitch header bit**: Rather than intercepting `@storeb`/`@storew` writes
to the header, `IsFixedPitchRequested()` reads the live bit on demand. The
execution loop can check this after any memory write to the header region.

### Test Coverage (32 tests)

- @set_text_style: Roman clears, combines via addition, duplicate idempotent,
  callback invoked (4)
- @set_font: normal→previous, fixed-pitch switch, char graphics, font 2
  returns 0, font 5+ returns 0, font 0 queries (6)
- @set_colour: sets fg/bg, colour 0 means current, default is 1, all standard
  (2–9), Standard 1.1 greys (10–12) (5)
- @get_cursor: writes position to memory, defaults (1,1) (2)
- @erase_line: value 1 invokes, other values ignored (2)
- @buffer_mode: enable/disable (2)
- @check_unicode: printable ASCII both bits, space, BMP non-ASCII print only,
  control codes neither (4)
- @save_undo/@restore_undo: save returns 1, restore without save returns 0,
  save+modify+restore round-trip, restore only works once (4)
- Fixed-pitch: default false, set bit → true, clear bit → false (3)

---

## Phase 7: Integration

### Task 7.1 — Main Execution Loop and Opcode Dispatch

**Date**: 2026-09-07

#### Steps Taken

1. **Moved IScreen and IInputStream interfaces to Core.** These pure abstractions
   had no dependencies — keeping them in IO forced the execution engine into a
   higher layer. Moving them to Core follows the standard pattern where the
   innermost layer owns the contracts and outer layers provide implementations.
   Updated `using` directives in all IO implementors (ConsoleScreen,
   ConsoleInputStream, FileInputStream, InputStreamManager, WindowManager).

2. **Created `Interpreter` class** (`src/ZMachine.Core/ZMachine.cs`). Named
   `Interpreter` instead of `ZMachine` to avoid collision with the root namespace
   `ZMachine.Core` — C# resolves the type name before the namespace, causing
   `using ZMachine.IO` to fail with "type name does not exist in type."

3. **Implemented Load/Init/Run/Step cycle.** `Load()` accepts a story path (or
   byte array for testing), an `IInputStream`, and an `IScreen`. `Init()` wires
   all subsystems: Memory, MachineState, TextDecoder/Encoder, ObjectTable,
   Dictionary, Tokenizer, ReadHandler, OutputStreamManager, and all six opcode
   handler classes. Sets the initial PC from header $06 — byte address for V1-5,
   packed routine address (via `Call()`) for V6.

4. **Built dispatch tables for all five opcode forms** (Op2, Op1, Op0, VAR, EXT).
   Each form has its own dispatch method with a switch on opcode number. Store
   and branch bytes are decoded per-opcode before operand resolution, matching
   the Z-Machine spec's instruction layout.

5. **Wired all Phase 3-6 opcodes:**
   - ArithmeticOps: je/jl/jg/jz/test, add/sub/mul/div/mod, and/or/not,
     log_shift/art_shift, random
   - VariableMemoryOps: load/store/inc/dec/inc_chk/dec_chk, push/pull,
     loadw/loadb/storew/storeb, scan_table/copy_table
   - ObjectOps: get_parent/child/sibling, jin, insert_obj/remove_obj,
     get/put/next_prop, get_prop_addr/len, test/set/clear_attr, print_obj
   - TextOutputOps: print/print_ret (inline text), print_addr/paddr/char/num,
     new_line, print_table/unicode, encode_text
   - ControlFlowOps: call variants (call_vs/vn/2s/2n/1s/1n/vs2/vn2),
     ret/rtrue/rfalse/ret_popped, jump, catch/throw, verify, piracy, restart,
     quit, nop, check_arg_count
   - ScreenStyleOps: set_text_style, set_font, set_colour, get_cursor,
     erase_line, buffer_mode, check_unicode, save_undo/restore_undo

6. **Handled version-dependent opcode semantics:**
   - 1OP:15 is @not in V1-4 (store) vs @call_1n in V5+ (no store)
   - 0OP:9 is @pop in V1-4 vs @catch in V5+ (store)
   - 0OP:5/6 are @save/@restore with branch (V1-3) or store (V4) or EXT (V5+)
   - VAR:4 @read stores terminating char in V5+ only

7. **Implemented helper methods:**
   - `InvokeCall()`: unpacks routine address, passes args, handles address 0
     (store 0 + advance), delegates to ControlFlowOps.Call()
   - `ExecuteRead()`: shows status line (V1-3), reads input, processes text
     and parse buffers via ReadHandler
   - `ExecuteTokenise()`: V5+ @tokenise with optional custom dictionary

8. **Created integration tests** against zork1.z3:
   - Boot and first moves: verifies opening text mentions ZORK and West of House
   - Quit stops the machine
   - Five moves: look, open mailbox, read leaflet, go north, inventory

#### Design Decisions

- **Interface placement**: Interfaces in Core, implementations in IO. This is
  the Dependency Inversion Principle applied to the project structure — Core
  defines what it needs, IO provides it.

- **Class naming**: `Interpreter` instead of `ZMachine`. The namespace collision
  is a permanent problem (any file with `using ZMachine.Core` would shadow the
  namespace), and renaming avoids global:: workarounds everywhere.

- **Inline store/branch decoding**: Rather than pre-computing which opcodes need
  store/branch (which would require a lookup table or attributes), each dispatch
  method decodes store/branch in the first switch before resolving operands. This
  keeps the knowledge local to each opcode and matches the spec's instruction
  layout where store/branch follow operands.

- **No abstract dispatch table**: Could have used a `Dictionary<(OpcodeForm, int),
  Action<Instruction>>` but the switch-based approach is faster (no allocation,
  no delegate dispatch), easier to debug (stack traces show the exact case), and
  makes version-dependent behavior natural with inline `if (_version >= 5)`.

#### Spec Interpretation Notes

- **@print/@print_ret inline text**: The text starts at `inst.NextAddress` (after
  the opcode byte). The text decoder returns the byte length consumed. PC advances
  past the text, then for @print_ret, @rtrue is executed.

- **Indirect variable references**: dec_chk, inc_chk, store, inc, dec, load, pull
  use the raw operand value as a variable number (not the resolved value). This
  is why they read `(byte)inst.Operands[0]` instead of `ops[0]`.

### Test Coverage (3 integration tests)

- Zork I boot and first moves: opening text, look response (1)
- Quit stops machine (1)
- Five-move playthrough without crash (1)

---

### Task 7.2 — Regression Test Harness

**Date**: 2026-09-07

#### Steps Taken

1. **Created reusable test infrastructure** in `tests/ZMachine.Tests/Harness/`:
   - `ScriptedInputStream` — feeds commands from a list or file. Supports
     comment lines (`#`) and blank-line skipping for script files. Tracks
     lines consumed. Auto-returns "quit" when exhausted to prevent hangs.
   - `CaptureScreen` — captures all printed text and status lines separately.
     Implements full IScreen with no-op stubs for window/cursor operations.
   - `TestHarness` — orchestrator with static `Run()`, `RunScript()`, and
     `RunBytes()` factory methods. Enforces a configurable instruction limit
     (default 10M) to prevent infinite loops in tests. Exposes captured
     output, input stats, and interpreter state after the run.

2. **Created test scripts** in `tests/scripts/`:
   - `zork1_mailbox.txt` — open mailbox, read leaflet, quit
   - `zork1_exploration.txt` — five-move sequence through the opening area
   - `czech_conformance.txt` — starts the Czech conformance test suite

3. **Wrote 16 regression tests** covering:
   - Zork I (V3): boot text, open mailbox, read leaflet, inventory, go north,
     script-file-driven mailbox and exploration sequences, instruction count
     sanity check
   - Minizork (V3): boot verification
   - Czech (V5): boot and header text verification
   - Harness infrastructure: instruction limit abort, ScriptedInputStream
     file parsing, command exhaustion, line counting, CaptureScreen output
     and status line capture

4. **Refactored ZMachineIntegrationTests** to use the public harness classes
   instead of private inner classes, eliminating code duplication.

#### Design Decisions

- **Static factory methods on TestHarness**: `Run()`, `RunScript()`, `RunBytes()`
  rather than a constructor+method chain. Each call is a complete test run —
  no mutable setup state to misuse. The harness itself is write-once-read-many
  after the run completes.

- **Instruction limit as safety net**: 10M default is generous enough for any
  reasonable game interaction but catches infinite loops in under a second.
  Tests can override for specific scenarios (e.g., the 100-instruction abort
  test).

- **Script file format**: One command per line, `#` for comments, blank lines
  skipped. Simple enough to edit by hand, no parser complexity.

- **Separate status line capture**: Status lines go to their own list rather
  than mixing into the main output. This lets tests assert on status content
  without parsing it out of the middle of game text.

### Test Coverage (16 regression tests)

- Zork I boot: opening text, ZORK mention (1)
- Zork I commands: open mailbox, read leaflet, inventory, go north (4)
- Zork I script files: mailbox sequence, exploration sequence (2)
- Zork I metrics: instruction count sanity (1)
- Minizork: boot verification (1)
- Czech V5: boot and header text (1)
- Harness infrastructure: instruction limit, ScriptedInputStream (file
  parsing, exhaustion, line count), CaptureScreen (output, status lines) (6)

---

## Phase 8: Developer Tools

### Task 8.1 — Story File Inspector

**Date:** 2026-09-07

**Objective:** Create a read-only inspector that extracts and formats story file
metadata — header fields, memory map, header extension table, and interpreter
capabilities — for debugging and developer tooling.

**Design Decisions:**

1. **Single class, four views:** `StoryInspector` provides `GetHeaderFields()`,
   `GetHeaderExtension()`, `GetMemoryMap()`, and `GetInterpreterInfo()` — each
   returning strongly typed records. This maps directly to the four planned
   GUI panels without coupling to any presentation layer.

2. **Records for output:** Used `HeaderField(Name, Address, Size, RawHex,
   DecodedValue)` and `MemoryRegion(Name, Start, End)` records. Records give
   value equality, immutability, and concise declarations — ideal for
   read-only inspection data.

3. **Version-gated field inclusion:** Rather than returning empty/null values
   for fields that don't apply to a given version, the inspector omits them
   entirely. A V3 story has no interpreter number/version or colour fields in
   its output. This simplifies consumers — no need to check applicability.

4. **Structure size estimation:** The memory map needs end addresses for
   variable-length structures (abbreviation table, object table, dictionary).
   Rather than requiring the full decoder infrastructure, used lightweight
   estimators:
   - Abbreviation table: 32 entries (V1-2) or 96 entries (V3+) × 2 bytes
   - Object table: first object's property pointer reveals object count
   - Dictionary: header encodes separator count, entry length, entry count

5. **Checksum verification in two places:** Both `GetHeaderFields()` (raw
   "verified"/"MISMATCH") and `GetInterpreterInfo()` ("PASS"/"FAIL") report
   checksum status. Different context — one shows the raw field, the other
   summarizes interpreter state.

6. **True colour decoding (ZSpec11):** RGB 5-5-5 format with sentinels
   $FFFE (default) and $FFFF (current). Extracted as R/G/B components.

**Spec References:**
- ZSpec S11 — Header layout ($00–$3F)
- ZSpec S11.1 — Flags 1 (V1-3 vs V4+ interpretation)
- ZSpec S11.1.2 — Flags 2 (game-requested features)
- ZSpec11 "Header Extension" — Extension table and words 1–6
- ZSpec11 "True Colours" — RGB 5-5-5 encoding with sentinels

**Multi-Version Testing:**
Story files now span V3–V6 for comprehensive coverage:
- V3: zork1.z3, ballyhoo.z3, minizork.z3
- V4: mind.z4
- V5: sherlock.z5, czech.z5
- V6: Journey/STORY.DATA.z6

**Test Coverage (26 tests):**
- Zork I V3: version, flags, checksum, serial, table addresses, no
  interpreter fields, file length (7)
- Czech V5: version, colours, alphabet field, header extension (4)
- Mind V4: interpreter number/version, screen size (1)
- Sherlock V5: standard revision (1)
- Journey V6: routine/string offsets, packed initial PC (2)
- Memory map: all regions, header 64 bytes, sort order, dynamic/static
  boundary, globals 480 bytes (5)
- Header extension: V3 empty, V5 conditional (2)
- Interpreter info: essential fields, checksum PASS (2)
- Synthetic memory: V5 full decode, Flags 3 transparency, checksum
  mismatch detection (3)

---

### Task 8.2 — Object Tree Viewer

**Date:** 2026-09-07

**Objective:** Create a read-only viewer for the Z-Machine object hierarchy
with tree traversal, detail extraction, search/filter, and plain-text export.

**Design Decisions:**

1. **Self-contained construction:** `ObjectTreeViewer` takes only a `Memory`
   instance and internally creates its own `ObjectTable` and `TextDecoder`.
   This avoids coupling the viewer to the `Interpreter` class and allows
   standalone use from developer tools or test harnesses.

2. **Object count derivation:** The Z-Machine spec doesn't store an explicit
   object count. Used the standard technique: the first object's property
   table pointer immediately follows the last object entry, so
   `count = (propTable1 - entriesStart) / entrySize`. This worked correctly
   for all test files (V3–V5).

3. **Tree built from pointers:** `GetTree()` finds all root objects
   (parent == 0) and recursively follows child/sibling pointers to build
   `ObjectNode` trees. Each node carries its number, decoded short name,
   and child list.

4. **Detail extraction in one call:** `GetObjectDetail()` returns everything
   about one object: tree pointers (with decoded parent/sibling/child names),
   all set attributes, and all properties with their number, address, data
   length, hex bytes, and word-interpreted values for 1–2 byte properties.

5. **Property iteration via GetNextProperty:** Used the existing
   `ObjectTable.GetNextProperty(obj, 0)` chain rather than manually scanning
   property blocks, which keeps the viewer honest to the same parsing logic
   the interpreter uses.

6. **Search dual-mode:** Search accepts both name substrings
   (case-insensitive) and exact object numbers. Numeric queries match the
   object number directly, then also match any names containing that string.

7. **Export format:** Plain text with two sections — indented tree view,
   then full detail blocks. Intentionally simple for offline analysis rather
   than a structured format, matching the task requirement.

**Spec References:**
- ZSpec S12 — Object table layout and tree structure
- ZSpec S12.3 — Object entries: attributes, tree pointers, property pointer
- ZSpec S12.4 — Property blocks, size bytes, descending order

**Test Coverage (25 tests):**
- Zork I tree: object count, West of House exists/has parent/is child of
  parent, mailbox has properties and attributes (6)
- Zork I tree building: has roots, roots have children, all objects in tree (3)
- Zork I search: by number, case-insensitive, not-found (3)
- Zork I detail: hex/interpreted values, descending property order (2)
- Zork I export: contains known objects, includes detail sections (2)
- Multi-version: Mind V4 named objects, Sherlock V5 tree, Czech V5
  attribute range, Czech V5 object count (4)
- Synthetic V3: parent/child, tree structure, search, export, attributes (5)

---

### Task 8.3 — Dictionary Viewer

**Date:** 2026-09-07

**Objective:** Create a read-only viewer for the Z-Machine dictionary with
metadata extraction, entry decoding, search, sorting, and word lookup.

**Design Decisions:**

1. **Self-contained construction:** Like `ObjectTreeViewer`, the
   `DictionaryViewer` takes only a `Memory` instance and creates its own
   `Dictionary`, `TextDecoder`, and `TextEncoder` internally. No coupling
   to the `Interpreter` class.

2. **Reuse of existing Dictionary class:** Rather than duplicating the
   parsing logic, the viewer wraps the existing `Dictionary` class and adds
   the entry-reading, search, and sorting layer on top. This keeps the
   parsing consistent with the interpreter.

3. **Entry decoding via TextDecoder:** Dictionary entries are standard
   Z-strings with the top bit set on the last word, so `DecodeZString` works
   directly on them. No special decoding path needed.

4. **Game-specific data exposed as raw hex:** The bytes following the encoded
   text in each entry are game-specific (typically part-of-speech flags).
   Since interpretation varies by game, they're exposed as hex rather than
   decoded.

5. **Three sort fields:** Entries can be sorted by address, decoded text, or
   entry number, ascending or descending. The default order (by entry number)
   matches the on-disk order.

6. **Dual search modes:** `Search()` filters by substring on decoded text
   (for browsing), while `LookupWord()` uses the dictionary's binary/linear
   search on encoded form (for exact matching).

**Spec References:**
- ZSpec S13 — Dictionary layout, separators, entry structure
- ZSpec S3 — Z-character encoding used for dictionary entries

**Test Coverage (24 tests):**
- Metadata: reasonable values, standard separators, V5 encoded length,
  entries start after header (4)
- Known words: mailbox, open, take lookups, not-found (4)
- Entry content: all have text, encoded hex, count matches, data bytes,
  addresses increasing (5)
- Sorting: alphabetical, by address, descending reversal (3)
- Search: case-insensitive, not-found, partial match (3)
- Multi-version: Mind V4, Sherlock V5 (2)
- Synthetic V3: two entries, separators, search all (3)

---

### Task 8.4 — Disassembler

**Date:** 2026-09-07

**Objective:** Create a disassembler that decodes Z-Machine instructions with
mnemonics, operand types, store/branch targets, inline text, routine detection,
cross-references, and plain-text export.

**Design Decisions:**

1. **Reuse of InstructionDecoder:** The disassembler uses the existing
   `InstructionDecoder.Decode()` for instruction parsing, then applies its own
   store/branch tables for the decode-only path (the interpreter's dispatch
   interleaves store/branch decoding with execution). This keeps parsing
   consistent between the interpreter and disassembler.

2. **Store/branch tables mirror the interpreter:** The `DecodeStoreAndBranch`
   method replicates the exact same per-opcode store/branch decisions as the
   interpreter's dispatch methods, including all version-dependent cases
   (0OP:5/6 save/restore V1-3 branch vs V4 store, 1OP:15 not/call_1n,
   VAR:4 read store in V5+, 0OP:9 catch/pop).

3. **Mnemonic tables with version awareness:** V3 `not` vs V5 `call_1n`,
   V3 `read` vs V5 `aread`, V3 `save`/`restore` vs V5 EXT forms. The
   mnemonic tables are public so tests can verify them directly without
   constructing full instructions.

4. **Routine detection heuristic:** Scans from high memory looking for
   valid local-count bytes (0–15) preceded by a zero padding byte. Also
   follows call targets from disassembled code to discover routines not
   reachable from the heuristic scan alone. Not perfect — static analysis
   of computed calls is impossible — but sufficient for developer tooling.

5. **Cross-reference building:** For each routine, disassembles its code
   and collects call targets, then inverts the map: for each routine address,
   lists call sites. Operates on a provided routine list rather than
   scanning the whole file.

6. **Inline text extraction:** `print` (0OP:2) and `print_ret` (0OP:3)
   have inline Z-strings after the opcode byte. The disassembler decodes
   these via `TextDecoder.DecodeZString` and advances past them.

7. **Terminator detection:** A routine ends at rtrue, rfalse, ret,
   ret_popped, quit, print_ret, restart, or jump. This is conservative
   (could miss fallthrough code) but safe for the common case.

**Spec References:**
- ZSpec S4 — Instruction encoding forms (long, short, variable, extended)
- ZSpec S4.5 — Store byte
- ZSpec S4.7 — Branch offset encoding
- ZSpec S14/S15 — Opcode tables by form and version

**Test Coverage (27 tests):**
- Zork I main routine: disassembles, starts with call, valid local count,
  valid mnemonics (4)
- Single instruction: address/bytes, contiguous range (2)
- Operand formatting: variable refs, constants (2)
- Store and branch: arrow prefix, question mark prefix (2)
- Inline text: print instructions decoded (1)
- Routine detection: finds main, valid local counts (2)
- Cross-references: build from subset (1)
- Export: header text, hex addresses (2)
- Mnemonics: 2OP known, 1OP version-dependent, 0OP known, VAR known,
  EXT known (5)
- Multi-version: Mind V4, Sherlock V5, Czech V5 mnemonics (3)
- Synthetic: rtrue, add with store, je with branch (3)

---

### Task 8.5 — Interactive Debugger

**Date:** 2026-09-08

**Objective:** Create a debugger engine that wraps the `Interpreter` class with
step-through execution, breakpoints, state inspection, execution trace, and
watch expressions — the programmatic layer that a future GUI debugger will drive.

**Design Decisions:**

1. **Wrapper architecture:** `Debugger` takes a loaded `Interpreter` and drives
   its `Step()` method with debugger logic layered on top. This avoids modifying
   the interpreter's execution path — the debugger is an external controller,
   not an intrusive hook. The interpreter doesn't know it's being debugged.

2. **Three breakpoint types:** Address (PC == target), Conditional (global
   variable == value), and Opcode (mnemonic match). Each is represented by a
   single `Breakpoint` class with type-discriminated constructors. Breakpoints
   have an `Enabled` flag for disable-without-remove. Conditional breakpoints
   use `ReadVariable(globalVar + 16)` to read the global without stack side
   effects.

3. **StepOver via call-depth tracking:** Before executing the first instruction,
   records `CallStack.FrameCount`. If the instruction entered a call (depth
   increased), continues stepping until depth returns to the original level,
   checking breakpoints each step. This handles nested calls naturally — the
   depth comparison catches the return from any depth of nesting.

4. **Continue with breakpoint checking:** Checks breakpoints *before* each
   instruction execution, so the breakpoint fires at the instruction about to
   execute rather than after it. This matches the expected debugger UX where
   the stopped PC is the breakpoint address.

5. **State snapshots as records:** `FrameSnapshot`, `LocalsSnapshot`, and
   `MemoryDump` are immutable records returned by inspection methods. The caller
   gets a frozen snapshot — no references to live interpreter state that could
   change on the next step.

6. **Locals 1-indexed in frame:** Frame locals are stored 1-indexed (slot 0
   unused) per the Z-Machine spec. The snapshot copies `Locals[1..LocalCount]`
   into a 0-indexed array for display convenience.

7. **Rolling trace log:** Default 1000 entries, configurable via
   `MaxTraceEntries`. Oldest entries are dropped when the limit is reached.
   Each entry records address, mnemonic, and sequence number. Disabling trace
   stops recording but preserves existing entries.

8. **Watch expression parsing:** Supports four formats:
   - `G00`–`GEF`: Global variables 0–239 (read via `ReadVariable(idx + 16)`)
   - `L00`–`L0E`: Local variables 0–14 (read via `ReadVariable(idx + 1)`)
   - `SP`: Stack top (peek, not pop — uses `EvalStack.Peek()`)
   - `[$1234]`: Memory byte at hex address (via `Memory.ReadByte`)
   Invalid expressions set the `Error` field instead of throwing.

9. **Memory dump with ASCII sidebar:** Formatted as 16-byte rows with hex
   address, hex bytes (grouped 8+8), and printable ASCII sidebar. Non-printable
   bytes shown as `.`. Includes the static memory base address for reference.

**Spec References:**
- ZSpec S4–S6 — Instruction encoding, routines, variables (context for state inspection)
- ZSpec S6.3 — Per-routine evaluation stack
- ZSpec S6.4 — Variable numbering: 0=SP, 1-15=locals, 16-255=globals

**Test Coverage (32 tests):**
- Step Into: 10 instructions PC advances, each step has valid instruction,
  returns Halted when stopped (3)
- Step Over: call returns to same depth (1)
- Continue: stops at breakpoint, hits instruction limit (2)
- Address breakpoints: add/remove, disabled doesn't trigger, clear all (3)
- Conditional breakpoints: correct type and fields (1)
- Opcode breakpoints: triggers on mnemonic (1)
- State inspection: call stack non-empty, locals inspectable, 240 globals,
  eval stack accessible, call stack grows on call (5)
- Memory dump: formatted hex output, static base, header version byte (3)
- Trace: records steps, exports text, rolling log truncates, disabled no
  recording, clear empties (5)
- Watch expressions: global evaluates, local evaluates, memory evaluates,
  invalid sets error, updates on step, remove/clear, SP evaluates (7)
- Multi-version: Czech V5 step into (1)

---

## Phase 9: Save/Restore (Quetzal)

### Task 9.1 — IFF Container Format Reader/Writer

**Date:** 2026-09-08

**Objective:** Implement a general-purpose IFF (Interchange File Format)
reader/writer that handles both Quetzal (IFZS) and Blorb (IFRS) containers,
including nested FORM chunks, padding, duplicate chunk warnings, and
unknown chunk preservation.

**Design Decisions:**

1. **Static reader/writer classes:** `IffReader` and `IffWriter` are static
   utility classes — no instance state needed. Both accept either a `Stream`
   or `byte[]` for convenience. The writer also has a `WriteToArray` shortcut.

2. **IffForm as parsed result:** The reader returns an `IffForm` with the
   form type, a flat list of chunks in file order, and any warnings. Helper
   methods `GetChunk(type)` and `GetChunks(type)` make lookup easy. The
   first-match semantics of `GetChunk` follow Quetzal S8.8.

3. **IffChunk with optional inner form type:** Regular chunks have a `TypeId`
   and `Data`. Nested FORM chunks (like AIFF inside Blorb) additionally have
   an `InnerFormType`. The `Length` property accounts for the 4-byte inner
   type when present. The `Data` for nested FORMs contains only the sub-chunk
   data, not the inner type bytes — the reader strips it, the writer adds it.

4. **Odd-length padding (Quetzal S8.4.1):** The reader skips the pad byte
   after odd-length chunks. The writer emits a zero pad byte. Neither includes
   the pad byte in the chunk length. The IFhd chunk's 13-byte length (Quetzal
   S5.7) is the canonical test case for this.

5. **Duplicate chunk handling (Quetzal S8.8):** When a chunk type appears
   more than once and only one is expected, the reader keeps the first and
   ignores later duplicates with a warning. ANNO chunks are the exception —
   multiple are allowed per Quetzal S7.5.

6. **Unknown chunk preservation (Quetzal S8.9):** The reader does not reject
   unknown chunk types — they're stored in the chunk list like any other.
   Consumers decide what to do with them.

7. **Big-endian I/O:** Both reader and writer use explicit big-endian
   encoding for the 4-byte length fields, avoiding any endianness assumptions
   about the runtime platform.

8. **Nested FORM detection:** When the reader encounters a chunk with type
   ID "FORM", it reads the next 4 bytes as the inner form type and the
   remaining bytes as data. This handles AIFF sounds in Blorb (chunk type
   'FORM' with formtype 'AIFF') without special-casing the format.

**Spec References:**
- Quetzal S8 — IFF format basics (chunks, FORM, padding, duplicates)
- Quetzal S8.4.1 — Odd-length padding byte
- Quetzal S8.5 — FORM structure
- Quetzal S8.8 — Duplicate chunk handling
- Quetzal S8.9 — Unknown chunk skipping
- Quetzal S5.7 — IFhd 13-byte odd length
- Blorb "The IFF Format" — FORM and chunk layout
- Blorb "AIFF Sounds" — Nested FORM with inner formtype

**Test Coverage (30 tests):**
- Reader basics: empty form, single chunk, multiple chunks in order,
  empty chunk (4)
- Padding: odd-length chunk, single-byte chunk (2)
- Nested FORM: inner form type exposed, coexists with regular chunks (2)
- Duplicates: first kept with warning, multiple ANNO all kept (2)
- Unknown chunks: preserved without error (1)
- Error handling: non-FORM throws, truncated header throws (2)
- Text chunks: AUTH/ANNO/(c) decode (1)
- IffForm helpers: GetChunk missing, GetChunks multiple (2)
- Writer basics: empty form, single chunk, odd-length padding, nested
  FORM, invalid type throws (5)
- Round-trip: multiple chunks, IFhd 13-byte, nested FORM, large chunk (4)
- Stream API: write+parse round-trip (1)
- FORM types: IFZS (Quetzal), IFRS (Blorb) (2)
- IffChunk properties: regular length, nested FORM length (2)

---

### Task 9.2 — Quetzal Save Implementation

**Date:** 2026-09-08

**Objective:** Write Z-Machine state to a Quetzal 1.4 save file (IFF FORM
'IFZS') with IFhd, CMem/UMem, and Stks chunks.

**Design Decisions:**

1. **Static `QuetzalWriter` class:** Like `IffWriter`, no instance state is
   needed. `Save(Stream, Interpreter, savePC)` and `SaveToArray` convenience
   method. The `savePC` parameter is passed explicitly because its value
   depends on version-specific semantics (Quetzal S5.8) that the interpreter's
   opcode dispatcher determines.

2. **IFhd chunk (Quetzal S5.4):** 13 bytes: 2-byte release ($02), 6-byte
   serial ($12), 2-byte checksum ($1C), 3-byte PC. Always first chunk.
   If the story has no checksum (old games), it's calculated from story bytes
   starting at $40 (Quetzal S5.5).

3. **CMem compression (Quetzal S3.2–S3.7):** XOR current dynamic memory with
   original, then run-length encode zeros. A zero byte followed by a count
   byte represents count+1 zeros. Non-zero bytes pass through verbatim.
   Trailing zeros are omitted (Quetzal S3.4). For zork1.z3 after a few moves,
   CMem is typically under 200 bytes vs 9780 bytes of dynamic memory.

4. **UMem fallback (Quetzal S3.8):** Raw dump of dynamic memory, controlled
   by `useCompression` parameter. Quetzal says ability to write UMem is
   optional but reading both is required (Task 9.3 will handle reading).

5. **Stks chunk (Quetzal S4):** Frames written oldest-first via
   `GetFramesBottomUp()`. Each frame: 3-byte return PC, flags byte (p=discard,
   vvvv=local count), store variable, argument flags (bit per arg), eval stack
   count word, local values, eval stack values (oldest first).

6. **Dummy frame for non-V6 (Quetzal S4.11):** V1-5 and V7-8 execution starts
   at an address, not a routine. The bottom frame is written as a dummy with
   all fields zero except eval stack count. The dummy frame is mandatory even
   if the eval stack is empty at the top level.

7. **Eval stack ordering:** `Stack<ushort>.ToArray()` returns top-first, but
   Quetzal S4.7 stores eval stack oldest-first. The writer reverses the array
   when writing.

8. **Big-endian consistency:** All multi-byte values are written big-endian
   to match IFF and Z-Machine conventions.

**Spec References:**
- Quetzal S2 — Overall IFZS structure
- Quetzal S3.2–S3.7 — CMem XOR+RLE compression
- Quetzal S3.8 — UMem uncompressed dump
- Quetzal S4.3 — Stack frame format
- Quetzal S4.11 — Dummy frame for non-V6
- Quetzal S5.4 — IFhd chunk format (13 bytes)
- Quetzal S5.5 — Checksum calculation for old games
- Quetzal S5.7 — IFhd odd-length padding
- Quetzal S5.8 — PC encoding: V1-3 branch data, V4+ store byte

**Test Coverage (26 tests):**
- Valid IFF: produces IFZS, starts with FORM, length correct (3)
- IFhd: comes first, 13 bytes, release number, serial number, checksum,
  PC, odd-length padded (7)
- CMem: present with compression, smaller than dynamic, decodes correctly,
  untouched memory minimal (4)
- UMem: present when uncompressed, matches dynamic memory exactly (2)
- Stks: present, non-empty, V3 dummy frame, frame count matches, second
  frame return PC (5)
- Chunk ordering: IFhd then CMem then Stks (1)
- Multi-version: Czech V5 valid IFZS, V5 IFhd valid (2)
- Round-trip: 3-move CMem decode matches, stream=array output (2)

---

### Task 9.3 — Quetzal Restore and Undo

**Date:** 2026-09-08

**Objective:** Read a Quetzal save file and reconstruct game state — validate
IFhd, decode CMem/UMem to restore dynamic memory, reconstruct call stack
from Stks, and set the PC.

**Design Decisions:**

1. **Static `QuetzalReader` class:** Mirrors `QuetzalWriter`. `Restore(Stream,
   Interpreter)` and `Restore(byte[], Interpreter)` parse the IFF, validate,
   and restore state in-place. Returns the saved PC.

2. **IFhd validation (Quetzal S5.3):** Compares release ($02), serial ($12),
   and checksum ($1C) against the loaded story's original bytes. Rejects
   mismatches with `QuetzalException`. Checksum calculated from file bytes
   if the story header has none (Quetzal S5.5).

3. **CMem decode (Quetzal S3.2–S3.4):** First restores original dynamic
   memory, then XOR-decompresses CMem data on top. Trailing zeros (S3.4)
   leave the original intact — no extra handling needed.

4. **CMem error handling (Quetzal S3.5):** Detects decoded data exceeding
   dynamic memory and incomplete runs (zero without length byte).

5. **Stks reconstruction (Quetzal S4):** Clears the existing call stack
   via new `CallStack.Clear()`, then pushes frames bottom-up. The dummy
   frame (S4.11) for non-V6 is the first frame with all fields zero except
   eval stack count. Eval stack values stored oldest-first in file.

6. **IntD tolerance (Quetzal S7.8):** Unknown chunks silently ignored.
   Save files from other interpreters restore correctly.

7. **QuetzalException:** Dedicated exception with clear messages for each
   validation failure.

**Spec References:**
- Quetzal S3.2–S3.7 — CMem XOR+RLE decompression
- Quetzal S3.4–S3.5 — Short data and error cases
- Quetzal S4.3, S4.11 — Stack frame format and dummy frame
- Quetzal S5.3 — IFhd validation
- Quetzal S7.8–S7.17 — IntD chunks tolerated

**Test Coverage (22 tests):**
- Save/restore round-trip: PC, dynamic memory, call stack, globals,
  save-modify-restore (5)
- CMem round-trip: 3-move compressed (1)
- UMem round-trip: uncompressed memory (1)
- Multi-version: Czech V5 (1)
- IFhd validation: wrong release/serial/checksum, missing IFhd/memory/Stks,
  wrong FORM type (7)
- CMem/UMem errors: incomplete run, wrong length (2)
- IntD: present doesn't cause rejection (1)
- Stks: locals, return PCs, dummy frame (3)
- Stream API: restore from stream (1)

---

## Phase 10: Blorb Resource Loading

### Task 10.1 — Blorb File Parser and Resource Index

**Date:** 2026-09-08

**Objective:** Parse Blorb 2.0.4 resource files (IFF FORM 'IFRS'), read the
resource index (RIdx), and provide indexed access to resources by usage and
number.

**Design Decisions:**

1. **IffReader duplicate handling fix:** The IffReader from Task 9.1 was
   designed for Quetzal and dropped duplicate chunks (keeping only the first).
   Blorb files legitimately have many chunks of the same type (e.g., multiple
   PNG or OGGV chunks for different resources). Changed IffReader to keep all
   chunks while still generating warnings for duplicates. This is a strictly
   additive change — QuetzalReader's `GetChunk()` already returns the first
   match, so Quetzal behavior is preserved.

2. **Offset-based resource resolution:** RIdx entries reference chunks by file
   offset. Since IffReader doesn't track chunk positions, BlorbReader computes
   offsets from the chunk sequence: starting at byte 12 (after FORM header),
   advancing by 8 + Length + padding for each chunk. This avoids modifying the
   IffReader API.

3. **AIFF reconstruction:** AIFF sounds are nested FORMs (FORM/AIFF). Our
   IffChunk stores the inner data without the FORM wrapper. `GetResource()`
   reconstructs the full AIFF file (FORM + length + AIFF + data) so consumers
   get a usable audio file.

4. **Resource type resolution:** `GetResourceType()` returns the inner form
   type for nested FORMs ("AIFF") and the chunk TypeId for regular chunks
   ("PNG ", "JPEG", "ZCOD", etc.). This gives consumers the actual format
   identifier regardless of IFF nesting.

5. **BlorbReader as instance class:** Unlike the static QuetzalReader/Writer,
   BlorbReader is an instance class that holds the parsed state. This makes
   sense because the resource index is queried repeatedly during gameplay,
   not used once for a save/restore operation.

**Spec References:**
- Blorb "Overall Structure" — FORM 'IFRS', RIdx must be first
- Blorb "Contents of the Resource Index Chunk" — 4-byte count + 12-byte entries
- Blorb "Picture Resource Chunks" — PNG, JPEG, Rect types
- Blorb "Sound Resource Chunks" — AIFF (nested FORM), OGGV, MOD
- Blorb "Data Resource Chunks" — TEXT, BINA chunk types
- Blorb "Executable Resource Chunks" — ZCOD, GLUL, etc.
- Blorb "The Color Palette Chunk" — direct color (1 byte) or RGB list (3n bytes)
- Blorb "Deprecated Chunks" — SNam (UTF-16 BE) skipped gracefully

**Files Created/Modified:**
- `src/ZMachine.Core/BlorbReader.cs` — Main parser: Load, HasResource,
  GetResource, GetResourceType, offset mapping, Plte parsing
- `src/ZMachine.Core/BlorbTypes.cs` — BlorbUsage constants, BlorbPalette class
- `src/ZMachine.Core/IffReader.cs` — Fixed duplicate handling: keep all chunks
- `tests/ZMachine.Tests/BlorbTests.cs` — 38 tests
- `tests/ZMachine.Tests/IffTests.cs` — Updated duplicate test

**Test Coverage (38 tests):**
- Basic loading: PNG, JPEG, Rect, multiple resources, multiple PNGs (5)
- Sound resources: AIFF reconstruction, Ogg, MOD, mixed types (4)
- Executable: ZCOD resource (1)
- Data resources: TEXT, BINA (2)
- Shared chunks: two entries pointing to same offset (1)
- Color palette: direct 16-bit, direct 32-bit, RGB list, illegal length,
  invalid direct value, no palette (6)
- Deprecated/unknown chunks: SNam, unknown types, optional metadata (3)
- Validation: wrong FORM type, missing RIdx, RIdx not first, empty form,
  truncated RIdx, bad offset, duplicate entries (7)
- HasResource/missing: false for missing, GetResource throws,
  GetResourceType throws (3)
- Stream API (1)
- Form property (1)
- Non-contiguous numbers (1)
- Zero-entry RIdx (1)
- Odd-length chunks with offset correctness (1)
- BlorbUsage constants (1)

---

### Task 10.2 — Story Loading from Blorb and Metadata

**Date:** 2026-09-08

**Objective:** Load Z-code executables from Blorb files, validate story/Blorb
compatibility via IFhd, detect Blorb files by extension, and parse optional
metadata chunks (RelN, Fspc, IFhd, IFmd, RDes, AUTH, (c), ANNO).

**Design Decisions:**

1. **Extension-based auto-detection:** The existing `Interpreter.Load(string)`
   method now detects `.blorb`, `.zblorb`, `.blb`, `.zlb` extensions and
   routes to Blorb loading automatically. Non-Blorb extensions continue to
   load as raw story files. This keeps the caller API simple — no need to
   know whether a file is Blorb or raw.

2. **Conflicting executable validation:** Three-way check per Blorb spec:
   - Blorb has exec + standalone story → error (conflicting)
   - Blorb has no exec + no standalone → error (nothing to run)
   - Non-ZCOD exec type → error (only Z-code supported)
   This is enforced in `LoadFromBlorb()` before any memory loading.

3. **IFhd validation for resource-only Blorbs:** When a Blorb has no
   executable (used as a resource pack with a standalone story), the IFhd
   chunk is validated against the loaded story — same fields as Quetzal
   (release, serial, checksum). This catches story/resource mismatches.

4. **BlorbReader metadata properties:** Added parsed properties for all
   optional chunks directly on BlorbReader: ReleaseNumber, FrontispiecePicture,
   GameIdentifier, MetadataXml, Author, Copyright, Annotations, and
   ResourceDescriptions. Absent metadata has sensible defaults (0, -1, null,
   empty). This avoids a separate metadata class while keeping the data
   accessible for UI display and @picture_data queries.

5. **Interpreter.Blorb property:** The loaded BlorbReader is exposed via
   `Interpreter.Blorb` (null when loading a raw story). This makes resources
   available to later phases (picture/sound loading) without re-parsing.

6. **RDes parsing:** Resource descriptions use variable-length entries with
   UTF-8 text. Parsed into a dictionary keyed by (usage, number) for O(1)
   lookup — useful for accessibility features (alt-text for images).

**Spec References:**
- Blorb "Executable Resource Chunks" — ZCOD extraction, conflicting exec rules
- Blorb "The Game Identifier Chunk" — IFhd format (same as Quetzal S5)
- Blorb "The Release Number Chunk" — 2-byte big-endian value
- Blorb "The Frontispiece Chunk" — 4-byte picture resource number
- Blorb "The Resource Description Chunk" — variable-length UTF-8 entries
- Blorb "Metadata" — UTF-8 XML in IFmd chunk
- Blorb "File Suffixes" — .blorb, .zblorb, .blb, .zlb

**Files Created/Modified:**
- `src/ZMachine.Core/BlorbReader.cs` — Added metadata parsing (RelN, Fspc,
  IFhd, IFmd, AUTH, (c), ANNO, RDes), ValidateIFhd method
- `src/ZMachine.Core/ZMachine.cs` — Added Blorb property, extension detection,
  Load overloads for Blorb + standalone story, LoadFromBlorb core method
- `tests/ZMachine.Tests/BlorbStoryLoadingTests.cs` — 22 tests

**Test Coverage (22 tests):**
- ZCOD extraction: identical to raw, Blorb property set, can execute (3)
- Resource-only Blorb: standalone story + resource pack (1)
- Conflicting exec: exec + standalone, no exec + no story, non-ZCOD exec (3)
- IFhd validation: matching, mismatched release, mismatched serial (3)
- Extension detection: .zblorb, .blorb, .zlb, .blb, .z3 raw (5)
- Metadata: RelN, Fspc, IFmd XML, AUTH/(c)/ANNO, RDes, absent defaults (6)
- Interpreter Blorb property: null for raw story (1)

### Task 10.3 — Blorb Resource Discovery and Header Flag Integration

**Date**: 2026-09-08

#### Steps Taken

1. **Added `PictureCount` and `SoundCount` properties to `BlorbReader`** — populated
   at the end of `ParseRIdx()` using LINQ counts filtered by `BlorbUsage.Picture`
   and `BlorbUsage.Sound`. These give the interpreter a way to query resource
   availability without iterating the resource dictionary directly.

2. **Implemented `UpdateHeaderFlags()` in the `Interpreter`** — a private method
   called at the end of `Init()` that:
   - **Flags 1 (byte $01)**: Sets bit 1 (0x02, picture display) if Blorb has
     Pict resources, clears otherwise. Sets bit 5 (0x20, sound effects) if Blorb
     has Snd resources, clears otherwise.
   - **Flags 2 high byte ($10)**: For V5+, clears bit 0 (pictures requested) if
     no pictures available, clears bit 4 (sound requested) if no sounds available.
     Preserves the bits when resources are available.
   - **Version guard**: Skips entirely for V1–V3, since these versions don't use
     the picture/sound capability flags.
   - **Warnings**: When the game sets request bits but no Blorb is loaded,
     generates a warning (stored in `BlorbWarnings`). Per ZSpec11, the interpreter
     should prompt for a Blorb file at startup in this situation.

3. **Added `_blorbWarnings` field and `BlorbWarnings` property** — a simple
   `List<string>` / `IReadOnlyList<string>` pair on the Interpreter, surfacing
   any header flag warnings to callers without requiring a callback mechanism.

#### Design Decisions

- **Separate from Blorb.Warnings**: BlorbReader has its own parse warnings
  (duplicate entries, malformed chunks). Header flag warnings are interpreter-level
  concerns, so they live on the Interpreter as `BlorbWarnings`.

- **Clear rather than error on missing resources**: ZSpec11 says to "clear if no
  resources loaded" — the game can still run in text mode. An error would be too
  aggressive; a warning lets the host UI decide how to inform the user.

- **V4+ threshold for Flags 1, V5+ for Flags 2**: Flags 1 picture/sound bits
  exist from V4, but Flags 2 (the game request word at $10) is only meaningful
  from V5. V3 stories don't have these capabilities, so `UpdateHeaderFlags()`
  returns immediately for V1–V3.

- **Bit manipulation approach**: Used explicit set/clear masks
  (`flags |= mask` / `flags &= ~mask`) rather than conditional assignment.
  This preserves unrelated bits in the same flag byte.

#### Spec References

- ZSpec11 "Header capabilities bits" (lines 135–153): Flags 2/3 should reflect
  current availability; interpreters should prompt for Blorb on startup if game
  requests graphics/sound.
- ZSpec S11 — Header byte $01 (Flags 1): bit 1 = picture display, bit 5 = sound.
- ZSpec S11 — Header word $10 (Flags 2): bit 0 = pictures, bit 4 = sound.
- Blorb "Contents of the Resource Index Chunk" — resource usage types.

**Test Coverage (19 tests):**
- PictureCount/SoundCount: pictures only, sounds only, both, zero (4)
- Flags 1 capability bits: pictures set, sounds set, no Blorb cleared,
  no pictures cleared, both set (5)
- Flags 2 request bits: pictures cleared, sound cleared, pictures preserved,
  sound preserved (4)
- Blorb warnings: pictures no Blorb, sound no Blorb, both no Blorb,
  Blorb present no warnings, no requests no warnings (5)
- Version guard: V3 story flags not modified (1)

---

## Phase 11: GUI Framework and Vintage Themes

### Task 11.1 — Avalonia UI Project Setup and Rendering Abstraction

**Date**: 2026-09-08

#### Steps Taken

1. **Updated `ZMachine.App` to an Avalonia application** — changed `OutputType` to
   `WinExe`, added NuGet packages: `Avalonia` 12.1.2, `Avalonia.Desktop` 12.1.2,
   `Avalonia.Skia` 12.1.2, `Avalonia.Themes.Fluent` 12.1.2, `SkiaSharp` 4.151.2.
   Also added `AllowUnsafeBlocks` for the bitmap pixel copy in the canvas control.

2. **Added `SkiaSharp` to `ZMachine.IO`** — the rendering layer needs direct
   access to SkiaSharp types (SKBitmap, SKCanvas, SKColor, SKPaint).

3. **Created `ThemeConfig`** (IO project) — configuration class for vintage display
   themes: character dimensions (8×16 default), grid size (80×25), border width,
   Z-Machine color palette (colors 2–15 → RGB), default fg/bg, scanline and CRT
   curvature toggles, chrome mode (Borderless/Standard), and bitmap font data slot.

4. **Created `IRenderer` interface** (IO project) — pixel-level rendering abstraction:
   Initialize, DrawCharacter, DrawRegion, DrawImage, SetCursorPosition, Refresh,
   GetScreenSize, BackBuffer property.

5. **Created `SkiaRenderer`** (IO project) — IRenderer implementation drawing to
   an off-screen SKBitmap back buffer. Character rendering uses a 1-bit-per-pixel
   bitmap font with bold (shift right), italic (shift top half), and reverse video
   (swap fg/bg) support. Fires `OnRefresh` event for UI invalidation.

6. **Created `BuiltInFont`** (IO project) — 8×16 monospace bitmap font covering
   ASCII 32–126 (95 glyphs, 1520 bytes). VGA-style glyphs generated procedurally.
   Serves as the default fallback before theme-specific fonts are loaded (Task 11.2).

7. **Created `GuiScreen`** (IO project) — IScreen implementation backed by a
   character grid ([row,col] for char, fg, bg, style). Maps Z-Machine screen ops
   to IRenderer calls. Implements word wrapping, scrolling, upper/lower window
   split, status line, cursor positioning, and text styling.

8. **Created `GuiInputStream`** (IO project) — IInputStream implementation using
   a thread-safe blocking queue. GUI key events enqueue characters/lines; the
   Z-Machine background thread blocks on Dequeue. Supports timed input via
   TryDequeue with timeout.

9. **Created Avalonia application structure** (App project):
   - `App.axaml` / `App.axaml.cs` — Avalonia Application with FluentTheme (dark)
   - `MainWindow.axaml` / `MainWindow.axaml.cs` — main window with native menu bar
     (File: Open Story, Open Blorb, Quit; Tools: placeholder items; Options:
     placeholder items; Help: About), `SkiaCanvasControl` filling the client area,
     keyboard input handling (KeyDown for special keys, TextInput for printable chars)
   - `SkiaCanvasControl.cs` — custom Avalonia control that copies the SkiaRenderer
     back buffer to a WriteableBitmap on each Refresh for display. Scales to fill
     the control while maintaining aspect ratio.
   - `Program.cs` — entry point with `AppBuilder.Configure<App>()`

10. **Line-input mode** in MainWindow — accumulates typed characters in a buffer,
    submits on Enter. Echoes input to the GuiScreen for visual feedback.

#### Design Decisions

- **Avalonia 12.1.2**: Latest stable release with .NET 10 support. Avalonia 12
  removed `WithInterFont()` from AppBuilder (Avalonia 11 API); removed the call.

- **SkiaSharp in IO, not just App**: The rendering abstraction (IRenderer,
  SkiaRenderer, ThemeConfig) belongs in the IO layer alongside IScreen and
  ConsoleScreen. The App project adds the Avalonia host wrapper.

- **WriteableBitmap pixel copy**: The SkiaRenderer draws to an SKBitmap back
  buffer. The Avalonia SkiaCanvasControl copies pixels to a WriteableBitmap via
  unsafe pointer copy on each frame. This avoids Avalonia's SkiaSharp interop
  layer (which expects the GPU pipeline) and gives us pixel-precise control.

- **Blocking queue for input**: The Z-Machine runs on a background thread. GUI
  events post to a thread-safe queue; the interpreter blocks on ReadLine/ReadChar.
  This cleanly separates the execution thread from the UI thread without callbacks.

- **Native menu bar**: Used Avalonia's `NativeMenu` for platform-appropriate menus
  (macOS menu bar, Windows/Linux title bar). Tools and Options items are disabled
  placeholders for future tasks.

- **ConsoleScreen preserved**: The console backend remains as the headless/test
  fallback per the task requirements. GuiScreen is an independent implementation.

#### Spec References

- ZSpec S8 — Screen model: text output, windows, cursor positioning, styling.
- ZSpec S8.2 — Status line format (reverse video, location + score).
- ZSpec S8.3.1 — True colour table (Z-Machine colors 2–15).
- ZSpec S8.7 — @split_window, @set_window, @erase_window, @set_cursor.
- ZSpec S10 — Input streams, keyboard input, timed input.
- ZSpec S10.5 — ZSCII cursor/function key mappings (129–154).

**Test Coverage (27 tests):**
- ThemeConfig: defaults, pixel dimensions, color mapping, out-of-range, palette size (5)
- BuiltInFont: data size, space glyph, letter A pixels, ASCII coverage (4)
- SkiaRenderer: back buffer creation, screen size, draw character, draw region,
  refresh event, reverse style (6)
- GuiScreen: screen size, print draws, split/set window, erase window -1,
  set text style, status line, buffer mode flush (7)
- GuiInputStream: line input, char input, line timeout, char timeout, truncation (5)

### Task 11.2 — Bitmap Font System

**Date**: 2026-09-08

#### Steps Taken

1. **Created the `BitmapFont` class** (`src/ZMachine.IO/BitmapFont.cs`) as the
   unified API for bitmap font rendering. The class wraps raw 1-bit-per-pixel
   glyph data and provides:
   - `GetGlyph(char)` — returns row bytes for a character
   - `RenderGlyph(...)` — draws directly to an SKBitmap with style support
     (reverse, bold, italic)
   - `ResolveGlyphIndex(char)` — maps ZSCII 155–251 extra characters to
     unaccented ASCII equivalents via a configurable mapping table
   - `CreateBuiltIn()` factory — wraps the existing BuiltInFont VGA 8×16 data

2. **Created vintage font data sets** (`src/ZMachine.IO/FontData.cs`) with six
   platform-specific bitmap fonts:
   - **C64 8×8** — Commodore 64 uppercase PETSCII style, rounded forms
   - **Apple II 7×8** — 7-pixel-wide character generator (fits 40/80 column
     modes depending on pixel doubling)
   - **CGA 8×8** — IBM PC CGA character ROM
   - **EGA 8×14** — IBM PC EGA with taller glyphs for improved readability
   - **VGA 8×16** — delegates to existing BuiltInFont data
   - **Amiga Topaz 8×8** — Amiga Workbench Topaz-style, wider strokes

   Each font covers all 95 printable ASCII characters (32–126) with
   hand-crafted glyph bitmaps faithful to the original platform aesthetics.

3. **Added `Font` property to ThemeConfig** — new `BitmapFont?` property that,
   when set, overrides the raw `FontBitmap`/`FontFirstChar`/`FontGlyphCount`
   fields. This provides a clean migration path: existing code using raw byte
   arrays continues to work, while new theme definitions use BitmapFont.

4. **Refactored SkiaRenderer** to detect and use `BitmapFont` when the theme's
   `Font` property is set. `DrawCharacter()` delegates to
   `BitmapFont.RenderGlyph()` for the full rendering pipeline (background fill,
   glyph pixels, bold shift, italic shift, reverse swap). Falls back to the
   existing raw byte array code path when no BitmapFont is provided.

5. **ZSCII extra character mapping** — `BuildDefaultZsciiMap()` maps ZSCII
   codes 155–251 (accented Latin characters per ZSpec S3.8.5) to their closest
   unaccented ASCII equivalents. This allows bitmap fonts that only contain
   ASCII 32–126 to still display accented text legibly. The mapping is
   configurable per-font via the constructor's `zsciiMap` parameter.

#### Design Decisions

- **BitmapFont wraps raw data rather than replacing it.** The existing
  `BuiltInFont` static class and the raw `FontBitmap` byte arrays in
  ThemeConfig continue to work. BitmapFont is additive — it provides a richer
  API (ZSCII mapping, named access, style rendering) without breaking existing
  code paths. This was chosen over a flag-day refactor to keep the test suite
  green throughout development.

- **Font data is procedurally defined, not loaded from ROM files.** Using
  embedded byte arrays means no external file dependencies and no copyright
  concerns with actual ROM dumps. The glyph patterns are hand-crafted to match
  the visual style of each platform's original character set.

- **Apple II uses 7-pixel width.** The original Apple II character generator
  produces 7-pixel-wide characters. Rather than padding to 8 pixels, we
  preserve the authentic width. ThemeConfig's `CharWidth` property must be set
  to 7 when using this font, which affects `PixelWidth` calculations
  throughout the rendering pipeline.

- **CGA shares C64 data.** At 8×8 resolution, the IBM PC CGA and C64 character
  ROMs are nearly identical. Rather than duplicating 760 bytes of data with
  minor cosmetic differences, CGA reuses the C64 glyph data. If platform
  purists need differentiation, individual glyphs can be overridden later.

#### Spec References

- ZSpec S3.8.5 — Default ZSCII extra characters (155–251): accented Latin
  characters mapped to Unicode code points, here approximated to ASCII.
- ZSpec S8 — Screen model: fixed-width character cells, all text rendered
  through the font system.
- ZSpec S8.7.1 — Text styles: reverse video (swap fg/bg), bold (shifted
  overdraw), italic (top-half shift).

**Test Coverage (30 tests):**
- BitmapFont Core API: dimensions, glyph length, space blank, letter A pixels,
  out-of-range fallback, ZSCII 155→'a', ZSCII 159→'O' (7)
- FontData Vintage Fonts: C64 dimensions/pixels, Apple II dimensions/pixels,
  CGA dimensions, EGA dimensions/pixels, VGA matches BuiltIn, Amiga
  dimensions/pixels, GetAll count, all spaces blank, all printable ASCII (13)
- ZORK Rendering: VGA/C64/Apple II/EGA pixel dimensions (4)
- SkiaRenderer Integration: BitmapFont draw, reverse style, fallback to
  BuiltIn, Apple II dimensions (4)
- RenderGlyph Styles: bold shifts pixels, italic differs from normal (2)

### Task 11.3 — C64 Classic Theme

**Date**: 2026-09-08

#### Steps Taken

1. **Created the `ITheme` interface** (`src/ZMachine.IO/ITheme.cs`) — a simple
   contract requiring `Name` and `CreateConfig()`. Each vintage theme implements
   this interface to provide its platform-specific ThemeConfig.

2. **Created `C64Theme`** (`src/ZMachine.IO/C64Theme.cs`) implementing `ITheme`
   with authentic Commodore 64 display parameters:
   - 40 columns × 25 rows, 8×8 character cells (320×200 logical pixels)
   - 32-pixel border (matching PAL overscan proportions)
   - VIC-II 16-color palette mapped to Z-Machine colors 2–15
   - Default foreground: light blue (#7869C4, C64 color 14)
   - Default background and border: medium blue (#40318D, C64 color 6)
   - Uses the C64 8×8 BitmapFont from FontData
   - Borderless chrome mode for full-screen retro feel

3. **Added cursor blinking** to SkiaRenderer via `DrawCursor()`:
   - Block cursor drawn as a filled rectangle at the current cursor position
   - ~1 Hz toggle (500ms interval) using `Environment.TickCount64`
   - `SetCursorPosition()` resets the blink timer so the cursor is immediately
     visible when the position changes

4. **Added post-processing effects** to SkiaRenderer (all toggleable via
   ThemeConfig flags):
   - `ApplyScanlines()` — darkens every other pixel row by ~30%
   - `ApplyCrtCurvature()` — barrel distortion warping pixels outward from
     center, with configurable strength parameter
   - `ApplyPhosphorBloom()` — brightens pixels adjacent to bright areas,
     simulating CRT phosphor bleed

5. **Added `PhosphorBloom` property** to ThemeConfig alongside existing
   `Scanlines` and `CrtCurvature` flags.

#### Design Decisions

- **VIC-II palette sourced from VICE emulator PAL values.** The C64 didn't have
  a standardized RGB palette — it output composite video. Different capture
  methods produce different RGB approximations. The VICE PAL palette is the most
  widely recognized reference and matches the visual expectations of C64 users.

- **32-pixel border.** The original C64 PAL display had visible overscan borders.
  The 32-pixel border creates the characteristic "framed" look where the text area
  floats within a colored border, matching the reference screenshot.

- **Post-processing effects are per-pixel, not shader-based.** SkiaSharp doesn't
  expose GPU shader programs in the way needed for real-time CRT emulation. The
  per-pixel approach works well for the 320×200 logical resolution of the C64
  theme (only 64,000 pixels per frame). For higher-resolution themes, the effects
  may need optimization or could be applied at a lower resolution.

- **Cursor blink uses `Environment.TickCount64` rather than a timer thread.**
  The blink state is evaluated lazily when `DrawCursor()` is called, avoiding
  the need for a separate timer and cross-thread synchronization. The cursor
  appears immediately after `SetCursorPosition()` resets the blink timer.

#### Spec References

- ZSpec S8 — Screen model: 40-column display, split windows, status line.
- ZSpec S8.2 — Status line: reverse-video bar at row 0 (V1–3).
- ZSpec S8.3.1 — True colour table mapped to VIC-II palette.
- ZSpec S8.7.1 — Text styles applied via BitmapFont rendering.

**Test Coverage (25 tests):**
- ITheme: implements interface, CreateConfig returns config (2)
- Screen Dimensions: 40×25, pixel dimensions with borders (2)
- Color Palette: 14 entries, foreground light blue, background medium blue,
  border medium blue, black is color 2, white is color 9 (6)
- Font: C64 8×8 BitmapFont (1)
- Chrome: borderless mode (1)
- Renderer Integration: correct initialization, draws correct colors, status
  line reverse video, border color fill (4)
- Cursor Blinking: draws block, SetCursorPosition makes visible (2)
- Post-Processing: scanlines darken rows, CRT curvature warps edges, phosphor
  bloom brightens neighbors, PhosphorBloom defaults false, C64 defaults off (5)
- Full Rendering: ZORK produces visible text, GuiScreen 40×25 (2)

### Task 11.4 — Modern C64 Theme

**Date**: 2026-09-08

#### Steps Taken

1. **Created `ModernC64Theme`** (`src/ZMachine.IO/ModernC64Theme.cs`) implementing
   `ITheme` with a modern windowed interpretation of the C64 aesthetic:
   - 80 columns × 30 rows — wider than the classic 40-column layout
   - VGA 8×16 font for comfortable reading at modern resolutions
   - Bright blue background (#0050A4) with white text
   - Minimal 4px border matching the background color
   - Standard chrome mode (OS title bar + native menu bar)
   - No CRT post-processing effects — clean, modern presentation

2. **Designed the Modern C64 palette** — 14 colors mapped to Z-Machine colors
   2–15, using more saturated, modern values than the classic VIC-II palette
   while keeping the spirit of the C64 color scheme.

#### Design Decisions

- **80 columns × 30 rows with VGA 8×16.** The screenshot shows a modern window
  with wider-than-C64 text. 80 columns is the standard terminal width and
  matches the screenshot's proportions. VGA 8×16 provides clean, legible text
  without anti-aliasing, at a comfortable size for modern displays.

- **Standard chrome mode.** Unlike the C64 Classic's borderless full-screen
  layout, the modern variant uses OS window chrome with a title bar and menu
  bar (File, Tools, Help), matching the reference screenshot's windowed layout.

- **Minimal border (4px).** The screenshot shows text extending nearly edge to
  edge within the canvas area. A 4px border provides just enough margin for
  clean appearance without the wide CRT-style borders of the classic theme.

- **No post-processing.** The modern theme is designed to be crisp and clean,
  without scanlines or CRT effects. This matches the reference screenshot and
  provides a contrasting aesthetic option alongside the vintage C64 Classic.

#### Spec References

- ZSpec S8 — Screen model: 80-column display, wider than classic C64.
- ZSpec S8.3.1 — True colour table mapped to modern C64-inspired palette.

**Test Coverage (22 tests):**
- ITheme: implements interface, CreateConfig returns config (2)
- Screen Dimensions: 80×30, VGA 8×16 font, minimal border, pixel dims (4)
- Color Palette: 14 entries, white foreground, bright blue background, border
  matches background, black is color 2 (5)
- Chrome and Effects: standard chrome, no post-processing (2)
- Font: VGA BitmapFont (1)
- Renderer Integration: initialization, white-on-blue draw, border color,
  GuiScreen 80×30, ZORK rendering (5)
- Classic vs Modern: wider columns, chrome mode, brighter blue (3)

### Task 11.5 — Apple II Theme

**Date**: 2026-09-08

#### Steps Taken

1. **Created `AppleIITheme`** (`src/ZMachine.IO/AppleIITheme.cs`) implementing
   `ITheme` with authentic Apple II monochrome phosphor display:
   - 40 columns × 24 rows, 7×8 character cells (280×192 logical pixels)
   - 24-pixel border matching the phosphor background
   - P1 green phosphor: foreground #33FF33, background #001100
   - All Z-Machine colors 3–15 mapped to phosphor green — true monochrome
   - Color 2 (black) maps to phosphor background for correct reverse video
   - Apple II 7×8 BitmapFont from FontData
   - Borderless chrome mode

#### Design Decisions

- **True monochrome palette.** Every Z-Machine color except black resolves to
  phosphor green. This matches the original Apple II hardware which had no color
  text mode — the P1 phosphor produced only green-on-dark. The only visual
  distinction available is reverse video (swapping fg/bg), which the Z-Machine
  status line uses.

- **Color 2 maps to background, not green.** If black also mapped to green,
  reverse video (which swaps fg/bg colors) would be invisible — both colors
  would be green. Mapping black to the phosphor background color ensures
  reverse-video works correctly.

- **24-pixel border.** The Apple II had visible overscan borders on its display.
  A 24-pixel border simulates this while keeping the overall frame proportional
  to the 280×192 character area.

- **40×24 (not 40×25).** The Apple II text mode was 40×24 lines, not 25. This
  is one row fewer than the C64 and IBM PC, matching the original hardware.

#### Spec References

- ZSpec S8 — Screen model: 40-column display, monochrome rendering.
- ZSpec S8.2 — Status line: inverse video bar at top row.
- ZSpec S8.3.1 — Colour table: all entries mapped to phosphor green.
- ZSpec S8.7.1 — Reverse video: the only visual styling on monochrome display.

**Test Coverage (22 tests):**
- ITheme: implements interface, CreateConfig (2)
- Screen Dimensions: 40×24, 7×8 cells, border, pixel dimensions (4)
- Monochrome Palette: all colors green, black maps to background, default
  fg/bg, border color, 14 entries (6)
- Font: Apple II 7×8 BitmapFont (1)
- Chrome and Effects: borderless, no post-processing (2)
- Renderer Integration: initialization, only green pixels, all colors render
  green, reverse video swaps, status line reverse green, border dark green,
  GuiScreen 40×24, ZORK green text (7)

### Task 11.6 — DOS Monochrome Themes (Green Screen and Amber Screen)

**Date**: 2026-09-08

#### Steps Taken

1. **Created `DosMonochromeTheme` abstract base class**
   (`src/ZMachine.IO/DosMonochromeTheme.cs`) — shared layout and font logic:
   - 80 columns × 25 rows, EGA 8×14 font (MDA resolution proportions)
   - 8-pixel border, borderless chrome mode
   - Abstract properties for phosphor colors (normal, bright, background)
   - `BuildMonochromePalette()` maps all Z-Machine colors to phosphor; white
     (color 9) maps to bright/intensified phosphor for bold distinction

2. **Created `DosGreenTheme`** — P1 green phosphor:
   - Normal: #33FF33, Bright: #66FF66, Background: #0A1A0A

3. **Created `DosAmberTheme`** — P3 amber phosphor:
   - Normal: #FFB000, Bright: #FFD060, Background: #1A0F00

#### Design Decisions

- **Shared base class rather than composition.** Both themes are identical
  except for color values. An abstract base with three color property overrides
  is the cleanest factoring — no config objects, no builders, just subclass
  and supply colors.

- **White (color 9) maps to bright phosphor.** This gives the default
  foreground text the intensified/bold appearance, matching how MDA monitors
  rendered "normal intensity" vs "high intensity" text. Other colors map to
  normal phosphor since monochrome displays can't distinguish them.

- **EGA 8×14 font.** The IBM MDA used 9×14 characters, but the extra pixel
  column was hardware-generated (duplicating column 8 for box-drawing chars).
  The EGA 8×14 font is the closest standard representation.

#### Spec References

- ZSpec S8 — Screen model: 80-column display, monochrome rendering.
- ZSpec S8.3.1 — Colour table: all entries mapped to phosphor colors.
- ZSpec S8.7.1 — Text styles: bold→bright, reverse→swap fg/bg.

**Test Coverage (33 tests):**
- ITheme: green implements, green config, amber implements, amber config (4)
- Shared Layout: green 80×25, amber 80×25, pixel dims, green EGA font,
  amber EGA font (5)
- Green Palette: 14 entries, all green, black→bg, bright fg, border (5)
- Amber Palette: 14 entries, all amber, black→bg, bright fg, border (5)
- Chrome/Effects: green borderless, amber borderless, green no fx, amber no fx (4)
- Green Renderer: init, only-green pixels, ZORK green, GuiScreen 80×25 (4)
- Amber Renderer: init, only-amber pixels, ZORK amber, GuiScreen 80×25 (4)
- Shared Base: same dimensions/font, different colors (2)

---

### Task 11.7 — DOS CGA Color Theme

**Date:** 2026-09-08

#### What Was Done

Implemented `DosColorTheme` — an IBM PC CGA/EGA 16-color text mode theme
matching the classic DOS gaming aesthetic. The theme provides:

- **80×25 character grid** with EGA 8×14 font, matching standard DOS text mode
- **Full CGA palette** mapped to Z-Machine colors 2–15 using authentic CGA
  hardware color values (0x00, 0x55, 0xAA, 0xFF per channel)
- **Default DOS colors**: light grey (CGA 7) on black for the lower window
- **Status line support**: white (CGA 15) on blue (CGA 1) available via the
  palette for the classic DOS adventure game status bar

#### Design Decisions

- **CGA Yellow vs Brown for Z-Machine "Yellow" (color 5).** CGA has both
  Brown (CGA 6, #AA5500) and Yellow (CGA 14, #FFFF55). Since the Z-Machine
  calls color 5 "Yellow", we map it to CGA bright yellow (#FFFF55). CGA Brown
  (#AA5500) maps to Z-Machine 13 ("Orange") instead — it's the closest warm
  color in the CGA palette.

- **14 palette slots for 16 CGA colors.** Z-Machine colors 2–15 give us 14
  slots but CGA has 16 colors. The mapping prioritizes semantic correctness
  (Z-Machine color names match CGA equivalents) and uses the reserved slots
  (14, 15) for Light Magenta and Light Cyan. CGA colors Light Blue (#5555FF),
  Light Green (#55FF55), and Light Red (#FF5555) are omitted — they're rarely
  used in Z-Machine games and the semantic slots are more important.

- **Two grey slots (11, 12) both map to CGA Dark Grey (#555555).** CGA has
  only two greys (Light Grey CGA 7, Dark Grey CGA 8), while Z-Machine defines
  three (Light, Medium, Dark). Both medium and dark map to the single CGA
  dark grey — there's no better CGA approximation.

- **DefaultForeground = 10 (Light grey), not 9 (White).** Standard DOS text
  mode uses CGA attribute 07h (Light Grey foreground), not 0Fh (White). White
  was the "high intensity" variant. This matches what users remember from DOS.

- **No post-processing effects.** CGA/EGA monitors were sharp digital displays
  without the phosphor bloom or scanline artifacts of CRT-based themes.

#### Spec References

- ZSpec S8 — Screen model: 80-column color display with 16-color palette.
- ZSpec S8.3.1 — Colour table: Z-Machine colors 2–15 mapped to CGA values.

**Test Coverage (35 tests):**
- ITheme: implements, config (2)
- Layout: 80×25, pixel dims, EGA font, border (4)
- Palette: 14 entries, all 14 colors individually, CGA values, fully opaque (17)
- Defaults: light grey fg, black bg, status line white/blue (3)
- Chrome/Effects: borderless, black border, no post-processing (3)
- Renderer: init, CGA colors, red-on-blue, GuiScreen 80×25 (4)
- Comparison: same dims as monochrome, different palettes (2)

---

### Task 11.8 — Amiga Theme

**Date:** 2026-09-08

#### What Was Done

Implemented `AmigaTheme` — an Amiga Workbench 1.x-inspired theme using the
authoritative ZSpec11 gamma-adjusted Amiga V6 colour set. The theme provides:

- **80×25 character grid** with Amiga Topaz 8×8 font
- **ZSpec11 Amiga palette** for Z-Machine colours 2–12, derived from the
  spec's 15-bit true colour values via (val << 3) | (val >> 2) expansion
- **Workbench 1.x accents** for reserved colours 13–15 (orange, blue, grey)
- **Standard chrome** mode with Workbench blue border for the Amiga frame feel
- **16px border** giving room for the classic Workbench window aesthetic

#### Design Decisions

- **ZSpec11 true colour values as authoritative source.** The spec provides
  15-bit true colour values (bits 14–10 blue, 9–5 green, 4–0 red) for the
  Amiga V6 colour set. These were gamma-adjusted from original 8-bit Amiga
  values using Z = 31 * [(Amiga / 15) ^ (1.8/2.2)]. I expand each 5-bit
  channel to 8-bit via the standard method: (val << 3) | (val >> 2). The
  tests verify both the conversion and the palette match.

- **White on black default rather than white on blue.** While the Amiga
  Workbench desktop was blue, Infocom's Amiga Z-Machine interpreter typically
  used black backgrounds for game text. White on black is more universally
  compatible across Z-Machine games.

- **Workbench colours for reserved slots 13–15.** The ZSpec11 colour table
  marks 13–14 as "reserved" and 15 as "transparent (V6)". Since the Amiga
  theme has a distinct visual identity, the reserved slots carry Workbench 1.x
  accent colours — orange (#FF8800), blue (#0055AA), and grey (#AAAAAA) —
  giving V6 games access to the Amiga's signature colour scheme.

- **ChromeMode.Standard** selected for the Amiga's decorative window style.
  The Workbench had distinctive title bars with close/depth gadgets. The
  actual Amiga chrome rendering is a renderer concern — the theme config
  establishes Standard mode so the renderer knows to draw window chrome.

#### Spec References

- ZSpec11 "Colour numbers" — Amiga V6 colour set, gamma-adjusted.
- ZSpec11 "Colour numbers" — Gamma formula: Z = 31 * [(A/15)^(1.8/2.2)].
- ZSpec S8.3.1 — True colour encoding (15-bit, BGR 5-5-5).

**Test Coverage (43 tests):**
- ITheme: implements, config (2)
- Layout: 80×25, pixel dims, Topaz font, border (4)
- ZSpec11 Palette: 14 entries, 11 individual colours verified against spec,
  all opaque, grey uniformity (15)
- Workbench Accents: orange, blue, grey (3)
- Defaults/Chrome: white fg, black bg, status line, standard chrome,
  WB blue border, no post-processing (6)
- Renderer: init, Amiga colours, GuiScreen 80×25 (3)
- True Colour Verification: 11 Theory cases cross-checking 15-bit→8-bit
  conversion against palette (11, included in total above)

---

### Task 11.9 — Theme Selection UI and Preferences

**Date:** 2026-09-08

#### What Was Done

Implemented the theme selection and user preferences system, completing
Phase 11. Three new components:

1. **ThemeRegistry** (`src/ZMachine.IO/ThemeRegistry.cs`) — central registry
   of all 7 vintage themes. Provides `GetAll()` for enumeration and
   `GetByName()` for lookup by display name (case-insensitive). Default
   theme is C64 Classic.

2. **UserPreferences** (`src/ZMachine.IO/UserPreferences.cs`) — JSON-backed
   preferences model persisted to `~/.zai-machine/preferences.json`. Stores
   selected theme, CRT effect toggles (scanlines, curvature, bloom), sound
   volume, and window geometry. Handles missing files (returns defaults),
   corrupt JSON (returns defaults), and partial JSON (merges with defaults).
   `ResolveTheme()` maps the saved theme name to an `ITheme` via the registry.

3. **MainWindow integration** (`src/ZMachine.App/`) — updated to load
   preferences on startup, resolve the saved theme, and build a dynamic
   Options → Theme submenu from `ThemeRegistry.GetAll()`. `SwitchTheme()`
   reinitializes the renderer with the new theme's config, redraws, and
   saves the preference. The current theme is marked with a bullet prefix.
   `LoadAndRunStory()` now uses the selected theme instead of a bare
   `ThemeConfig()`.

#### Design Decisions

- **ThemeRegistry as static class, not DI.** All themes are statically known
  at compile time with no runtime plugins. A static registry with `GetAll()`
  and `GetByName()` is the simplest correct design. If plugin themes are
  needed later, this can become an interface.

- **Preferences in ZMachine.IO, not ZMachine.App.** Both the preferences
  model and the theme registry live in ZMachine.IO so they're testable
  without an Avalonia dependency. Only the menu-building code lives in the
  App project.

- **Graceful fallback on load failure.** `UserPreferences.Load()` catches
  `JsonException` and `IOException`, returning defaults. `ResolveTheme()`
  falls back to `ThemeRegistry.Default` for unknown theme names. A user
  with a corrupt preferences file gets a working app, not a crash.

- **NativeMenu API for theme items.** Avalonia's `NativeMenuItem` is not a
  `Control`, so `FindControl<T>()` can't locate it. Instead, we traverse
  the `NativeMenu.GetMenu(this)` attached property to find the Options
  submenu and insert theme items programmatically.

#### Spec References

- ZSpec S8 — Screen model: theme determines visual presentation.

**Test Coverage (34 tests):**
- ThemeRegistry Enumeration: 7 themes, all expected names, all ITheme,
  all valid configs, default C64 Classic (5)
- ThemeRegistry Lookup: 7 by-name lookups, case-insensitive, null for
  unknown (9)
- Preferences Defaults: all default values correct (1)
- JSON Round-Trip: save/load, creates directory, valid JSON, missing file,
  corrupt JSON, partial JSON (6)
- Theme Resolution: finds saved, falls back for unknown, all 7 resolvable (9)
- Theme Switching: renderer dims change, new palette used, GuiScreen
  dimensions update (3)
- Full Workflow: save → load → resolve → create config end-to-end (1)

---

### Task 12.1 — Picture Resource Loading and Display

**Date:** 2026-09-11

#### What Was Done

Implemented picture resource loading from Blorb and wired up the three
picture opcodes in the Z-Machine interpreter:

1. **IPictureProvider** (`src/ZMachine.Core/IPictureProvider.cs`) — interface
   decoupling the interpreter engine from SkiaSharp. Exposes HasPictures,
   GetPictureSize, DrawPicture, ErasePicture, and IsPlaceholder.

2. **PictureManager** (`src/ZMachine.IO/PictureManager.cs`) — IO-layer
   implementation that decodes PNG/JPEG from Blorb via `SKBitmap.Decode()`,
   caches decoded bitmaps, and parses Rect placeholder dimensions from the
   8-byte chunk data (big-endian width + height).

3. **Opcode wiring** (`src/ZMachine.Core/ZMachine.cs`) — added EXT:5
   (@draw_picture), EXT:6 (@picture_data), and EXT:7 (@erase_picture) to
   DispatchEXT. @picture_data decodes branch for pic==0 (availability query
   writing count/release) and pic>0 (existence query writing height/width).

#### Design Decisions

- **IPictureProvider in Core, PictureManager in IO.** Core has no SkiaSharp
  dependency and must stay that way. The interface lets Core call picture
  operations through an abstraction; the IO layer provides the implementation
  with actual image decoding. The host sets `ZMachine.PictureProvider` before
  calling Run().

- **Bitmap caching.** Decoded SKBitmaps are cached by picture number to avoid
  re-decoding on repeated @draw_picture calls (common in V6 games that redraw
  scenes). The size is also cached separately since @picture_data may be
  called many times without drawing.

- **Rect placeholder behavior.** Per Blorb spec: Rect exists for @picture_data
  and @erase_picture, but @draw_picture is an error. PictureManager returns
  false for draw on Rect, and parses the 8-byte big-endian width/height for
  GetPictureSize.

- **@picture_data array layout.** For pic==0: array[0] = count, array[1] =
  release. For pic>0: array[0] = height, array[1] = width. This follows the
  Z-Machine spec where height precedes width in the output array.

#### Spec References

- Blorb "Picture Resource Chunks" — PNG, JPEG, Rect formats.
- Blorb "Placeholder Pictures" — Rect is valid for @picture_data/@erase_picture only.
- ZSpec11 "@picture_data" — pic 0 branches on availability, pic N branches on existence.
- ZSpec S15 EXT:5, EXT:6, EXT:7 — draw_picture, picture_data, erase_picture.

**Test Coverage (25 tests):**
- No Blorb: all queries return false/zero/empty (5)
- PNG Loading: has picture, dimensions, not placeholder, draw returns true,
  multiple pictures, caching (6)
- Rect Placeholders: is placeholder, dimensions, has picture, draw false,
  erase true, zero dims (6)
- Mixed: PNG + Rect together (1)
- Nonexistent: has/size/draw/placeholder all fail gracefully (4)
- IPictureProvider: implements interface, release default (2)
- Dispose: no throw after use (1)
