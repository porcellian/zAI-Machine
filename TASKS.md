# zAI-Machine — Implementation Tasks

A phased task list for building a C# Z-Machine interpreter implementing
Z-Machine Standard 1.1, Quetzal 1.4, and Blorb 2.0.4, with selectable
vintage GUI themes.

**Notation**: "ZSpec Sx.y" = base Z-Machine Standards Document sections.
"ZSpec11" = Standard 1.1 amendments (`specs/ZSpec11.txt`).
"Quetzal Sx.y" = save format spec (`specs/savefile_14.txt`).
"Blorb" = resource format spec (`specs/blorb_format.txt`).

**Test story files**: 27×V3, 4×V4, 2×V5, 3×V6 (Arthur, Journey, Shogun
with companion PIC.DATA/CPIC.DATA files).

---

## Code Standards

The following commenting and documentation standards apply to **all**
implementation tasks across every phase.

### Code Comments

All code must include useful comments. The goal is to help a future
developer (or the original author, six months later) understand the
reasoning behind the implementation — not to restate what the code
already says through well-chosen names.

- **Public API XML doc comments**: Every public class, interface, and
  public method gets a `<summary>` doc comment describing its purpose,
  parameters, return value, and any important constraints or side effects.
- **Spec reference comments**: Where code implements a specific section
  of the Z-Machine Standard, Quetzal, or Blorb spec, include a comment
  citing the spec section. Example:
  ```csharp
  // ZSpec S4.1 — Long form: bits 6,5 encode the two operand types
  ```
- **"Why" comments**: Explain non-obvious logic, workarounds, edge cases,
  and design trade-offs. If a block of code would surprise a competent
  reader or if removing it would look safe but would break something,
  explain why it exists.
- **No noise**: Do not write comments that merely restate what the code
  does (e.g., `// increment counter` above `counter++`). If the code is
  clear, let it speak for itself.

### Development Journal

A separate development journal document must be maintained alongside the
code (see Task 14.5). This journal captures the higher-level narrative —
steps taken, decisions made, alternatives weighed — that does not belong
in code comments but is essential for understanding the project's history.

---

## Phase 1: Project Foundation

### 1.1 — Solution Structure and Build Configuration

Create the .NET 8+ solution with layered projects separating the engine
from I/O and GUI.

**Deliverables**:
- `zAI-Machine.sln` with projects:
  - `ZMachine.Core` (class library) — interpreter engine, no UI deps
  - `ZMachine.IO` (class library) — I/O abstractions and screen interfaces
  - `ZMachine.App` (executable) — GUI host application
  - `ZMachine.Tests` (xUnit) — unit and integration tests
- Interface stubs: `IScreen`, `IInputStream`, `ISoundEngine` in ZMachine.IO
- A `ConsoleScreen` placeholder implementing `IScreen`
- `.gitignore` for .NET, `Directory.Build.props` with shared settings
- Solution builds and runs (prints "zAI-Machine ready" and exits)

**Dependencies**: None

---

### 1.2 — Story File Loader and Memory Model

Load a story file into a byte array with big-endian read/write access.
Memory is divided into dynamic (writable), static (read-only at runtime),
and high (code/strings, read-only). Keep a pristine copy for restart and
Quetzal XOR compression.

**Deliverables**:
- `Memory` class:
  - `LoadStory(string path)` / `LoadStory(byte[] data)`
  - `ReadByte(int address)`, `ReadWord(int address)` (big-endian u16)
  - `WriteByte(int address, byte value)`, `WriteWord(int address, ushort value)` — writes at or above StaticBase throw
  - `DynamicBase`, `StaticBase`, `HighBase` properties from header
  - `OriginalBytes` — immutable copy of the original file
- Validation: file size vs header-declared length, version 1–8
- Padding beyond header length excluded from checksum (ZSpec11 "Padding")
- Version-specific max sizes enforced: V6/V7 = 512K (ZSpec11 "Memory layout")
- Tests: load `zork1.z3`, verify header bytes, confirm static write protection

**Spec refs**: ZSpec S1, ZSpec11 "Memory layout", ZSpec11 "Padding"
**Dependencies**: 1.1

---

### 1.3 — Header Parser and Version Detection

Parse the 64-byte header and optional header extension table. This drives
all version-conditional behavior and capability negotiation.

**Deliverables**:
- `Header` class with typed properties for all standard fields:
  - Version (byte 0), Flags 1 ($01), Flags 2 ($10)
  - High memory base ($04), initial PC ($06), dictionary ($08),
    object table ($0A), globals ($0C), static base ($0E)
  - Serial number ($12, 6 ASCII bytes), abbreviation table ($18)
  - File length ($1A, packed per version), checksum ($1C)
  - Interpreter number ($1E) and version ($1F)
  - Standard revision ($32/$33) — interpreter writes $01 $01
  - Header extension table address ($36)
- `HeaderExtension` class (if table present):
  - Word 0: word count, Word 1: Unicode table address
  - Word 2: Flags 3 (transparency bit), Words 3–4: true default colors
- Capability negotiation: set Flags 1 bits (bold, italic, fixed, timed input,
  colors); clear Flags 2 bits the interpreter can't provide
- All reserved Flags 3 bits cleared by interpreter (ZSpec11 requirement)
- Tests: parse headers from `zork1.z3` (V3), `trinity.z4` (V4),
  `sherlock.z5` (V5); verify version, serial, addresses

**Spec refs**: ZSpec S14, ZSpec11 "Header capabilities bits",
ZSpec11 "Header Extension"
**Dependencies**: 1.2

---

## Phase 2: Instruction Decoding

### 2.1 — Opcode Forms and Operand Type Decoding

Implement the instruction decoder: read bytes at PC, determine opcode
number, operand count, and operand types. Four encoding forms: long,
short, variable, extended.

**Deliverables**:
- `Instruction` struct: opcode number, form (2OP/1OP/0OP/VAR/EXT),
  operand count, operand types (LargeConstant/SmallConstant/Variable),
  raw operand values, store target, branch info
- `InstructionDecoder.Decode(Memory, int pc)` → decoded Instruction +
  next instruction address
- Long form (`0b0x`): bits 6,5 = operand types, bottom 5 = opcode
- Short form (`0b10`): bits 5,4 = operand type (or 0OP if `0b11`),
  bottom 4 = opcode
- Variable form (`0b11`): bit 5 → 2OP vs VAR, operand type byte follows
- Extended form (first byte `0xBE`, V5+): next byte = opcode, type byte(s)
- Double-variable forms (call_vs2, call_vn2): two type bytes, up to 8 operands
- Operands evaluated left-to-right (ZSpec11 "Operand evaluation")
- Tests: hand-craft byte sequences for each form, decode and verify

**Spec refs**: ZSpec S4.1–S4.4, ZSpec11 "Operand evaluation"
**Dependencies**: 1.2

---

### 2.2 — Branch and Store Result Mechanics

Decode store bytes and branch offsets that follow certain opcodes.

**Deliverables**:
- Store decoding: single byte — 0=stack push, 1–15=local, 16–255=global
- Branch decoding: bit 7 = branch-on-true/false; bit 6 set = 6-bit offset
  (0–63); bit 6 clear = 14-bit signed offset from two bytes
- Branch offset 0 = rfalse, 1 = rtrue, otherwise jump to
  `address_after_branch + offset - 2` (ZSpec11 "@jump")
- `ExecuteBranch(bool condition, BranchInfo)` helper
- `StoreResult(byte variable, ushort value)` helper
- Tests: decode various offset sizes, verify rfalse/rtrue detection

**Spec refs**: ZSpec S4.5–S4.6, ZSpec11 "@jump"
**Dependencies**: 2.1

---

### 2.3 — Packed Address Calculations

Convert packed addresses to byte addresses. The formula differs by version.

**Deliverables**:
- `AddressHelper` static class:
  - `UnpackRoutineAddress(ushort packed, int version, ushort routinesOffset)`
  - `UnpackStringAddress(ushort packed, int version, ushort stringsOffset)`
- Version rules:
  - V1–3: packed × 2
  - V4–5: packed × 4
  - V6–7: packed × 4 + (routines/stringsOffset) × 8
  - V8: packed × 8
- Tests: verify against known addresses from V3 and V4 stories

**Spec refs**: ZSpec S2.5–S2.6
**Dependencies**: 1.3

---

### 2.4 — Stack and Call Frame Model

Implement the dual stack: a call stack of frames (each with local
variables and return address) plus a per-frame evaluation stack.

**Deliverables**:
- `CallFrame` class: return PC, local variables (ushort[], up to 15),
  evaluation stack, store variable, discard-result flag, argument count
- `CallStack` class: push/pop frame, current frame access, frame count
  (needed for CATCH — Quetzal S6.2)
- Stack pointer (variable 0): push, pop, read-in-place, write-in-place
- Indirect variable references for 7 opcodes (inc, dec, inc_chk, dec_chk,
  load, store, pull): variable 0 reads/writes stack in place, does NOT
  push/pop (ZSpec11 "Indirect variable references")
- `ReadVariable(byte varNum)` and `WriteVariable(byte varNum, ushort value)`:
  dispatch to stack (0), locals (1–15), or globals (16–255)
- Global variables: word array at address from header byte $0C
- Tests: push/pop frames, read/write locals and globals, verify indirect
  stack behavior

**Spec refs**: ZSpec S5, S6.3, ZSpec11 "Indirect variable references"
**Dependencies**: 1.3, 2.1

