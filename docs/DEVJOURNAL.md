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
