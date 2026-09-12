# Changelog

All notable changes to zAI-Machine are documented here.

## [Unreleased]

### Phase 14: Testing, Polish, and Release

#### 14.4 — Documentation and Release Packaging
- Comprehensive README with features, build instructions, usage guide,
  theme table, project structure, known limitations
- CHANGELOG.md documenting all phases
- LICENSE file (MIT)
- Release build instructions for Windows, macOS, Linux

#### 14.3 — Performance Optimization and Error Handling
- Abbreviation cache in TextDecoder (96-slot, cleared on restart)
- `ZMachineException` with PC address and opcode context for all errors
- EXT unknown opcodes now throw instead of silently advancing
- Address overflow guards in VariableMemoryOps
- Optional `TraceWriter` callback and `InstructionCount` on Interpreter
- Stress tests: 5 stories × 100 random inputs (10 new tests)

#### 14.2 — Infocom Story File Compatibility Testing (partial)
- Test harness for Infocom story files (minizork, zork1, ballyhoo,
  mind, sherlock, Journey)
- `SkippableFact`/`SkippableTheory` for proper skip reporting when
  gitignored stories are absent
- V6 Journey crash-without-Blorb documented and tested

#### 14.1 — Czech Conformance Testing
- Fixed all 29 indirect variable opcode failures (used resolved
  operand values instead of raw instruction operands)
- Added `Header.ConfigureInterpreter()` call in Init() for interpreter
  identity, capabilities, and screen dimensions
- Czech 0.8: 406 tests + 19 print checks, 0 failures

### Phase 13: Standard 1.1 Extensions

#### 13.5 — Adaptive Palette
- `AdaptivePaletteManager` with 16-entry current palette
- Blorb APal chunk parsing with adaptive picture set tracking
- Palette version counter for render cache invalidation

#### 13.4 — Buffer Screen and Remaining EXT Opcodes
- `@buffer_screen` (EXT:29) — mode tracking with old-mode return
- Screen redraw bit (Flags 2 bit 2) via `RequestScreenRedraw()`
- V6 optional window parameter for `@set_font` and `@set_colour`
- EXT mnemonic table: `make_menu`, `picture_table`, `buffer_screen`

#### 13.3 — Mouse Input
- `MouseState` class with position, button state, menu tracking
- `@read_mouse` writes to 4-word array
- Mouse click coordinates written to header on terminating char 254/253
- `@mouse_window` restricts click reporting to a specific V6 window

#### 13.2 — True Colour and Transparency
- `TrueColourManager` for 15-bit sRGB true colour state
- `@set_true_colour` (EXT:13) with magic values (-1 to -4)
- V6 window properties 16/17 (true fg/bg) synchronised
- Standard colour ↔ true colour equivalence table

#### 13.1 — V6 Window System
- `V6WindowManager` with 8 independent windows (18 properties each)
- All V6 EXT opcodes: `move_window`, `window_size`, `window_style`,
  `get_wind_prop`, `put_wind_prop`, `scroll_window`, `set_margins`
- `SplitWindow`/`SetWindow` for V6 coordinate-based windowing

### Phase 12: Graphics and Sound

#### 12.3 — Sound Resource Playback
- `ISoundEngine` interface: prepare, play, stop, unload
- `SoundManager` with Blorb sound resource lookup
- `@sound_effect` opcode (VAR:21) — all 4 actions, volume/repeat
  decoding, callback routine address

#### 12.2 — Image Scaling and Resolution
- `ImageScaling` with aspect-ratio-preserving algorithms
- Blorb `Reso` chunk parsing for per-picture scaling ratios
- Resolution-independent coordinate mapping

#### 12.1 — Picture Resource Loading and Display
- `IPictureProvider` interface for draw/erase/query
- `PictureManager` backed by `BlorbReader`
- `@draw_picture`, `@picture_data`, `@erase_picture` opcodes
- Blorb Rect placeholder handling

### Phase 11: GUI Framework and Vintage Themes

#### 11.9 — Theme Selection UI and Preferences
- Theme selection menu in Avalonia GUI
- `UserPreferences` with JSON persistence
- `ThemeRegistry` for theme enumeration and lookup

#### 11.8 — Amiga Theme
- Amiga Workbench blue/white/orange palette
- Topaz-inspired bitmap font

#### 11.7 — DOS CGA Color Theme
- EGA 16-colour palette, 80×25 text mode
- CGA/EGA-era bitmap font