---

## Phase 3: Text System

### 3.1 — Z-Character Decoding and Alphabet Tables

Z-Machine text is 5-bit Z-characters packed three per 16-bit word,
mapped through three alphabet tables (A0, A1, A2).

**Deliverables**:
- `TextDecoder` class:
  - `DecodeZString(Memory, int address)` → decoded string + byte length
  - Reads 16-bit words; top bit = "last word"
  - Extracts three 5-bit Z-characters per word (bits 14–10, 9–5, 4–0)
- Alphabet handling:
  - Z-chars 1–3: abbreviation references (V2: only 1; V3+: 1, 2, 3)
  - Z-chars 4, 5: shift to A1, A2 (single-shift V3+; shift-lock V1–2)
  - A2 Z-char 6: literal ZSCII (next two Z-chars form 10-bit code)
  - V1–2 shift-lock with consecutive 4/5 codes (ZSpec11 "Encoded text")
- Custom alphabet table (V5+): if header word $34 non-zero, read 78 bytes
  (3×26) as custom alphabets
- Tests: decode title string from `zork1.z3`, verify "ZORK I"

**Spec refs**: ZSpec S3.1–S3.6, S15, ZSpec11 "Encoded text"
**Dependencies**: 1.2, 1.3

---

### 3.2 — Abbreviation Table Expansion

Z-characters 1/2/3 (version-dependent) trigger abbreviation expansion.
96 entries in V3+ (32 per trigger character).

**Deliverables**:
- Abbreviation table at header byte $18
- Entry index: `(z - 1) × 32 + x` where z = trigger Z-char, x = next Z-char;
  entry is a word address; abbreviation at `word_address × 2`
- Guard against recursive abbreviation expansion (illegal per spec)
- Integration with `TextDecoder`
- Tests: decode strings from `zork1.z3` using abbreviations (common words
  like "the", "you")

**Spec refs**: ZSpec S3.3
**Dependencies**: 3.1

---

### 3.3 — ZSCII Character Set and Unicode Output

ZSCII codes 32–126 = ASCII. Codes 155–251 = "extra characters" defaulting
to Latin-1-like, overridable via a Unicode translation table in V5+.

**Deliverables**:
- `ZsciiEncoder` class:
  - `ZsciiToUnicode(int zsciiCode)` → Unicode char
  - `UnicodeToZscii(char unicodeChar)` → ZSCII code (or -1)
- Default extra characters 155–251: standard accented Latin set (ZSpec S3.8.5)
- Unicode translation table (V5+, header extension word 1): count byte
  followed by Unicode code points replacing extras 155+
- Character $27 = right-single-quote/apostrophe (ZSpec11 "Character set")
- Character $60 = left-single-quote, NOT grave accent (ZSpec11)
- Control code filtering: U+0000–U+001F and U+007F–U+009F rejected (ZSpec11 "Unicode")
- No non-BMP support (U+0000–U+FFFF only — ZSpec11 "Unicode")
- Tests: verify default table, custom table overrides, control code rejection

**Spec refs**: ZSpec S3.8, ZSpec11 "Character set", ZSpec11 "Unicode"
**Dependencies**: 1.3

---

### 3.4 — Text Encoding for Dictionary Lookup

Encode text into Z-characters for dictionary lookup (used by `@tokenise`
and `@read`).

**Deliverables**:
- `TextEncoder.EncodeForDictionary(string text, int version)` → encoded
  bytes (4 bytes / 6 Z-chars for V1–3; 6 bytes / 9 Z-chars for V4+)
- Encoding rules:
  - Try A0 first, then A1 (shift 4), then A2 (shift 5)
  - If not found: A2-shift + Z-char 6 + two 5-bit ZSCII halves
  - Pad with Z-char 5; set top bit of last word
- V1–2 special: shift-locks when next two characters share a non-A0
  alphabet (ZSpec11 "Encoded text")
- V1–2 truncation: if truncation leaves a multi-Z-char construction
  incomplete, end-bit of last word is NOT set (ZSpec11)
- Custom alphabet support (V5+)
- Tests: encode "mailbox", compare against `zork1.z3` dictionary entry

**Spec refs**: ZSpec S3.7, ZSpec11 "Encoded text"
**Dependencies**: 3.1, 3.3

---

## Phase 4: Object System

### 4.1 — Object Table and Tree Traversal

The world model is a tree of objects with parent/sibling/child pointers.
Entry size differs: V1–3 = 9 bytes (objects 1–255), V4+ = 14 bytes
(objects 1–65535).

**Deliverables**:
- `ObjectTable` class:
  - `GetParent/GetSibling/GetChild(int obj)` → object number
  - `SetParent/SetSibling/SetChild(int obj, int value)`
  - `InsertObject(int obj, int destination)` — remove from current parent,
    insert as first child of destination
  - `RemoveObject(int obj)` — unlink from parent's child chain
- V1–3: 1-byte parent/sibling/child, 4 attribute bytes, 2 property pointer bytes
- V4+: 2-byte parent/sibling/child, 6 attribute bytes, 2 property pointer bytes
- Property defaults table: 31 words (V1–3) or 63 words (V4+) before entries
- Object 0 = "nothing" (null sentinel)
- Tests: traverse object tree in `zork1.z3`, verify parent/child/sibling
  chains, verify insert/remove integrity

**Spec refs**: ZSpec S12, S12.2, S12.3
**Dependencies**: 1.2, 1.3

---

### 4.2 — Property System

Each object has a variable-length property list preceded by a short name
(Z-string). Properties are numbered, stored in descending order, terminated
by a zero size byte.

**Deliverables**:
- `ObjectTable` extensions:
  - `GetPropertyAddress(int obj, int prop)` → address (0 if absent)
  - `GetProperty(int obj, int prop)` → value (1 byte=byte, 2 bytes=word;
    absent → default from property defaults table)
  - `SetProperty(int obj, int prop, ushort value)` — 1 or 2 bytes by size
  - `GetNextProperty(int obj, int prop)` → next property number
    (prop=0 → first property)
  - `GetPropertyLength(int address)` → byte count
  - `GetPropertyLength(0)` must return 0 (ZSpec11 "@get_prop_len")
- V1–3 size byte: top 5 bits = property number, bottom 3 = data length - 1
- V4+ size: bit 7=0 → bit 6 selects 1 or 2 byte data, bits 5–0 = prop number;
  bit 7=1 → second byte follows with bits 5–0 = length (0 treated as 64)
- Short name: Z-string with length-in-words prefix byte
- Tests: read properties in `zork1.z3`, verify short name decoding,
  verify `@get_prop_len 0` returns 0

**Spec refs**: ZSpec S12.4, ZSpec11 "@get_prop_len"
**Dependencies**: 4.1, 3.1

---

### 4.3 — Attribute System

Bitfield of flags per object: 32 attributes (4 bytes) in V1–3, 48
attributes (6 bytes) in V4+. Numbered from MSB of first byte.

**Deliverables**:
- `ObjectTable` extensions:
  - `TestAttribute(int obj, int attr)` → bool
  - `SetAttribute(int obj, int attr)`
  - `ClearAttribute(int obj, int attr)`
- Attribute 0 = bit 7 of first byte, attribute 7 = bit 0, attribute 8 =
  bit 7 of second byte, etc.
- Range checking: out-of-range attributes produce a warning, not a crash
- Tests: test/set/clear attributes in `zork1.z3`

**Spec refs**: ZSpec S12.3.1
**Dependencies**: 4.1

---

## Phase 5: I/O and Screen Model

### 5.1 — Text Output Backend (Console)

Initial text output path: console-based screen for V3 status line and
basic upper/lower window model. The first playable output mode.

**Deliverables**:
- `IScreen` interface:
  - `Print(string text)`, `PrintChar(char c)`, `NewLine()`
  - `ShowStatusLine(string location, string scoreOrTime)`
  - `SplitWindow(int lines)`, `SetWindow(int window)`
  - `EraseLine()`, `EraseWindow(int window)`
  - `SetCursor(int line, int column)`
  - `SetTextStyle(int style)`, `BufferMode(bool enabled)`
- `ConsoleScreen` implementation:
  - ANSI escape codes for basic styling
  - Reverse-video status line (V1–3)
  - Upper window as fixed-position text area
  - Word wrapping for lower window in buffer mode
- Tests: verify word wrapping at 80 columns, status line formatting

**Spec refs**: ZSpec S8.1–S8.3, S8.4
**Dependencies**: 1.1

---

### 5.2 — Output Stream Management

Four output streams: 1=screen, 2=transcript file, 3=memory table,
4=player input recording. Streams 3 and 4 are V5+ only.

**Deliverables**:
- `OutputStreamManager`:
  - `SelectStream(int stream, ushort tableAddress)` — enable/disable
  - `Print(string text)` — dispatch to all active streams
  - When stream 3 is active, text goes ONLY to stream 3
- Stream 3 nesting: up to 16 levels, each with its own table address
  (ZSpec11 "@output_stream")
- Stream 3 table format: first word = character count (updated), then
  ZSCII bytes
- Stream 2: transcript file output
- Stream 4: command recording file
- Tests: stream 3 captures to memory, nesting works, stream 3 suppresses
  other output

**Spec refs**: ZSpec S7.1–S7.2, ZSpec11 "Output streams", ZSpec11 "@output_stream"
**Dependencies**: 5.1, 1.2

---