#### 11.6 — DOS Monochrome Themes
- `DosMonochromeTheme` abstract base with phosphor colour
- DOS Green (P1 phosphor) and DOS Amber (P3 phosphor)
- MDA-style attribute mapping

#### 11.5 — Apple II Theme
- Green phosphor on black, 40-column monospaced
- Apple II-inspired bitmap font

#### 11.4 — Modern C64 Theme
- Updated C64 palette with improved contrast/readability
- Same PETSCII-style font as C64 Classic

#### 11.3 — C64 Classic Theme
- Commodore 64 blue-on-blue colour scheme
- PETSCII-style 8×8 bitmap font
- Authentic CRT-era proportions

#### 11.2 — Bitmap Font System
- `BitmapFont` and `BuiltInFont` for pixel-precise glyph rendering
- `FontData` binary format for embedded font bitmaps
- SkiaSharp glyph blitting to back buffer

#### 11.1 — Avalonia UI Project Setup
- Avalonia 12.1.2 with SkiaSharp 4.151.2
- `SkiaCanvasControl` for back-buffer rendering
- `IRenderer` abstraction between themes and display

### Phase 10: Blorb Resource Loading

#### 10.3 — Blorb Resource Discovery and Header Flag Integration
- Header Flags 1/2 updates based on Blorb picture/sound availability
- Blorb warnings when game requests resources without loaded Blorb
- `IFhd` validation against loaded story

#### 10.2 — Story Loading from Blorb and Metadata
- `BlorbReader.Load()` with ZCOD extraction
- Combined Blorb+standalone story loading
- Metadata chunk parsing (IFhd, IFmd, Fspc)

#### 10.1 — Blorb File Parser and Resource Index
- IFF-based Blorb parser with RIdx resource index
- Support for Pict, Snd, Exec usage types
- PNG and JPEG picture resource extraction

### Phase 9: Save/Restore (Quetzal)

#### 9.3 — Quetzal Restore and Undo
- `QuetzalReader` — IFhd validation, CMem/UMem decompression,
  Stks frame reconstruction
- Undo stub (`@save_undo`/`@restore_undo`)

#### 9.2 — Quetzal Save
- `QuetzalWriter` — IFhd, CMem XOR compression, Stks serialisation
- Full call stack round-trip

#### 9.1 — IFF Container Format
- `IffReader` and `IffWriter` for FORM-based IFF containers
- Shared infrastructure for both Quetzal (IFZS) and Blorb (IFRS)

### Phase 8: Developer Tools

#### 8.5 — Interactive Debugger
- Step into/over/continue with instruction limit
- Address, conditional (global == value), and opcode breakpoints
- Watch expressions (globals, locals, stack top, memory)
- Execution trace log with export
- Call stack, locals, eval stack, and memory dump inspection

#### 8.4 — Disassembler
- Full instruction disassembly with mnemonic lookup
- Operand formatting (constants, variables, branch targets)
- Address range and single-instruction disassembly

#### 8.3 — Dictionary Viewer
- Dictionary entry enumeration with decoded text
- Lookup by encoded word

#### 8.2 — Object Tree Viewer
- Hierarchical object tree display
- Attribute and property listing per object

#### 8.1 — Story File Inspector
- Header field display with version-specific interpretation
- Memory map visualisation (dynamic/static/high regions)

### Phase 7: Execution Integration

#### 7.2 — Regression Test Harness
- `TestHarness` with `CaptureScreen` and `ScriptedInputStream`
- Instruction limit safety for automated runs

#### 7.1 — Main Execution Loop
- `Interpreter.Run()` and `Step()` driving fetch-decode-execute
- Four-form dispatch (2OP, 1OP, 0OP, VAR) plus EXT

### Phase 6: Full Instruction Set

#### 6.6 — V5+ Screen and Style Opcodes
- `@set_text_style`, `@set_colour`, `@erase_line`, `@buffer_mode`
- `@set_font`, `@get_cursor`, `@set_cursor`

#### 6.5 — Control Flow Opcodes
- `@call` (all variants), `@ret`, `@rtrue`, `@rfalse`, `@ret_popped`
- `@jump`, `@catch`/`@throw`, `@check_arg_count`
- `@quit`, `@restart`, `@verify`, `@piracy`, `@nop`

#### 6.4 — Text Output Opcodes
- `@print`, `@print_ret`, `@print_addr`, `@print_paddr`
- `@print_num`, `@print_char`, `@print_obj`
- `@print_unicode`, `@encode_text`, `@print_table`