### 5.3 — Dictionary and Lexical Analysis

Parse the game's dictionary and implement the tokenization algorithm
for `@read` and `@tokenise`.

**Deliverables**:
- `Dictionary` class:
  - `Parse(Memory, int dictionaryAddress)` — read separators, entry length,
    entry count, entries
  - `Lookup(string word)` → byte address of match (0 if not found),
    binary search (entries are sorted)
- `Tokenizer` class:
  - `Tokenize(string input, Memory, int dictAddr, int parseBuffer, int textBuffer)`
  - Split on spaces and word separators; encode each word; look up in dictionary
  - Parse buffer: byte 0=max words, byte 1=count, then per word:
    address (2), length (1), text position (1)
  - Text buffer: byte 0=max chars; V1–4: null-terminated;
    V5+: byte 1=count, not null-terminated
- Custom dictionary for `@tokenise` (V5+): optional 3rd operand;
  4th operand flag for unrecognized word handling (ZSpec11 "@tokenise")
- Tests: tokenize "open mailbox" against `zork1.z3` dictionary

**Spec refs**: ZSpec S13, S15, ZSpec11 "@tokenise"
**Dependencies**: 3.4, 1.3

---

### 5.4 — Input System

Implement `@read` (line input) and `@read_char` (single keypress),
including timed input in V4+.

**Deliverables**:
- `IInputStream` interface: `ReadLine(...)`, `ReadChar(...)`
- `ConsoleInputStream` using Console.ReadKey / Console.ReadLine
- `@read` (V1–4: `sread`; V5+: `aread`):
  - V1–3: display status line first, no store, write text/parse buffers
  - V4: adds timed input
  - V5+: stores terminating character (13 for enter); text buffer format changes
  - Must return 13 for enter (ZSpec11 "@read")
  - Convert input to lowercase before tokenizing
- `@read_char`: single ZSCII key, supports timed input (V4+)
- Timed input callback mechanism (V4+, ZSpec S10.7):
  - `@read` and `@read_char` accept optional time (tenths of a second) and
    routine operands
  - When the timer expires, the callback routine is called; if it returns
    true, the input is cancelled and the opcode returns 0
  - Timer resets after each keypress; callback may print but must not
    alter input state