#### 6.3 — Object Manipulation Opcodes
- `@test_attr`, `@set_attr`, `@clear_attr`
- `@get_parent`, `@get_child`, `@get_sibling`, `@jin`
- `@insert_obj`, `@remove_obj`
- `@get_prop`, `@get_prop_addr`, `@get_next_prop`, `@put_prop`,
  `@get_prop_len`

#### 6.2 — Variable, Memory, and Table Opcodes
- `@load`, `@store`, `@inc`, `@dec`, `@inc_chk`, `@dec_chk`
  (all with indirect variable semantics)
- `@push`, `@pull`, `@loadw`, `@loadb`, `@storew`, `@storeb`
- `@copy_table`, `@scan_table`

#### 6.1 — Arithmetic, Logical, and Comparison Opcodes
- `@add`, `@sub`, `@mul`, `@div`, `@mod` (signed 16-bit)
- `@and`, `@or`, `@not`, `@log_shift`, `@art_shift`
- `@je`, `@jl`, `@jg`, `@jz`, `@test`
- `@random` with seeded/unseeded modes

### Phase 5: I/O and Screen Model

#### 5.5 — Status Line and Window Management
- V1–V3 status line with location and score/time
- V4–V5 split window model

#### 5.4 — Input System
- `IInputStream` with `ReadLine` and `ReadChar`
- Console and scripted input implementations

#### 5.3 — Dictionary and Lexical Analysis
- Dictionary parser with separator and entry tables
- `Tokenizer` for input tokenisation against dictionary

#### 5.2 — Output Stream Management
- Four output streams: screen, transcript, memory table, player command
- Stream selection via `@output_stream`

#### 5.1 — Text Output Backend
- `IScreen` interface with Print, SetTextStyle, GetScreenSize
- Console implementation

### Phase 4: Object System

#### 4.3 — Attribute System
- 32 attributes (V1–3) / 48 attributes (V4+)
- Bit-level test/set/clear operations

#### 4.2 — Property System
- Variable-length properties with default values
- Version-dependent property table layout (V1–3 vs V4+)

#### 4.1 — Object Table and Tree Traversal
- Parent/child/sibling tree navigation
- Object short name decoding
- Version-dependent entry sizes (9 bytes V1–3, 14 bytes V4+)

### Phase 3: Text System

#### 3.4 — Text Encoding for Dictionary Lookup
- `TextEncoder` for dictionary word encoding
- Version-dependent word lengths (6 Z-chars V1–3, 9 Z-chars V4+)

#### 3.3 — ZSCII Character Set and Unicode Output
- `ZsciiEncoder` for character-to-ZSCII mapping
- Unicode translation table support (V5+)

#### 3.2 — Abbreviation Table Expansion
- Three-bank abbreviation system (Z-chars 1–3 as triggers)
- Version-dependent trigger rules (V1: none, V2: z-char 1, V3+: 1–3)

#### 3.1 — Z-Character Decoding and Alphabet Tables
- 5-bit Z-character unpacking (3 per 16-bit word)
- Three alphabet tables (A0/A1/A2) with V1–2 shift-lock semantics
- Custom alphabet support (V5+, header word $34)
- 10-bit ZSCII literal escape in A2

### Phase 2: Instruction Decoding

#### 2.4 — Stack and Call Frame Model
- `CallStack` and `CallFrame` with per-frame evaluation stack
- Local variables (1-indexed, up to 15)

#### 2.3 — Packed Address Calculations
- `AddressHelper` with version-dependent routine/string unpacking
- V6/V7 offset calculations

#### 2.2 — Branch and Store Result Mechanics
- Branch offset decoding (1-byte and 2-byte forms)
- Special offsets 0 (rfalse) and 1 (rtrue)
- Store variable byte decoding

#### 2.1 — Opcode Forms and Operand Type Decoding
- `InstructionDecoder` for all four forms (long, short, variable, extended)
- Double-variable form for call_vs2/call_vn2
- `Instruction` struct with operand types and values

### Phase 1: Project Foundation

#### 1.3 — Header Parser and Version Detection
- `Header` class with all standard header fields
- `HeaderExtension` for Standard 1.1 extension table
- `InterpreterCapabilities` flags enum

#### 1.2 — Story File Loader and Memory Model
- `Memory` class with dynamic/static/high region enforcement
- Big-endian read/write with bounds checking
- Version-dependent size limits (128K–512K)
- Pristine copy retention for restart and Quetzal

#### 1.1 — Solution Structure and Build Configuration
- Three-project solution: ZMachine.Core, ZMachine.IO, ZMachine.App
- xUnit test project
- Directory.Build.props with shared settings