- Input stream management (`@input_stream`):
  - Stream 0: keyboard (default)
  - Stream 1: file playback — reads pre-recorded commands from a file
    (the counterpart of output stream 4's command recording)
  - `@input_stream 0/1` switches between keyboard and file input
  - When file input is exhausted, revert to keyboard (stream 0)
- Input character mapping: cursor/function keys → ZSCII 129–154
- Tests: mock "open mailbox", verify text buffer for V3 and V5 formats;
  timed input callback returning true cancels input; input stream file
  playback feeds commands correctly

**Spec refs**: ZSpec S10, S10.5, S10.7, S15, ZSpec11 "@read"
**Dependencies**: 5.1, 5.3

---

### 5.5 — Status Line and Window Management (V1–V5)

V1–3: status line with location/score/time. V4–5: split upper/lower
window model.

**Deliverables**:
- V1–3 status line:
  - Displayed before each `@read`
  - Object name from global 0, score from global 1, turns from global 2
  - Flags 1 bit 1 → time display (hours:minutes) instead of score/turns
- V4–5 windows:
  - `@split_window n` — upper=n lines, cursor to (1,1); n=0 unsplits
  - `@set_window 0/1` — select output window
  - Upper: no scroll, no word wrap, manual cursor positioning
  - Lower: scrolling, word wrap in buffer mode
  - `@erase_window -1` clears+unsplits; `-2` clears only; `0/1` clears that window
  - `@set_cursor line column` in upper window (1-based)
  - Implicit split if cursor moves below split (ZSpec11 "@set_cursor")
- Tests: status line with mock globals, split/unsplit behavior

**Spec refs**: ZSpec S8.4, S8.7, ZSpec11 "@set_cursor", ZSpec11 "@split_window"
**Dependencies**: 5.1, 4.1

---

## Phase 6: Full Instruction Set

### 6.1 — Arithmetic, Logical, and Comparison Opcodes

All arithmetic, bitwise, and comparison instructions. 16-bit values;
arithmetic uses signed interpretation where appropriate.

**Deliverables**:
- Arithmetic: `@add`, `@sub`, `@mul`, `@div`, `@mod` — signed 16-bit;
  division by zero = error
- Bitwise: `@and`, `@or`, `@not` (V1–4: 1OP; V5+: `@not` moves to VAR)
- Shifts: `@log_shift` (logical), `@art_shift` (arithmetic) — places must
  be in range -15 to +15 (ZSpec11 "@art_shift and @log_shift")
- Comparison: `@je` (2–4 operands, branches if first equals any;
  1 operand is illegal — ZSpec11 "@je"), `@jl` (signed <), `@jg` (signed >),
  `@jz` (branch if zero)
- `@test` — branch if all bits in op2 are set in op1
- `@random` — positive: random 1..range; negative: seed; 0: re-randomize
- Tests: signed overflow, shift boundaries, `@je` with 3+ operands

**Spec refs**: ZSpec S15, ZSpec11 "@je", ZSpec11 "@art_shift and @log_shift"
**Dependencies**: 2.1, 2.2, 2.4

---

### 6.2 — Variable, Memory, and Table Opcodes

Opcodes for variable manipulation, memory read/write, and table operations.

**Deliverables**:
- Variable opcodes:
  - `@load`/`@store` (indirect) — stack pointer reads/writes in place
    (ZSpec11 "Indirect variable references")
  - `@inc`/`@dec` (indirect, signed)
  - `@inc_chk`/`@dec_chk` (indirect, increment/decrement + branch)
  - `@push value`, `@pull var` (V1–5: indirect; V6: stores to result)
- Memory: `@loadw`, `@loadb`, `@storew`, `@storeb`
- Tables:
  - `@scan_table x table len form` — form bit 7 = words vs bytes
    (ZSpec11 "@scan_table" — bit 7, not bit 8)
  - `@copy_table first second size` — second=0 → zero table;
    negative size → copy forward (allows overlap)
- Tests: indirect stack ops, `@scan_table` word/byte, `@copy_table` overlap

**Spec refs**: ZSpec S15, ZSpec11 "Indirect variable references",
ZSpec11 "@scan_table"
**Dependencies**: 2.4, 6.1

---

### 6.3 — Object Manipulation Opcodes

All opcodes operating on the object tree, properties, and attributes.

**Deliverables**:
- Tree: `@get_parent`, `@get_child` (branch), `@get_sibling` (branch),
  `@jin` (branch if child of), `@insert_obj`, `@remove_obj`
- Properties: `@get_prop`, `@get_prop_addr`, `@get_prop_len`,
  `@get_next_prop`, `@put_prop`
- Attributes: `@test_attr` (branch), `@set_attr`, `@clear_attr`
- `@print_obj` — print object's short name
- Tests: manipulate objects in `zork1.z3`, verify tree changes

**Spec refs**: ZSpec S12, S15
**Dependencies**: 4.1, 4.2, 4.3, 3.1

---

### 6.4 — Text Output Opcodes

All opcodes that produce text output.

**Deliverables**:
- `@print` — inline Z-string after opcode; advance PC past it
- `@print_ret` — print inline Z-string + newline, return true
- `@print_addr addr`, `@print_paddr packed-addr`
- `@print_char zscii-code`, `@print_num value` (signed decimal)
- `@new_line`
- `@print_table` (V5+) — rectangular table with width/height/skip
- `@print_unicode` (EXT, V5+) — BMP only, reject control codes
- `@encode_text` (V5+) — encode ZSCII to dictionary form in memory
- All output via `OutputStreamManager`
- Tests: inline Z-string PC advancement, `@print_num` negative,
  `@print_table` layout

**Spec refs**: ZSpec S15, S3
**Dependencies**: 3.1, 3.3, 5.2

---

### 6.5 — Control Flow Opcodes

Routine calls (multiple variants), returns, and flow control.

**Deliverables**:
- Calls (store result): `@call_1s`, `@call_2s`, `@call_vs`, `@call_vs2`
- Calls (discard, V5+): `@call_1n`, `@call_2n`, `@call_vn`, `@call_vn2`
- V1–3 `@call` = `@call_vs`; calling address 0 → do nothing, store 0
- Routine header: count byte (locals); V1–4 initial values as words;
  V5+ locals init to 0
- Returns: `@ret value`, `@rtrue`, `@rfalse`, `@ret_popped`
- Control: `@jump offset` (ZSpec11 "@jump"), `@nop`, `@piracy` (branch true),
  `@quit`, `@restart`
- `@check_arg_count n` — branch if nth argument was supplied
- `@catch` → frame count; `@throw value frame-count` → unwind and return
  (Quetzal S6.1–S6.2)
- `@verify` — checksum from $40 to file length (exclude padding), branch on match
- Tests: call/return cycle, catch/throw, restart resets state,
  verify checksum against `zork1.z3`

**Spec refs**: ZSpec S5, S15, ZSpec11 "@jump", Quetzal S6.1–S6.2
**Dependencies**: 2.3, 2.4, 6.1

---

### 6.6 — V5+ Screen and Style Opcodes

Remaining opcodes for styles, fonts, colors, and screen queries.

**Deliverables**:
- `@set_text_style style` — 0=Roman, 1=Reverse, 2=Bold, 4=Italic, 8=Fixed;
  Standard 1.1 requires combinations via addition; priority order:
  Fixed > Italic > Bold > Reverse (ZSpec11 "@set_text_style")
- `@set_font font` — 1=normal, 3=character graphics, 4=fixed-pitch;
  font 2 undefined, must return 0; fonts 5–1023 reserved;
  stores previous font (ZSpec11 "@set_font")
- `@set_colour fg bg` (VAR:27) — explicit opcode implementation:
  - Colors 0=current, 1=default, 2–9=standard, 10–12=Standard 1.1 greys
    (ZSpec11 "Colour numbers")
  - -1 = under cursor (V6 only)
  - V6 adds optional 3rd operand (window number)
  - When Flags 3 (header extension) is present, the multiple-colour model
    MUST be used; interpreter number should NOT be set to "Amiga" (4)
    unless by explicit user action (ZSpec11 "Colour numbers")
- Fixed-pitch header bit (V5): monitor Flags 2 byte $10 bit 3 at runtime;
  any `@storeb`/`@storew` to that header byte could toggle fixed-pitch
  mode — the interpreter must honor this bit in V5 even though it is
  deprecated (ZSpec11 "The fixed-pitch header bit")
- V6 `@set_text_style` property 10 feedback: window property 10 must
  reflect the ACTUAL style combination in use (not just what was
  requested), so games can probe for style availability
  (ZSpec11 "@set_text_style")
- `@get_cursor array`, `@erase_line`, `@buffer_mode flag`
- `@check_unicode char` (EXT) — bit 0=can print, bit 1=can accept input
- `@save_undo` / `@restore_undo` (EXT) — single-level undo
  (stores: 0=fail, 1=saved, 2=restored)
- Tests: style combinations, font switch returns previous, color setting,
  `@set_colour` with all standard colors, fixed-pitch header bit toggle

**Spec refs**: ZSpec S8, ZSpec11 "@set_text_style", ZSpec11 "@set_font",
ZSpec11 "Colour numbers", ZSpec11 "The fixed-pitch header bit",
ZSpec11 "@set_colour"
**Dependencies**: 5.5, 6.1

---

## Phase 7: Execution Integration and First Playable

### 7.1 — Main Execution Loop and Opcode Dispatch

Wire all opcodes into a central fetch-decode-execute loop. This integrates
all prior work into a running interpreter.

**Deliverables**:
- `ZMachine` class:
  - `Load(string storyPath)` — load story, parse header, init memory/stack/screen
  - `Run()` — main loop: decode at PC, dispatch, advance
  - Opcode dispatch table mapping numbers to handler methods
- All Phase 3–6 opcodes wired into dispatch
- Initialization: V1–5 PC from header $06 as byte address;
  V6 as packed routine address to call
- Error handling: unknown opcodes produce diagnostic with address and bytes
- **Milestone**: `zork1.z3` boots, prints opening text, responds to "look"
  and "inventory"
- Integration test: scripted first 5 moves of Zork I

**Spec refs**: ZSpec S5, S6
**Dependencies**: All Phase 2–6 tasks

---

### 7.2 — Regression Test Harness

Automated testing: feed scripted input, capture output, compare against
known-good transcripts.

**Deliverables**:
- `TestHarness` class: load story, feed commands, capture output,
  turn-count limit
- `ScriptedInputStream`: returns commands from a string array
- `CaptureScreen`: collects all printed text into a StringBuilder
- Initial test scripts:
  - `zork1.z3`: "open mailbox" / "read leaflet"
  - `czech.z5`: Z-Machine V5 conformance test suite
- Scripts stored in `tests/scripts/`
- xUnit tests asserting expected output substrings

**Dependencies**: 7.1

---

## Phase 8: Developer Tools

These tools live in the **Tools** menu and are essential for development
itself — use them to verify the interpreter against story files and to
help authors debug new works. Building them early (right after the first
playable milestone) pays for itself as a troubleshooting aid for every
subsequent phase.

### 8.1 — Story File Inspector

Display parsed metadata and structural information about the loaded
story file. Accessible from Tools → Story Info.

**Deliverables**:
- **Memory Map panel**: show dynamic, static, and high memory regions
  with start/end addresses and sizes in bytes; highlight the header,
  abbreviation table, object table, global variable table, dictionary,
  and code areas
- **Header panel**: every header field decoded into human-readable form —
  version, serial, release, checksum (with computed vs stored comparison),
  file length, Flags 1/2 decoded bit-by-bit with labels, interpreter
  number/version, Standard revision, all address pointers
- **Header Extension panel** (if present): Flags 3 bits decoded,
  Unicode translation table address, true default colors
- **Interpreter Info panel**: current interpreter number, version,
  Standard revision being reported, which capability bits are set/cleared
  and why
- Display as a scrollable panel or dialog within the Avalonia window
- Read-only — no modification of live memory from this view
- Tests: load `zork1.z3`, verify inspector shows correct values for
  known header fields

**Dependencies**: 1.3, 7.1

---

### 8.2 — Object Tree Viewer

Interactive tree view of the game's object hierarchy. Accessible from
Tools → Object Tree.

**Deliverables**:
- **Tree view**: expandable/collapsible tree showing all objects by
  parent/child/sibling relationships; root objects (parent = 0) at top
  level; each node shows object number and short name
- **Object detail panel** (on selecting a node):
  - Short name (decoded Z-string)
  - Parent, sibling, child object numbers and names
  - All attributes listed with set/clear status
  - All properties listed with number, size, data (hex + interpreted),
    and address
- Live view: reflects current game state (updates after each turn or
  on manual refresh)
- Search/filter: find objects by name substring or object number
- Export: dump full object tree to a text file (for offline analysis)
- Tests: load `zork1.z3`, verify "West of House" appears as a child of
  its parent, verify attribute and property display

**Dependencies**: 4.1, 4.2, 4.3, 7.1

---

### 8.3 — Dictionary Viewer

Display the game's dictionary contents. Accessible from
Tools → Dictionary.

**Deliverables**:
- **Dictionary table**: list all entries showing:
  - Entry number and byte address
  - Encoded form (hex bytes)
  - Decoded text (the word as a string)
  - Entry data bytes (the game-specific data following each word,
    typically part-of-speech flags — displayed as raw hex)
- **Metadata**: word separator characters, entry length, entry count,
  dictionary base address
- Sortable by address, decoded text, or entry number
- Search: filter entries by substring match on decoded text
- Tests: load `zork1.z3`, verify known words ("mailbox", "open", "take")
  appear with correct decoded text

**Dependencies**: 3.1, 3.4, 5.3, 7.1

---

### 8.4 — Disassembler

Disassemble Z-Machine instructions from the story file, showing decoded
opcodes with operands, branch targets, and store destinations. Accessible
from Tools → Disassembly.

**Deliverables**:
- **Disassembly view**: scrollable listing showing for each instruction:
  - Byte address (hex)
  - Raw bytes (hex)
  - Decoded opcode mnemonic (e.g., `call_vs`, `je`, `print`)
  - Decoded operands with types (e.g., `#42` for constant, `L03` for
    local 3, `G1F` for global $1F, `SP` for stack)
  - Store target (e.g., `-> L00`)
  - Branch target (e.g., `?0A3C` or `?TRUE` / `?FALSE`)
  - Inline Z-string text (for `@print` / `@print_ret`)
- **Navigation**: jump to address, jump to routine (by packed address),
  follow branch/call targets (clickable links)
- **Routine detection**: identify routine boundaries by scanning for
  routine headers (local-count byte followed by initial values for V1–4);
  list all detected routines in a sidebar
- **Cross-references**: for each routine, list call sites (addresses
  of `call_*` instructions that reference it)
- Export: dump disassembly to a text file
- Tests: disassemble the main routine of `zork1.z3`, verify opcode
  mnemonics match known disassembly output

**Dependencies**: 2.1, 2.2, 2.3, 7.1

---

### 8.5 — Interactive Debugger (Step-Through Execution)

A step-through debugger that allows instruction-by-instruction execution
with full state inspection — the primary troubleshooting tool for both
interpreter development and story file authoring. Accessible from
Tools → Debugger.

**Deliverables**:
- **Execution controls** (toolbar or keyboard shortcuts):
  - **Step Into** (F11): execute one instruction; if it's a call, enter
    the called routine
  - **Step Over** (F10): execute one instruction; if it's a call, run
    the entire called routine and break on return
  - **Continue** (F5): run until next breakpoint or input prompt
  - **Break** (Ctrl+Break): pause execution at the current instruction
  - **Restart**: reload story file and reset all state
- **Breakpoints**:
  - Set/clear by clicking an address in the disassembly view
  - Set by address (hex) or routine packed address via dialog
  - Conditional breakpoints: break when a global variable equals a
    specific value, or when a specific opcode is reached
  - Breakpoint list panel showing all active breakpoints
- **State inspection panels** (visible while paused):
  - **Current instruction**: disassembled instruction at PC with
    operand values resolved (show actual values, not just types)
  - **Call stack**: all frames with return PC, routine address,
    argument count; clicking a frame shows its locals and eval stack
  - **Local variables**: current frame's locals (L00–L0E) with
    values as both unsigned hex and signed decimal
  - **Global variables**: scrollable table of globals (G00–GEF) with
    values; highlight recently changed values
  - **Evaluation stack**: current frame's stack contents
  - **Memory viewer**: hex dump of any memory region with ASCII sidebar;
    navigate by address; highlight dynamic vs static boundary
- **Execution trace log**: optional rolling log of the last N executed
  instructions (address + mnemonic), viewable and exportable
- **Watch expressions**: user-defined list of variable/memory reads
  that update on each step (e.g., "G00" for global 0, "[0x1234]" for
  byte at address, "object 42 parent" for object field)
- Integration: the debugger reuses the disassembly view (Task 8.4)
  with the current PC highlighted; the object tree (8.2) and dictionary
  (8.3) viewers remain accessible alongside the debugger
- Tests: step through the first 10 instructions of `zork1.z3`, verify
  PC advances correctly and variable state matches expectations

**Dependencies**: 8.4, 2.4, 7.1

---

## Phase 9: Save/Restore (Quetzal)

### 9.1 — IFF Container Format Reader/Writer

Both Quetzal and Blorb use IFF. Implement a general-purpose IFF
reader/writer.

**Deliverables**:
- `IffReader.Parse(Stream)` → `IffForm` (form type + chunk list)
- Each chunk: 4-byte type ID, length, raw data
- Handles odd-length padding bytes (Quetzal S8.4.1)
- Skips unknown chunks (Quetzal S8.9)
- Duplicate chunk handling: if more than one chunk of a type that expects
  only one (e.g., two `IFhd` chunks), use the first and ignore later
  duplicates with a warning (Quetzal S8.8)
- Nested FORM support: AIFF sound chunks have chunk type 'FORM' with
  formtype 'AIFF' inside (not a bare 'AIFF' chunk type) — the reader
  must detect nested FORMs and expose the inner formtype so resource
  type detection works correctly (Blorb "AIFF Sounds")
- `IffWriter.Write(Stream, string formType, IEnumerable<IffChunk>)` —
  writes FORM header + chunks with padding; auto-calculates FORM length
- `IffChunk` class: type, data, length, optional inner formtype for
  nested FORMs
- Tests: round-trip FORM with odd-length chunk, verify padding;
  parse nested AIFF FORM; duplicate chunk produces warning

**Spec refs**: Quetzal S8, Blorb "The IFF Format", Blorb "AIFF Sounds"
**Dependencies**: 1.1

---

### 9.2 — Quetzal Save Implementation

Write current game state to a Quetzal file (IFF FORM 'IFZS').

**Deliverables**:
- `QuetzalWriter.Save(Stream, ZMachine)` — writes valid Quetzal 1.4 file
- IFhd chunk (Quetzal S5.4): release, serial, checksum, PC
  - V1–3 PC → branch data after SAVE; V4+ PC → store byte (Quetzal S5.8)
  - IFhd before CMem/UMem and Stks
  - 13-byte odd length → pad byte (Quetzal S5.7)
- CMem chunk (Quetzal S3.2–S3.7): XOR with original + run-length encode
  (zero byte + count = count+1 zeros)
- UMem chunk (Quetzal S3.8): uncompressed fallback
- Stks chunk (Quetzal S4): frames oldest-first; each = 3-byte return PC,
  flags, store var, argument flags, eval stack count, locals, eval stack
  - Non-V6: dummy first frame, all zeros except eval stack (Quetzal S4.11)
- Optional AUTH/ANNO/IntD chunks
- Old games without checksums: if the story file header has no checksum,
  calculate one from the story file bytes when saving (Quetzal S5.5)
- `@save` opcode: V1–3 branch on success; V4+ store 1/0
- Optional prompt parameter for V5+ (ZSpec11 "@save and @restore")
- Tests: save after 3 moves of `zork1.z3`, verify valid IFF, verify IFhd

**Spec refs**: Quetzal S2–S5, S7, ZSpec11 "@save and @restore"
**Dependencies**: 9.1, 2.4, 7.1

---

### 9.3 — Quetzal Restore and Undo

Read a Quetzal file and reconstruct game state. Also implement
`@save_undo` / `@restore_undo` using in-memory snapshots.

**Deliverables**:
- `QuetzalReader.Restore(Stream, ZMachine)` — validate IFhd, restore
  memory and stacks
- IFhd validation: compare release, serial, checksum (Quetzal S5.3)
- CMem decode: XOR-decompress; short data treated as zeros (Quetzal S3.4)
  - Error handling (Quetzal S3.5): reject if decoded data is larger than
    dynamic memory; reject if encoded data ends with an incomplete run
    (zero byte without a following length byte)
- UMem decode: overwrite; length must match (Quetzal S3.6)
- Stks decode: reconstruct frames; verify dummy frame (Quetzal S4.11)
  - Stack overflow detection (Quetzal S4.8): if the restored stack dump
    exceeds the interpreter's stack size limits, report an error rather
    than crashing
- IntD chunk handling: read and preserve `IntD` (interpreter-dependent
  data) chunks from save files written by other interpreters; do not
  reject a save file for containing an IntD with a different interpreter
  ID (Quetzal S7.8–S7.17)
- PC restore: V4+ store target receives 2 ("restore just happened")
- `@restore`: V1–3 branch; V4+ restored save's store gets 2
- Undo:
  - `@save_undo` → in-memory snapshot (1=success, 0=unsupported, -1=fail)
  - `@restore_undo` → restore from snapshot (stored save_undo gets 2)
  - Single undo slot, overwritten each save_undo
- Recheck capabilities after restore (ZSpec11 "Behaviour after loading")
- Tests: save/modify/restore round-trip; CMem compression round-trip

**Spec refs**: Quetzal S3–S5, ZSpec11 "Behaviour after loading",
ZSpec11 "@save and @restore"
**Dependencies**: 9.2

---

## Phase 10: Blorb Resource Loading

### 10.1 — Blorb File Parser and Resource Index

Parse a Blorb file (IFF FORM 'IFRS'). The first chunk must be the
resource index ('RIdx').

**Deliverables**:
- `BlorbReader`:
  - `Load(Stream)` — parse IFF, verify 'IFRS', read RIdx
  - `GetResource(string usage, int number)` → chunk data
  - `GetResourceType(string usage, int number)` → chunk type
    (PNG, JPEG, AIFF, OGGV, MOD, ZCOD, etc.)
  - `HasResource(string usage, int number)`
- Resource index parsing: 4-byte count, count × 12-byte entries
  (usage, number, offset)
- Shared resource chunks: multiple resource index entries may point to
  the same chunk offset — handle without duplication or error
  (Blorb "Contents of the Resource Index Chunk")
- Data resource chunks: handle 'Data' usage entries with 'TEXT' and
  'BINA' chunk types gracefully (skip or expose via API) — the parser
  must not choke on these even if Z-code doesn't use them directly
  (Blorb "Data Resource Chunks")
- Color Palette chunk ('Plte'): parse if present; two formats — explicit
  RGB list or direct-color depth hint — indicating what colors the game
  needs (Blorb "The Color Palette Chunk")
- Deprecated chunks: gracefully skip 'SNam' (UTF-16 big-endian story
  name) found in older Blorb files (Blorb "Deprecated Chunks")
- Validation: RIdx must be first chunk; warn on duplicates
- Tests: construct minimal Blorb, parse and retrieve resources; shared
  chunks resolve correctly; unknown/deprecated chunks skipped gracefully

**Spec refs**: Blorb "Overall Structure", "Contents of the Resource Index Chunk",
"Data Resource Chunks", "The Color Palette Chunk", "Deprecated Chunks"
**Dependencies**: 9.1

---

### 10.2 — Story Loading from Blorb and Metadata

Load Z-code from the 'Exec' resource ('ZCOD' chunk type). Parse optional
metadata chunks.

**Deliverables**:
- If Exec resource 0 with type 'ZCOD' exists, extract and pass to
  `Memory.LoadStory(byte[])`
- IFhd validation: if standalone story + Blorb both provided, verify match
- Conflicting executable validation: error if Blorb contains an executable
  chunk AND a separate standalone story file was also provided; also error
  if Blorb has no executable chunk and no standalone file was given
  (Blorb "Executable Resource Chunks")
- Accept `.zblorb`/`.zlb`/`.blorb`/`.blb` extensions
- Optional chunks: IFhd (game ID), RelN (release number for `@picture_data 0`),
  Fspc (frontispiece), RDes (resource descriptions), IFmd (metadata XML),
  AUTH, `(c) `, ANNO
- Tests: load Blorb-embedded ZCOD, verify identical to raw .z file

**Spec refs**: Blorb "Executable Resource Chunks", "The Game Identifier Chunk",
"The Release Number Chunk", "The Frontispiece Chunk"
**Dependencies**: 10.1, 1.2

---

### 10.3 — Blorb Resource Discovery and Header Flag Integration

Connect resource availability to Z-Machine header capability flags.

**Deliverables**:
- On startup with Blorb, scan resource index:
  - 'Pict' resources → set Flags 1 graphics + Flags 2 pictures bits
  - 'Snd ' resources → set Flags 1 sound bit
  - No resources of a type → clear corresponding bits
- Prompt for Blorb if "game wants sound/graphics" bits set but no Blorb
  provided (ZSpec11 "Header capabilities bits")
- `BlorbReader.PictureCount`, `BlorbReader.SoundCount` properties
- Tests: verify header flags set/cleared based on Blorb contents

**Spec refs**: ZSpec11 "Header capabilities bits", Blorb "Z-Machine Compatibility Issues"
**Dependencies**: 10.2, 1.3

---

## Phase 11: GUI Framework and Vintage Themes

**Suggested framework: Avalonia UI + SkiaSharp**

Avalonia UI is the recommended GUI framework for zAI-Machine:
- Cross-platform (Windows, macOS, Linux) with native feel on each OS
- SkiaSharp integration for pixel-precise custom rendering of retro themes
- Built-in support for menu bars, dialogs, and file pickers — needed for
  the "Leafpile"-style modern windowed themes (see `examples/zork_i_modC64.jpg`)
- NuGet packages: `Avalonia`, `Avalonia.Desktop`, `Avalonia.Skia`
- The rendering canvas (`SKCanvasView`) hosts the bitmap-font character grid,
  while Avalonia provides the surrounding window chrome, menus, and input
- For pure retro themes (C64, Apple II), the canvas fills the window with
  optional CRT shader effects; for modern-retro themes (Modern C64), the
  canvas sits inside a standard windowed layout with native menu bar

Alternatives considered but not recommended:
- SDL2-CS: excellent for pixel rendering, but lacks native menus/dialogs;
  the Modern C64 windowed style would require reimplementing all UI chrome
- MAUI: not suited for pixel-precise custom rendering
- WPF: Windows-only, no macOS/Linux support

### 11.1 — Avalonia UI Project Setup and Rendering Abstraction

Set up the Avalonia UI application and build the rendering abstraction
that all themes will render through.

**Deliverables**:
- Add NuGet references to `ZMachine.App`:
  - `Avalonia` (UI framework)
  - `Avalonia.Desktop` (desktop host)
  - `Avalonia.Skia` (SkiaSharp rendering backend)
  - `SkiaSharp` (direct bitmap/canvas operations)
- `App.axaml` — Avalonia application entry point with theme resources
- `MainWindow.axaml` — primary window containing:
  - Native menu bar (File: Open Story, Open Blorb, Quit;
    Tools: Story Info, Object Tree, Dictionary, Disassembly, Debugger;
    Options: Theme submenu, CRT Effects, Sound Volume;
    Help: About zAI-Machine)
  - `SKCanvasView` (or custom `SKControl`) filling the client area
    for retro rendering
- `IRenderer` interface:
  - `Initialize(int widthPixels, int heightPixels, ThemeConfig theme)`
  - `DrawCharacter(int col, int row, char c, Color fg, Color bg, TextStyle style)`
  - `DrawRegion(int x, int y, int w, int h, Color color)`
  - `DrawImage(int x, int y, SKBitmap image, int scaledW, int scaledH)`
  - `SetCursorPosition(int col, int row)`, `Refresh()`
  - `GetScreenSize()` → (columns, rows)
- `SkiaRenderer` implementing `IRenderer`: draws to an `SKBitmap`
  back buffer, blits to canvas on `Refresh()`
- `ThemeConfig` class: font bitmap data, char width/height, color palette
  (Z-Machine colors 2–15 → RGB), border style, scanline toggle,
  CRT curvature toggle, screen margins, window chrome mode
  (borderless-fullclient vs. standard-windowed)
- `GuiScreen` implementing `IScreen`: maps Z-Machine screen ops to
  renderer calls via character grid backing store with word wrap
- Keyboard input wired from Avalonia `KeyDown`/`TextInput` events to
  `IInputStream`; mouse events wired for later V6 support
- Console backend (`ConsoleScreen`) remains as fallback for headless/test use
- Milestone: `zork1.z3` runs in an Avalonia window with a basic monospace
  font, white-on-black

**Dependencies**: 5.1, 7.1

---

### 11.2 — Bitmap Font System ✅

Vintage themes require pixel-perfect bitmap fonts — no TrueType,
no anti-aliasing. Render glyphs as SkiaSharp pixel data.

**Deliverables**:
- `BitmapFont` class:
  - Fixed char width/height
  - `GetGlyph(char c)` → `bool[]` or `byte[]` pixel bitmap
  - `RenderGlyph(SKCanvas canvas, char c, int x, int y, SKColor fg, SKColor bg)`
    — blit glyph pixels directly to SkiaSharp canvas
  - Load from embedded resources (byte arrays) or external font data files
- Built-in font sets (embedded as C# byte arrays or .bin resources):
  - C64: 8×8 pixels, PETSCII character set mapped to ZSCII
  - Apple II: 7×8 pixels
  - IBM PC: 8×8 (CGA), 8×14 (EGA), 8×16 (VGA) — CP437 glyphs
  - Amiga: 8×8 Topaz-style
- Font rendering integration: `SkiaRenderer.DrawCharacter()` uses
  `BitmapFont.RenderGlyph()` for retro themes
- ZSCII extra characters 155–251: map to closest available glyph in each
  font's character set, or render a fallback box character
- Tests: render "ZORK" with each font, verify pixel output dimensions

**Dependencies**: 11.1

---

### 11.3 — C64 Classic Theme ✅

Authentic Commodore 64 rendering, matching `examples/zork_i_c64.png`.
Full-screen retro feel — no OS window chrome visible in the rendering area.

**Deliverables**:
- `C64Theme` implementing `ITheme`:
  - 320×200 logical pixels, displayed scaled up (2× or 3×) via
    `SKBitmap` scaled blit to fill the Avalonia canvas
  - 40 columns × 25 rows character grid
  - Default colors matching the C64 screenshot:
    - Background: medium blue (#40318D)
    - Text: light blue (#7869C4)
    - Border: medium blue (#40318D), rendered as a solid margin
      around the 40×25 text area
  - Full C64 16-color palette mapped to Z-Machine colors 2–15:
    - Black, White, Red, Cyan, Purple, Green, Blue, Yellow,
      Orange, Brown, Light Red, Dark Grey, Medium Grey,
      Light Green, Light Blue, Light Grey
  - 8×8 C64 character ROM font (embedded bitmap data)
  - No OS window chrome in the rendering area — the Avalonia window
    either goes borderless or uses minimal chrome with the canvas
    filling the entire client area
- Status line: reverse-video bar at row 0 (V1–3), showing location
  left-justified and score/turns right-justified, as in the screenshot
- Blinking block cursor (C64 style, ~1 Hz toggle)
- Optional post-processing (toggleable via menu):
  - Scanline overlay (darken every other pixel row by ~30%)
  - CRT barrel distortion (slight curvature via SkiaSharp shader)
  - Phosphor bloom/glow (slight Gaussian blur on bright pixels)
- Visual reference: `examples/zork_i_c64.png`

**Dependencies**: 11.1, 11.2

---

### 11.4 — Modern C64 Theme

A modern windowed take on the C64 aesthetic, matching
`examples/zork_i_modC64.jpg`. Standard OS window chrome with menu bar
and a retro-colored text rendering area inside.

**Deliverables**:
- `ModernC64Theme` implementing `ITheme`:
  - Avalonia window with standard OS title bar (title: "zAI-Machine")
    and native menu bar (File, Tools, Help — as shown in the screenshot)
  - Rendering canvas fills the area below the menu bar
  - 80 columns × 30 rows (or dynamically sized to window) — wider than
    the classic C64's 40 columns, matching the screenshot's proportions
  - Colors matching the screenshot:
    - Background: bright blue (#0050A4 or similar — brighter and more
      saturated than the classic C64 blue)
    - Text: white or very light grey (#E0E0E0)
  - Monospace bitmap font: larger than classic C64 (e.g., 8×16 or 9×18
    to fill the modern window comfortably), but still pixel-sharp with
    no anti-aliasing — the screenshot's font is clean and legible
  - No border/margin decoration around the text area (text extends
    edge to edge within the canvas)
  - No CRT effects — this is a clean, modern-retro presentation
- Status line: not a reverse-video bar (the screenshot appears to be V5
  with no visible status line); if V1–3, render as a subtle separator
  line at the top
- Blinking underscore or block cursor
- Window is freely resizable; character grid reflows to fill available space
- Visual reference: `examples/zork_i_modC64.jpg`

**Dependencies**: 11.1, 11.2

---

### 11.5 — Apple II Theme

Classic Apple II monochrome phosphor display.

**Deliverables**:
- `AppleIITheme` implementing `ITheme`:
  - 280×192 logical pixels (40 columns × 24 rows)
  - Monochrome phosphor: green (#33FF33 on #001100) — the classic P1
    phosphor look
  - 7×8 Apple II font (embedded bitmap)
  - Borderless canvas filling the Avalonia window, scaled up 2×–3×
  - No color support: all Z-Machine colors mapped to the phosphor
    foreground color; reverse video uses swapped fg/bg
  - Status line: inverse video bar at top row
  - Non-blinking cursor (solid block, Apple II style)
  - Optional: phosphor glow/bloom effect (subtle Gaussian halo around
    bright pixels, toggleable)
- Tests: verify all output renders in single phosphor color

**Dependencies**: 11.1, 11.2

---

### 11.6 — DOS Monochrome Themes (Green Screen and Amber Screen)

Replicate old-school IBM PC monochrome monitor looks — the MDA/Hercules
green phosphor and the amber phosphor variant.

**Deliverables**:
- `DosGreenTheme` implementing `ITheme`:
  - 720×350 logical pixels (80 columns × 25 rows) — MDA resolution
  - Green phosphor: bright green (#33FF33) on dark green (#0A1A0A)
  - IBM MDA font: 9×14 pixels (or 8×14 with 1-pixel inter-character gap)
  - Borderless canvas, scaled to fill window
  - Text attributes: normal, bright (intensified green #66FF66), reverse,
    underline — mapped from Z-Machine styles (bold → bright,
    italic → underline, reverse → reverse)
  - Status line: reverse video top bar
  - Blinking block cursor
  - Optional: phosphor glow, slight scanline effect
- `DosAmberTheme` implementing `ITheme`:
  - Same layout and font as green screen
  - Amber phosphor: bright amber (#FFB000) on dark brown (#1A0F00)
  - Bright/intensified: light amber (#FFD060)
  - Same style mappings and optional effects as green screen
- Both themes share the DOS monochrome font and layout logic; only the
  color palette differs (factor out a `DosMonochromeBase` class)
- Tests: verify all output renders in the correct phosphor color,
  verify style mapping

**Dependencies**: 11.1, 11.2

---

### 11.7 — DOS CGA Color Theme

IBM PC CGA/EGA 16-color text mode — the classic DOS gaming look.

**Deliverables**:
- `DosColorTheme` implementing `ITheme`:
  - 640×200 (80 columns × 25 rows) CGA text mode, or 640×350 EGA
  - Full CGA 16-color palette → Z-Machine colors 2–15:
    - 0:Black, 1:Blue, 2:Green, 3:Cyan, 4:Red, 5:Magenta,
      6:Brown, 7:Light Grey, 8:Dark Grey, 9:Light Blue,
      10:Light Green, 11:Light Cyan, 12:Light Red,
      13:Light Magenta, 14:Yellow, 15:White
  - Font options: 8×8 CGA or 8×14 EGA (user-selectable)
  - Blinking block cursor
  - Status line: white text on blue background (top bar) — the classic
    DOS adventure game convention
  - Lower window: light grey on black (default DOS colors)
- Tests: verify all 16 colors render correctly, status line colors

**Dependencies**: 11.1, 11.2

---

### 11.8 — Amiga Theme

Amiga Workbench-inspired theme with decorative window chrome.

**Deliverables**:
- `AmigaTheme` implementing `ITheme`:
  - 640×200 (80×25) or interlaced 640×400
  - Workbench 1.x palette (blue/white/black/orange) or 2.x (grey/blue),
    user-selectable
  - 8×8 Topaz font (embedded bitmap)
  - Decorative Amiga-style window title bar with close/depth gadgets
    (rendered inside the canvas, not OS-native — purely cosmetic)
  - Z-Machine colors mapped to standard Amiga set (ZSpec11 "Colour numbers"
    — the original Amiga V6 colour set, gamma-adjusted)
  - Status line rendered in Amiga title bar style
- Tests: verify Amiga palette matches ZSpec11 colour table

**Dependencies**: 11.1, 11.2

---

### 11.9 — Theme Selection UI and Preferences

The theme picker and user preferences system.

**Deliverables**:
- Theme selection:
  - Startup dialog showing all available themes with a small preview
    screenshot/mockup for each (rendered from sample text)
  - Options → Theme submenu in the Avalonia menu bar for runtime switching
  - Runtime theme switch: reinitializes the renderer with the new theme's
    config and redraws the current screen contents
- Available themes in the selector:
  - C64 Classic, Modern C64, Apple II (Green Phosphor),
    DOS Green Screen, DOS Amber Screen, DOS CGA Color, Amiga Workbench
- User preferences persisted to JSON file (`~/.zai-machine/preferences.json`
  or platform equivalent):
  - Selected theme
  - CRT effect toggles (scanlines, curvature, phosphor glow)
  - Sound volume
  - Window size and position
  - Apple II phosphor color (green vs amber — if desired as a separate
    option rather than a separate theme)
- Preferences loaded on startup; saved on change
- Tests: verify theme switching redraws correctly, preferences round-trip

**Dependencies**: 11.3–11.8

---

## Phase 12: Graphics and Sound

### 12.1 — Picture Resource Loading and Display

Load picture resources from Blorb and render via the GUI.
Support PNG, JPEG, and Rect placeholders.

**Deliverables**:
- `PictureManager`:
  - `LoadPicture(int number)` — decode from Blorb to in-memory bitmap
  - `GetPictureSize(int number)` → (width, height)
  - PNG and JPEG decoding (via SkiaSharp or ImageSharp)
  - Rect placeholders: report size for `@picture_data` but `@draw_picture`
    is an error (Blorb "Placeholder Pictures")
- Opcodes:
  - `@picture_data pic array` — write width/height; `@picture_data 0`
    branches if pictures available, writes release/count (ZSpec11 "@picture_data")
  - `@draw_picture pic y x`
  - `@erase_picture pic y x`
  - `@picture_table table` — preload hint (can be no-op initially)
- Integration with `IRenderer.DrawImage()`
- Tests: load test PNG from Blorb, verify dimensions, Rect behavior

**Spec refs**: Blorb "Picture Resource Chunks", ZSpec11 "@picture_data"
**Dependencies**: 10.1, 11.1

---

### 12.2 — Image Scaling and Resolution System

Blorb's resolution/scaling system. Scalable images scale based on the
Elbow Room Factor (ERF).

**Deliverables**:
- Parse 'Reso' chunk: standard window size (px, py), min/max window size,
  per-image scaling entries (standard/min/max ratios)
- ERF calculation: `ERF = min(wx/px, wy/py)`
- Per-image ratio R:
  - `R = clamp(ERF × stdratio, minratio, maxratio)`
  - minratio = maxratio → fixed ratio, ERF ignored
  - No Reso entry → non-scalable, display at 1:1
- `@picture_data` reports scaled size, not raw
- Min/max window sizes hint the GUI's initial dimensions
- Tests: ERF for various window sizes, ratio clamping

**Spec refs**: Blorb "The Resolution Chunk"
**Dependencies**: 12.1

---

### 12.3 — Sound Resource Loading and Playback

Load and play sounds from Blorb: AIFF (effects), Ogg (music/effects),
MOD (music). Dual-channel model.

**Deliverables**:
- `ISoundEngine` interface:
  - `LoadSound(int number, byte[] data, string format)`
  - `PlaySound(int number, int volume, int repeats, ushort callback)`
  - `StopSound(int number)`, `StopAll()`
- Backend (NAudio, SDL2_mixer, or OpenAL):
  - AIFF playback (effects channel) — note: AIFF stored as nested IFF
    FORM with formtype 'AIFF' (handled by IFF reader, Task 9.1)
  - Ogg Vorbis (via NVorbis or similar)
  - MOD/IT/XM/S3M (tracker library)
  - SONG format: deprecated but still legal in Blorb files; recognize
    and either play (if feasible) or skip with a warning
    (Blorb "Song Sounds")
- Dual-channel model (Blorb "Z-Machine Compatibility Issues"):
  - Effects interrupt effects; music interrupts music; they do NOT
    cross-interrupt
- `@sound_effect` opcode:
  - 1=prepare, 2=play, 3=stop, 4=stop+unload
  - `@sound_effect 0 3/4` stops/unloads all (ZSpec11 "@sound_effect")
  - V5 repeats = total plays (not extra); 0 = play once + warning
  - Callback only when finished naturally (not manual stop)
- Volume 1–8 mapped to engine; 255 = loudest
- V3 looping: consult 'Loop' chunk (Blorb "The Looping Chunk")
- Tests: mock engine verifies channel assignment, callback

**Spec refs**: ZSpec S9, ZSpec11 "@sound_effect", ZSpec11 "Volume guidelines",
Blorb "Sound Resource Chunks", "The Looping Chunk"
**Dependencies**: 10.1, 11.1

---

## Phase 13: V6 and Advanced Features

### 13.1 — V6 Window System

V6 has 8 independent windows (0–7), each with position, size, cursor,
colors, font, attributes, and margins. All 8 treated identically except
defaults and `@split_window` targeting 0/1.

**Deliverables**:
- `V6Window` class with all 18 properties (ZSpec S8.8):
  - 0–1: y/x position, 2–3: height/width, 4–5: cursor y/x
  - 6–7: left/right margin, 8–9: newline interrupt/countdown
  - 10: text style, 11: colour data, 12: font, 13: font size
  - 14: attributes (wrap, scroll, transcript, buffered), 15: line count
  - 16–17: true fg/bg colour (read-only — ZSpec11 "@get_wind_prop")
- Opcodes: `@set_window`, `@get_wind_prop`, `@put_wind_prop`,
  `@move_window`, `@window_size`, `@window_style`, `@set_margins`,
  `@scroll_window`, `@mouse_window`
- V6 `@split_window`: manipulates windows 0 and 1; cursor stays in
  same absolute position (ZSpec11 "@split_window")
- Window drawing order: 0–7, lower behind higher
- Tests: multi-window properties, `@split_window` V6 behavior

**Spec refs**: ZSpec S8.8, ZSpec11 "Version 6 windows", ZSpec11 "@split_window",
ZSpec11 "@get_wind_prop"
**Dependencies**: 6.6, 11.1

---

### 13.2 — True Color and Transparency

Standard 1.1 true color via `@set_true_colour` and V6 transparency.

**Deliverables**:
- `@set_true_colour fg bg [window]` (EXT:13):
  - 15-bit sRGB: bits 14–10=blue, 9–5=green, 4–0=red
  - Magic: -1=default, -2=current, -3=under cursor (V6), -4=transparent (V6)
  - Optional window parameter in V6
  - Update window properties 16/17
- True default colors: header extension words 5–6
- Standard color↔true color equivalences (ZSpec11 "Colour numbers"):
  - 2=black($0000), 3=red($001D), 4=green($0340), 5=yellow($03BD),
    6=blue($59A0), 7=magenta($7C1F), 8=cyan($77A0), 9=white($7FFF),
    10=light grey($5AD6), 11=medium grey($4631), 12=dark grey($2D6B)
- Non-standard colour tracking in window property 11: when true colour
  or "under the cursor" is used, property 11 stores values >= 16 for
  non-standard colours; the interpreter should track the last 240
  distinct non-standard colours used (ZSpec11 "Colour numbers")
- Transparency (V6 only):
  - Background color 15 = transparent (via `@set_colour` only, NOT via
    `@set_true_colour -4` which uses a different path)
  - Flags 3 bit 0: game wants transparency; clear if unsupported
  - Transparent is only valid as BACKGROUND, not foreground — foreground
    attempt should produce a diagnostic (ZSpec11 "@set_colour")
  - Transparent bg constraints:
    - `@erase_window`, `@erase_line`, `@erase_picture` become no-ops
    - Scrolling is not permitted
    - Reverse video is not valid
    - Input prompts should be avoided
    - Text drawn without bg fill (avoid printing on top of itself —
      anti-aliasing artifacts)
- Tests: set/read true colors via properties 16/17, transparency disables
  erase, transparent foreground produces diagnostic, non-standard colour
  tracking in property 11

**Spec refs**: ZSpec11 "@set_true_colour", ZSpec11 "Colour numbers",
ZSpec11 "@set_colour", ZSpec11 "Header Extension"
**Dependencies**: 13.1, 6.6

---

### 13.3 — Mouse Input

Mouse clicks during input generate ZSCII codes. `@read_mouse` reads
current state in real time.

**Deliverables**:
- Mouse during `@read`/`@read_char`:
  - V5: all clicks → ZSCII 254
  - V6: single/first-of-double → 254; second-of-double → 253
  - Coordinates written to header $26 (y), $27 (x), relative to (1,1)
    at top-left (ZSpec11 "Mouse co-ordinates")
- `@read_mouse array` (V6):
  - Reads CURRENT position (realtime — ZSpec11 "@read_mouse")
  - Array: word 0=y, 1=x, 2=buttons (bitfield), 3=menu
  - Reports position even outside window
  - Button bit 0=primary, bit 1=secondary, etc. (ZSpec11 button table)
- `@mouse_window` integration: deliver clicks only within designated window
- GUI integration: native mouse events → Z-Machine coords + ZSCII
- Tests: simulated clicks, verify codes and coordinates

**Spec refs**: ZSpec11 "@read_mouse", ZSpec11 "Mouse clicks",
ZSpec11 "Mouse co-ordinates"
**Dependencies**: 5.4, 13.1

---

### 13.4 — Buffer Screen and Remaining EXT Opcodes

V6-specific and remaining Standard 1.1 extended opcodes.

**Deliverables**:
- `@buffer_screen mode` (EXT:29, V6):
  - 0 (default): updates visible before input
  - 1: changes only to backing store (compositing)
  - -1: force immediate update without changing mode
  - Returns old state; may be ignored (act as mode 0)
  - (ZSpec11 "@buffer_screen")
- `@set_font font window` in V6: optional window, -3 = current
  (ZSpec11 "@set_font")
- `@set_colour fg bg window` in V6: optional window parameter
- Screen redraw bit: interpreter sets after resize; game should redraw
  (ZSpec11 "Status line redraw")
- Stub implementations for any remaining EXT opcodes
- Tests: `@buffer_screen` returns previous mode, `@set_font` with window

**Spec refs**: ZSpec11 "@buffer_screen", ZSpec11 "@set_font",
ZSpec11 "Status line redraw"
**Dependencies**: 13.1, 6.6

---

### 13.5 — Adaptive Palette (Legacy V6 Games)

Support the APal chunk for Infocom V6 games (Arthur, Zork Zero) whose
pictures change colors based on previously-plotted pictures.

**Deliverables**:
- Parse 'APal' chunk (Blorb "The Adaptive Palette Chunk"):
  - List of adaptive picture resource numbers
  - Constraints: all PNGs indexed-color (type 3), indices 2–15,
    optional transparency at index 0
- Current Palette tracking (14 entries, indices 2–15):
  - Non-adaptive picture drawn → copy its PLTE to Current Palette,
    transforming through the PNG's gAMA, cHRM, and sRGB chunks to
    produce correct sRGB values (Blorb "The Adaptive Palette Chunk")
  - Adaptive picture drawn → ignore its PLTE, render with Current Palette
- Empty APal (Shogun, Journey): signals palette-changing behavior possible
- Cache invalidation: adaptive images may be stale after palette change
- Tests: draw non-adaptive (updates palette), then adaptive (uses it)

**Spec refs**: Blorb "The Adaptive Palette Chunk"
**Dependencies**: 12.1, 13.1

---

## Phase 14: Testing, Polish, and Release

### 14.1 — Czech Conformance Testing

Run `czech.z5` systematically. Fix all reported issues.

**Deliverables**:
- Run `czech.z5` to completion, capture results
- Fix all failures (opcode, text, stack, header issues)
- Automated test: scripted input, assert all tests pass
- Document any intentional deviations
- Verify Standard 1.1 header bytes $32/$33 = $01/$01

**Dependencies**: 7.2, all Phase 6

---

### 14.2 — Infocom Story File Compatibility Testing

Test all 34 story files. Each should boot, display opening text, and
handle basic commands.

**Deliverables**:
- Test matrix per story: loads, displays title, accepts input,
  basic gameplay, no crash after 20 turns
- V3 (27 files): all must boot and play
- V4 (4 files): test timed input features
- V5 (2 files): test upper window and menu features (`sherlock.z5`)
- V6 (3 files): Arthur, Journey, Shogun — require picture data,
  test V6 windows
- Bug tracker with prioritized issues
- At least one regression script per version class

**Dependencies**: 14.1, all Phase 13

---

### 14.3 — Performance Optimization and Error Handling

Profile, optimize, and harden.

**Deliverables**:
- Performance: efficient dispatch (function pointer table if needed),
  minimize bounds-check overhead, cache abbreviation expansions,
  avoid unnecessary redraws
- Error handling: clear messages with PC address and opcode for all
  illegal ops (static write, invalid opcode, stack underflow, div by zero);
  warn and continue for undefined behavior; fatal errors offer save
- Memory safety: bounds checking on all memory/array access
- Debug trace mode: optional instruction log (off by default)
- Stress test: all story files with 100 random inputs without crash

**Dependencies**: 14.2

---

### 14.4 — Documentation and Release Packaging

User-facing docs and release builds.

**Deliverables**:
- Updated `README.md`: description, features, build instructions (.NET 8+),
  usage guide, supported versions, screenshot per theme, known limitations
- `CHANGELOG.md`
- Release builds: single-file publish for Windows, macOS, Linux
- License file

**Dependencies**: 14.3

---

### 14.5 — Development Journal (PDF)

Maintain a living development journal that documents all implementation
steps, design decisions, and the reasoning behind them. This is separate
from code comments and user-facing docs — it is the engineering narrative
of the project.

**Deliverables**:
- `docs/DEVJOURNAL.md` — maintained throughout development, updated at the
  end of each phase (at minimum) with:
  - **Steps taken**: what was built, in what order, and how it connects
    to the overall architecture
  - **Design decisions**: key choices made (data structures, algorithms,
    API shapes, framework selection) with the rationale and trade-offs
  - **Alternatives considered**: options that were evaluated and rejected,
    with brief explanations of why
  - **Spec interpretation notes**: areas where the Z-Machine Standard,
    Quetzal, or Blorb specs were ambiguous or required judgment calls,
    and how those were resolved
  - **Lessons learned**: surprises, pitfalls, and insights gained during
    implementation
  - **Architecture diagrams**: visual overviews of major subsystems
    (memory model, instruction pipeline, screen/rendering stack, theme
    architecture) — can be hand-drawn, Mermaid, or any diagramming tool
- Exported as a PDF at project completion:
  - Clean formatting with table of contents, phase headings, and
    inline diagrams
  - Generated via Markdown-to-PDF tooling (e.g., `mdpdf`, `pandoc`,
    or VS Code Markdown PDF extension)
- The journal is a project artifact, committed to version control
  alongside the source code

**Dependencies**: 14.4

---

## Summary

| Phase | Tasks | Est. Days |
|-------|-------|-----------|
| 1. Project Foundation | 1.1–1.3 | 5 |
| 2. Instruction Decoding | 2.1–2.4 | 6 |
| 3. Text System | 3.1–3.4 | 7 |
| 4. Object System | 4.1–4.3 | 5 |
| 5. I/O and Screen Model | 5.1–5.5 | 10 |
| 6. Full Instruction Set | 6.1–6.6 | 10 |
| 7. Execution Integration | 7.1–7.2 | 3 |
| 8. Developer Tools | 8.1–8.5 | 10 |
| 9. Save/Restore (Quetzal) | 9.1–9.3 | 5 |
| 10. Blorb Resource Loading | 10.1–10.3 | 4 |
| 11. GUI and Vintage Themes | 11.1–11.9 | 18 |
| 12. Graphics and Sound | 12.1–12.3 | 7 |
| 13. V6 and Advanced | 13.1–13.5 | 10 |
| 14. Testing and Polish | 14.1–14.5 | 10 |
| **Total** | **56 tasks** | **~110 days** |
