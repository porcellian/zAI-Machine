namespace ZMachine.Core;

/// <summary>
/// The central Z-Machine interpreter: loads a story file, initialises all
/// subsystems, and runs the fetch-decode-execute loop. All Phase 3–6
/// opcodes are wired into a dispatch table keyed by (Form, Opcode).
/// </summary>
/// <remarks>
/// ZSpec S5 — Main execution loop.
/// ZSpec S6 — Variable/stack access during execution.
/// ZSpec S15 — Complete opcode table.
/// </remarks>
public class Interpreter
{
    private Memory _memory = new();
    private MachineState _state = null!;
    private TextDecoder _textDecoder = null!;
    private TextEncoder _textEncoder = null!;
    private ObjectTable _objectTable = null!;
    private Dictionary _dictionary = null!;
    private Tokenizer _tokenizer = null!;
    private OutputStreamManager _outputStreams = null!;
    private ReadHandler _readHandler = null!;
    private ArithmeticOps _arithmeticOps = null!;
    private ObjectOps _objectOps = null!;
    private TextOutputOps _textOutputOps = null!;
    private ControlFlowOps _controlFlowOps = null!;
    private ScreenStyleOps _screenStyleOps = null!;
    private StatusLineHandler _statusLineHandler = null!;

    private IInputStream _inputStream = null!;
    private IPictureProvider? _pictureProvider;
    private ISoundEngine? _soundEngine;
    private V6WindowManager? _v6Windows;
    private TrueColourManager? _trueColourManager;
    private int _version;
    private bool _running;
    private readonly List<string> _blorbWarnings = new();

    /// <summary>The loaded story file memory.</summary>
    public Memory Memory => _memory;

    /// <summary>The machine execution state (PC, call stack, variables).</summary>
    public MachineState State => _state;

    /// <summary>The output stream manager.</summary>
    public OutputStreamManager OutputStreams => _outputStreams;

    /// <summary>Whether the machine is currently running.</summary>
    public bool Running => _running;

    /// <summary>
    /// Picture provider for @draw_picture, @picture_data, @erase_picture.
    /// Set by the host before calling Run() when Blorb pictures are available.
    /// </summary>
    public IPictureProvider? PictureProvider
    {
        get => _pictureProvider;
        set => _pictureProvider = value;
    }

    /// <summary>
    /// Sound engine for @sound_effect opcode.
    /// Set by the host before calling Run() when Blorb sounds are available.
    /// </summary>
    /// <remarks>
    /// ZSpec S9 — sound effects. ZSpec11 "@sound_effect" — dual-channel model.
    /// </remarks>
    public ISoundEngine? SoundEngine
    {
        get => _soundEngine;
        set => _soundEngine = value;
    }

    /// <summary>
    /// V6 window manager for the 8 independent windows.
    /// Initialized automatically when a V6 story is loaded.
    /// </summary>
    /// <remarks>ZSpec S8.8 — 8 windows with 18 properties each.</remarks>
    public V6WindowManager? V6Windows => _v6Windows;

    /// <summary>
    /// True colour manager for Standard 1.1 colour operations.
    /// Available after Init(). Null for versions below 5.
    /// </summary>
    /// <remarks>ZSpec11 "@set_true_colour", "Colour numbers".</remarks>
    public TrueColourManager? TrueColours => _trueColourManager;

    /// <summary>
    /// The loaded Blorb resource file, or null if no Blorb was loaded.
    /// Available after calling any Load overload that accepts a Blorb.
    /// </summary>
    public BlorbReader? Blorb { get; private set; }

    /// <summary>
    /// Warnings generated during header flag updates, such as when the game
    /// requests graphics or sound but no Blorb resource file is loaded.
    /// ZSpec11 "Header capabilities bits".
    /// </summary>
    public IReadOnlyList<string> BlorbWarnings => _blorbWarnings;

    /// <summary>
    /// Blorb file extensions recognized for auto-detection.
    /// Blorb "File Suffixes" — .blorb, .zblorb, .blb, .zlb.
    /// </summary>
    private static readonly HashSet<string> BlorbExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".blorb", ".zblorb", ".blb", ".zlb"
    };

    /// <summary>
    /// Loads a story or Blorb file. If the path has a Blorb extension
    /// (.blorb, .zblorb, .blb, .zlb), it is parsed as a Blorb file and
    /// the ZCOD executable is extracted. Otherwise it is loaded as a
    /// raw story file.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a Blorb file has no executable resource.
    /// </exception>
    public void Load(string path, IInputStream inputStream, IScreen screen)
    {
        string ext = Path.GetExtension(path);
        if (BlorbExtensions.Contains(ext))
        {
            var blorb = BlorbReader.Load(File.ReadAllBytes(path));
            LoadFromBlorb(blorb, null, inputStream, screen);
        }
        else
        {
            _memory.LoadStory(path);
            _inputStream = inputStream;
            Init(screen);
        }
    }

    /// <summary>
    /// Loads from a byte array (for testing). Initialises all subsystems.
    /// </summary>
    public void Load(byte[] storyData, IInputStream inputStream, IScreen screen)
    {
        _memory.LoadStory(storyData);
        _inputStream = inputStream;
        Init(screen);
    }

    /// <summary>
    /// Loads a story file paired with a separate Blorb resource file.
    /// The Blorb must not contain an executable resource (use the single-path
    /// overload for Blorb files with embedded executables).
    /// </summary>
    /// <remarks>
    /// Blorb "Executable Resource Chunks" — error if both Blorb exec
    /// and standalone story are provided.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the Blorb contains an executable (conflicting with the
    /// standalone story), or if the Blorb's IFhd doesn't match the story.
    /// </exception>
    public void Load(string storyPath, string blorbPath, IInputStream inputStream, IScreen screen)
    {
        byte[] storyData = File.ReadAllBytes(storyPath);
        var blorb = BlorbReader.Load(File.ReadAllBytes(blorbPath));
        LoadFromBlorb(blorb, storyData, inputStream, screen);
    }

    /// <summary>
    /// Loads from a pre-parsed Blorb. The Blorb must contain an Exec/0
    /// resource of type 'ZCOD'.
    /// </summary>
    public void Load(BlorbReader blorb, IInputStream inputStream, IScreen screen)
    {
        LoadFromBlorb(blorb, null, inputStream, screen);
    }

    /// <summary>
    /// Loads a standalone story with a pre-parsed resource-only Blorb.
    /// The Blorb must not contain an executable resource.
    /// </summary>
    public void Load(BlorbReader blorb, byte[] storyData, IInputStream inputStream, IScreen screen)
    {
        LoadFromBlorb(blorb, storyData, inputStream, screen);
    }

    /// <summary>
    /// Core Blorb loading logic. Handles both embedded-exec and
    /// resource-only Blorb scenarios with validation.
    /// </summary>
    private void LoadFromBlorb(BlorbReader blorb, byte[]? standaloneStory,
        IInputStream inputStream, IScreen screen)
    {
        bool hasExec = blorb.HasResource(BlorbUsage.Executable, 0);

        // Blorb "Executable Resource Chunks" — conflicting executable
        if (hasExec && standaloneStory != null)
            throw new InvalidOperationException(
                "Blorb contains an executable resource and a standalone " +
                "story file was also provided. Use one or the other.");

        if (!hasExec && standaloneStory == null)
            throw new InvalidOperationException(
                "No executable: Blorb has no 'Exec' resource and no " +
                "standalone story file was provided.");

        if (hasExec)
        {
            string execType = blorb.GetResourceType(BlorbUsage.Executable, 0);
            if (execType != "ZCOD")
                throw new InvalidOperationException(
                    $"Blorb executable is '{execType}', not 'ZCOD'. " +
                    "Only Z-code executables are supported.");

            byte[] zcode = blorb.GetResource(BlorbUsage.Executable, 0);
            _memory.LoadStory(zcode);
        }
        else
        {
            _memory.LoadStory(standaloneStory!);

            // Blorb "The Game Identifier Chunk" — validate IFhd if present
            blorb.ValidateIFhd(_memory);
        }

        Blorb = blorb;
        _inputStream = inputStream;
        Init(screen);
    }

    private void Init(IScreen screen)
    {
        _version = _memory.ReadByte(0x00);
        int globalsAddr = _memory.ReadWord(0x0C);
        int abbrAddr = _memory.ReadWord(0x18);
        int dictAddr = _memory.ReadWord(0x08);
        int objTableAddr = _memory.ReadWord(0x0A);
        ushort routinesOffset = _version is 6 or 7 ? _memory.ReadWord(0x28) : (ushort)0;
        int alphabetAddr = _version >= 5 ? _memory.ReadWord(0x34) : 0;

        _state = new MachineState(_memory, globalsAddr);
        _textDecoder = new TextDecoder(_memory, _version, abbrAddr, alphabetAddr);
        _textEncoder = new TextEncoder(_version, alphabetAddr, alphabetAddr > 0 ? _memory : null);
        _objectTable = new ObjectTable(_memory, _version, objTableAddr);

        _dictionary = new Dictionary(_memory, _version, _textEncoder);
        _dictionary.Parse(dictAddr);

        _tokenizer = new Tokenizer(_version, _textEncoder);
        _readHandler = new ReadHandler(_version, _memory, _tokenizer, _dictionary);

        _outputStreams = new OutputStreamManager(_memory);
        _outputStreams.ScreenPrint = screen.Print;

        _arithmeticOps = new ArithmeticOps();
        _objectOps = new ObjectOps(_objectTable, _textDecoder);
        _textOutputOps = new TextOutputOps(_memory, _textDecoder, _textEncoder, _outputStreams, _version);
        _controlFlowOps = new ControlFlowOps(_memory, _state, _version, routinesOffset);
        _screenStyleOps = new ScreenStyleOps(_memory, _version);
        _statusLineHandler = new StatusLineHandler(_memory, _objectTable, _textDecoder);

        // ZSpec11 — parse header extension for true colours and Flags 3
        if (_version >= 5)
        {
            ushort extAddr = _memory.ReadWord(0x36);
            HeaderExtension? ext = extAddr > 0 ? new HeaderExtension(_memory, extAddr) : null;
            _trueColourManager = new TrueColourManager(_memory, _version, ext);
        }

        // Wire screen callbacks.
        _screenStyleOps.OnSetTextStyle = screen.SetTextStyle;
        _screenStyleOps.OnEraseLine = screen.EraseLine;
        _screenStyleOps.OnBufferMode = screen.BufferMode;
        _screenStyleOps.OnGetCursor = screen.GetScreenSize; // placeholder
        _screenStyleOps.OnSetFont = f => { return f; };

        // ZSpec S8.8 — V6 uses 8 independent windows
        if (_version == 6)
        {
            var (cols, rows) = screen.GetScreenSize();
            // V6 header stores pixel dimensions at 0x22/0x24 and
            // font size at 0x26/0x27; use character cells as a
            // fallback until the host writes pixel-accurate values.
            int fontW = _memory.ReadByte(0x26);
            int fontH = _memory.ReadByte(0x27);
            if (fontW == 0) fontW = 1;
            if (fontH == 0) fontH = 1;
            int screenW = cols * fontW;
            int screenH = rows * fontH;
            _v6Windows = new V6WindowManager(screenW, screenH, fontW, fontH);
        }

        // Initial PC and base frame.
        if (_version == 6)
        {
            ushort packed = _memory.ReadWord(0x06);
            _controlFlowOps.Call(packed, [], 0, 0, true, 0);
        }
        else
        {
            _state.PC = _memory.ReadWord(0x06);
            _state.CallStack.PushFrame(new CallFrame(0, 0, false, 0, 0));
        }

        UpdateHeaderFlags();

        // Mark the machine as ready to execute so both Run() (internal loop)
        // and external callers driving Step() directly see Running == true.
        _running = true;
    }

    /// <summary>
    /// Sets or clears header capability flags based on Blorb resource availability.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "Header capabilities bits" — Flags 1 advertises interpreter
    /// capabilities; Flags 2 reflects current availability (clear if no
    /// resources loaded). Also generates warnings when the game requests
    /// sound/graphics but no Blorb is loaded.
    /// </remarks>
    private void UpdateHeaderFlags()
    {
        if (_version < 4) return;

        bool hasPictures = Blorb != null && Blorb.PictureCount > 0;
        bool hasSounds = Blorb != null && Blorb.SoundCount > 0;

        // Flags 1 (byte $01) — interpreter-set capability bits
        byte flags1 = _memory.ReadByte(0x01);

        // V4+: bit 1 = picture display available
        if (hasPictures)
            flags1 |= 0x02;
        else
            flags1 = (byte)(flags1 & ~0x02);

        // V4+: bit 5 = sound effects available
        if (hasSounds)
            flags1 |= 0x20;
        else
            flags1 = (byte)(flags1 & ~0x20);

        _memory.WriteByte(0x01, flags1);

        // Flags 2 high byte ($10) — game-set request bits
        // ZSpec11: "clear if no resources loaded"
        if (_version >= 5)
        {
            byte flags2High = _memory.ReadByte(0x10);
            bool gameWantsPictures = (flags2High & 0x01) != 0;
            bool gameWantsSound = (flags2High & 0x10) != 0;

            if (!hasPictures && gameWantsPictures)
                flags2High = (byte)(flags2High & ~0x01);
            if (!hasSounds && gameWantsSound)
                flags2High = (byte)(flags2High & ~0x10);

            _memory.WriteByte(0x10, flags2High);

            // ZSpec11: prompt for Blorb if game wants resources but none loaded
            if (Blorb == null && (gameWantsPictures || gameWantsSound))
            {
                _blorbWarnings.Add(
                    "Game requests " +
                    (gameWantsPictures && gameWantsSound ? "pictures and sound" :
                     gameWantsPictures ? "pictures" : "sound") +
                    " but no Blorb resource file is loaded.");
            }
        }
    }

    /// <summary>
    /// Runs the main execution loop until @quit or an unrecoverable error.
    /// </summary>
    public void Run()
    {
        _running = true;
        while (_running)
            Step();
    }

    /// <summary>
    /// Executes a single instruction at the current PC.
    /// </summary>
    public void Step()
    {
        var inst = InstructionDecoder.Decode(_memory, _state.PC);

        switch (inst.Form)
        {
            case OpcodeForm.Op2: Dispatch2OP(ref inst); break;
            case OpcodeForm.Op1: Dispatch1OP(ref inst); break;
            case OpcodeForm.Op0: Dispatch0OP(ref inst); break;
            case OpcodeForm.Var: DispatchVAR(ref inst); break;
            case OpcodeForm.Ext: DispatchEXT(ref inst); break;
            default:
                throw new InvalidOperationException(
                    $"Unknown opcode form at ${inst.Address:X4}");
        }
    }

    /// <summary>Resolves variable-type operands to their values.</summary>
    private ushort[] ResolveOperands(ref Instruction inst)
    {
        var resolved = new ushort[inst.OperandCount];
        for (int i = 0; i < inst.OperandCount; i++)
        {
            resolved[i] = inst.OperandTypes[i] == OperandType.Variable
                ? _state.ReadVariable((byte)inst.Operands[i])
                : inst.Operands[i];
        }
        return resolved;
    }

    /// <summary>Stores a result and advances PC.</summary>
    private void StoreAndAdvance(ref Instruction inst, ushort value)
    {
        _state.StoreResult(inst.StoreVariable, value);
        _state.PC = inst.NextAddress;
    }

    /// <summary>Handles a branch condition and advances PC.</summary>
    private void BranchAndAdvance(ref Instruction inst, bool condition)
    {
        var result = MachineState.ExecuteBranch(condition, inst.Branch, inst.NextAddress);
        switch (result.Action)
        {
            case BranchAction.DontBranch:
                _state.PC = inst.NextAddress;
                break;
            case BranchAction.Jump:
                _state.PC = result.TargetAddress;
                break;
            case BranchAction.ReturnTrue:
                _controlFlowOps.ReturnTrue();
                break;
            case BranchAction.ReturnFalse:
                _controlFlowOps.ReturnFalse();
                break;
        }
    }

    /// <summary>Handles store+branch (e.g. get_child, get_sibling, scan_table).</summary>
    private void StoreBranchAndAdvance(ref Instruction inst, ushort value, bool condition)
    {
        _state.StoreResult(inst.StoreVariable, value);
        BranchAndAdvance(ref inst, condition);
    }

    #region 2OP dispatch

    private void Dispatch2OP(ref Instruction inst)
    {
        // Decode store/branch before resolving operands.
        switch (inst.Opcode)
        {
            case 1: case 2: case 3: case 4: case 5: case 6: case 7: case 10:
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
            case 8: case 9: case 15: case 16: case 17: case 18: case 19:
            case 20: case 21: case 22: case 23: case 24: case 25:
                InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
        }

        var ops = ResolveOperands(ref inst);

        switch (inst.Opcode)
        {
            case 1: // je: branch if a == any of b,c,d
                BranchAndAdvance(ref inst, ArithmeticOps.JumpEqual(ops, inst.OperandCount));
                break;
            case 2: // jl
                BranchAndAdvance(ref inst, ArithmeticOps.JumpLessThan(ops[0], ops[1]));
                break;
            case 3: // jg
                BranchAndAdvance(ref inst, ArithmeticOps.JumpGreaterThan(ops[0], ops[1]));
                break;
            case 4: // dec_chk (indirect var ref)
                BranchAndAdvance(ref inst,
                    VariableMemoryOps.DecChk(_state, (byte)inst.Operands[0], (short)ops[1]));
                break;
            case 5: // inc_chk (indirect var ref)
                BranchAndAdvance(ref inst,
                    VariableMemoryOps.IncChk(_state, (byte)inst.Operands[0], (short)ops[1]));
                break;
            case 6: // jin
                BranchAndAdvance(ref inst, _objectOps.JumpIn(ops[0], ops[1]));
                break;
            case 7: // test
                BranchAndAdvance(ref inst, ArithmeticOps.Test(ops[0], ops[1]));
                break;
            case 8: // or
                StoreAndAdvance(ref inst, ArithmeticOps.Or(ops[0], ops[1]));
                break;
            case 9: // and
                StoreAndAdvance(ref inst, ArithmeticOps.And(ops[0], ops[1]));
                break;
            case 10: // test_attr
                BranchAndAdvance(ref inst, _objectOps.TestAttr(ops[0], ops[1]));
                break;
            case 11: // set_attr
                _objectOps.SetAttr(ops[0], ops[1]);
                _state.PC = inst.NextAddress;
                break;
            case 12: // clear_attr
                _objectOps.ClearAttr(ops[0], ops[1]);
                _state.PC = inst.NextAddress;
                break;
            case 13: // store (indirect)
                VariableMemoryOps.Store(_state, (byte)inst.Operands[0], ops[1]);
                _state.PC = inst.NextAddress;
                break;
            case 14: // insert_obj
                _objectOps.InsertObj(ops[0], ops[1]);
                _state.PC = inst.NextAddress;
                break;
            case 15: // loadw
                StoreAndAdvance(ref inst, VariableMemoryOps.LoadWord(_memory, ops[0], ops[1]));
                break;
            case 16: // loadb
                StoreAndAdvance(ref inst, VariableMemoryOps.LoadByte(_memory, ops[0], ops[1]));
                break;
            case 17: // get_prop
                StoreAndAdvance(ref inst, _objectOps.GetProp(ops[0], ops[1]));
                break;
            case 18: // get_prop_addr
                StoreAndAdvance(ref inst, _objectOps.GetPropAddr(ops[0], ops[1]));
                break;
            case 19: // get_next_prop
                StoreAndAdvance(ref inst, _objectOps.GetNextProp(ops[0], ops[1]));
                break;
            case 20: // add
                StoreAndAdvance(ref inst, ArithmeticOps.Add(ops[0], ops[1]));
                break;
            case 21: // sub
                StoreAndAdvance(ref inst, ArithmeticOps.Sub(ops[0], ops[1]));
                break;
            case 22: // mul
                StoreAndAdvance(ref inst, ArithmeticOps.Mul(ops[0], ops[1]));
                break;
            case 23: // div
                StoreAndAdvance(ref inst, ArithmeticOps.Div(ops[0], ops[1]));
                break;
            case 24: // mod
                StoreAndAdvance(ref inst, ArithmeticOps.Mod(ops[0], ops[1]));
                break;
            case 25: // call_2s (V4+)
                InvokeCall(ref inst, ops, 1, store: true);
                break;
            case 26: // call_2n (V5+)
                InvokeCall(ref inst, ops, 1, store: false);
                break;
            case 27: // set_colour (V5+)
                ExecuteSetColour(ops);
                _state.PC = inst.NextAddress;
                break;
            case 28: // throw (V5+)
                _controlFlowOps.Throw(ops[0], ops[1]);
                break;
            default:
                UnknownOpcode(ref inst);
                break;
        }
    }

    #endregion

    #region 1OP dispatch

    private void Dispatch1OP(ref Instruction inst)
    {
        switch (inst.Opcode)
        {
            case 0: // jz
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
            case 1: // get_sibling (store+branch)
            case 2: // get_child (store+branch)
                InstructionDecoder.DecodeStore(_memory, ref inst);
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
            case 3: case 4: case 8: case 14:
                InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
            case 15: // call_1n (V5+) or not (V1-4, store)
                if (_version >= 5)
                    { /* no store for call_1n */ }
                else
                    InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
        }

        var ops = ResolveOperands(ref inst);

        switch (inst.Opcode)
        {
            case 0: // jz
                BranchAndAdvance(ref inst, ArithmeticOps.JumpZero(ops[0]));
                break;
            case 1: // get_sibling
            {
                var (sib, hasSib) = _objectOps.GetSibling(ops[0]);
                StoreBranchAndAdvance(ref inst, sib, hasSib);
                break;
            }
            case 2: // get_child
            {
                var (child, hasChild) = _objectOps.GetChild(ops[0]);
                StoreBranchAndAdvance(ref inst, child, hasChild);
                break;
            }
            case 3: // get_parent
                StoreAndAdvance(ref inst, _objectOps.GetParent(ops[0]));
                break;
            case 4: // get_prop_len
                StoreAndAdvance(ref inst, _objectOps.GetPropLen(ops[0]));
                break;
            case 5: // inc (indirect)
                VariableMemoryOps.Inc(_state, (byte)inst.Operands[0]);
                _state.PC = inst.NextAddress;
                break;
            case 6: // dec (indirect)
                VariableMemoryOps.Dec(_state, (byte)inst.Operands[0]);
                _state.PC = inst.NextAddress;
                break;
            case 7: // print_addr
                _textOutputOps.PrintAddr(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 8: // call_1s (V4+)
                InvokeCall(ref inst, ops, 0, store: true);
                break;
            case 9: // remove_obj
                _objectOps.RemoveObj(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 10: // print_obj
            {
                string name = _objectOps.PrintObj(ops[0]);
                _outputStreams.Print(name);
                _state.PC = inst.NextAddress;
                break;
            }
            case 11: // ret
                _controlFlowOps.Return(ops[0]);
                break;
            case 12: // jump
                _controlFlowOps.Jump((short)ops[0], inst.NextAddress);
                break;
            case 13: // print_paddr
            {
                ushort stringsOffset = _version is 6 or 7 ? _memory.ReadWord(0x2A) : (ushort)0;
                int addr = AddressHelper.UnpackStringAddress(ops[0], _version, stringsOffset);
                _textOutputOps.PrintPAddr(addr);
                _state.PC = inst.NextAddress;
                break;
            }
            case 14: // load (indirect)
                StoreAndAdvance(ref inst,
                    VariableMemoryOps.Load(_state, (byte)inst.Operands[0]));
                break;
            case 15: // call_1n (V5+) or not (V1-4)
                if (_version >= 5)
                    InvokeCall(ref inst, ops, 0, store: false);
                else
                    StoreAndAdvance(ref inst, ArithmeticOps.Not(ops[0]));
                break;
            default:
                UnknownOpcode(ref inst);
                break;
        }
    }

    #endregion

    #region 0OP dispatch

    private void Dispatch0OP(ref Instruction inst)
    {
        switch (inst.Opcode)
        {
            case 9: // catch (V5+, store) or pop (V1-4)
                if (_version >= 5)
                    InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
            case 13: // verify (branch)
            case 15: // piracy (branch)
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
        }

        switch (inst.Opcode)
        {
            case 0: // rtrue
                _controlFlowOps.ReturnTrue();
                break;
            case 1: // rfalse
                _controlFlowOps.ReturnFalse();
                break;
            case 2: // print (inline text)
            {
                int byteLen = _textOutputOps.Print(inst.NextAddress);
                _state.PC = inst.NextAddress + byteLen;
                break;
            }
            case 3: // print_ret (inline text + newline + rtrue)
            {
                int byteLen = _textOutputOps.PrintRet(inst.NextAddress);
                _state.PC = inst.NextAddress + byteLen;
                _controlFlowOps.ReturnTrue();
                break;
            }
            case 4: // nop
                _state.PC = inst.NextAddress;
                break;
            case 5: // save (V1-4 only; V5+ uses EXT)
                // Stub: always fail for now.
                if (_version <= 3)
                {
                    InstructionDecoder.DecodeBranch(_memory, ref inst);
                    BranchAndAdvance(ref inst, false);
                }
                else if (_version == 4)
                {
                    InstructionDecoder.DecodeStore(_memory, ref inst);
                    StoreAndAdvance(ref inst, 0);
                }
                else
                {
                    _state.PC = inst.NextAddress;
                }
                break;
            case 6: // restore (V1-4 only; V5+ uses EXT)
                if (_version <= 3)
                {
                    InstructionDecoder.DecodeBranch(_memory, ref inst);
                    BranchAndAdvance(ref inst, false);
                }
                else if (_version == 4)
                {
                    InstructionDecoder.DecodeStore(_memory, ref inst);
                    StoreAndAdvance(ref inst, 0);
                }
                else
                {
                    _state.PC = inst.NextAddress;
                }
                break;
            case 7: // restart
                _controlFlowOps.Restart();
                break;
            case 8: // ret_popped
                _controlFlowOps.ReturnPopped();
                break;
            case 9: // catch (V5+) or pop (V1-4)
                if (_version >= 5)
                    StoreAndAdvance(ref inst, _controlFlowOps.Catch());
                else
                {
                    _state.ReadVariable(0); // pop
                    _state.PC = inst.NextAddress;
                }
                break;
            case 10: // quit
                _running = false;
                break;
            case 11: // new_line
                _textOutputOps.NewLine();
                _state.PC = inst.NextAddress;
                break;
            case 12: // show_status (V3)
                if (_version <= 3)
                {
                    var (loc, score) = _statusLineHandler.BuildStatusLine();
                    // Show via screen if callback is available.
                }
                _state.PC = inst.NextAddress;
                break;
            case 13: // verify
                BranchAndAdvance(ref inst, _controlFlowOps.Verify());
                break;
            case 15: // piracy
                BranchAndAdvance(ref inst, _controlFlowOps.Piracy());
                break;
            default:
                UnknownOpcode(ref inst);
                break;
        }
    }

    #endregion

    #region VAR dispatch

    private void DispatchVAR(ref Instruction inst)
    {
        // Decode store/branch per opcode.
        switch (inst.Opcode)
        {
            case 0: case 7: case 12: case 22:
                InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
            case 4: // read: store in V5+
                if (_version >= 5)
                    InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
            case 23: // scan_table: store+branch
                InstructionDecoder.DecodeStore(_memory, ref inst);
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
            case 24: // not (V5+, store)
                if (_version >= 5)
                    InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
            case 31: // check_arg_count (branch)
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
        }

        var ops = ResolveOperands(ref inst);

        switch (inst.Opcode)
        {
            case 0: // call_vs / call (store)
                InvokeCall(ref inst, ops, inst.OperandCount - 1, store: true);
                break;
            case 1: // storew
                VariableMemoryOps.StoreWord(_memory, ops[0], ops[1], ops[2]);
                _state.PC = inst.NextAddress;
                break;
            case 2: // storeb
                VariableMemoryOps.StoreByte(_memory, ops[0], ops[1], (byte)ops[2]);
                _state.PC = inst.NextAddress;
                break;
            case 3: // put_prop
                _objectOps.PutProp(ops[0], ops[1], ops[2]);
                _state.PC = inst.NextAddress;
                break;
            case 4: // read / aread
                ExecuteRead(ref inst, ops);
                break;
            case 5: // print_char
                _textOutputOps.PrintChar(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 6: // print_num
                _textOutputOps.PrintNum(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 7: // random
                StoreAndAdvance(ref inst, _arithmeticOps.Random(ops[0]));
                break;
            case 8: // push
                VariableMemoryOps.Push(_state, ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 9: // pull (indirect var ref)
                VariableMemoryOps.Pull(_state, (byte)inst.Operands[0]);
                _state.PC = inst.NextAddress;
                break;
            case 10: // split_window
                if (_v6Windows != null)
                {
                    int fontH = _v6Windows.Current.FontSize >> 8;
                    if (fontH == 0) fontH = 1;
                    _v6Windows.SplitWindow(ops[0], fontH);
                }
                _state.PC = inst.NextAddress;
                break;
            case 11: // set_window
                if (_v6Windows != null)
                    _v6Windows.SetWindow(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 12: // call_vs2 (store)
                InvokeCall(ref inst, ops, inst.OperandCount - 1, store: true);
                break;
            case 13: // erase_window
                _state.PC = inst.NextAddress;
                break;
            case 14: // erase_line
                _screenStyleOps.EraseLine(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 15: // set_cursor
                _state.PC = inst.NextAddress;
                break;
            case 16: // get_cursor
                _screenStyleOps.GetCursor(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 17: // set_text_style
                _screenStyleOps.SetTextStyle(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 18: // buffer_mode
                _screenStyleOps.BufferMode(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 19: // output_stream
            {
                short stream = (short)ops[0];
                int tableAddr = inst.OperandCount >= 2 ? ops[1] : 0;
                _outputStreams.SelectStream(stream, tableAddr);
                _state.PC = inst.NextAddress;
                break;
            }
            case 20: // input_stream
                _state.PC = inst.NextAddress;
                break;
            case 21: // sound_effect
                ExecuteSoundEffect(ref inst, ops);
                break;
            case 22: // read_char (store)
            {
                int ch = _inputStream.ReadChar();
                StoreAndAdvance(ref inst, (ushort)ch);
                break;
            }
            case 23: // scan_table (store+branch)
            {
                ushort form = inst.OperandCount >= 4 ? ops[3] : (ushort)0x82;
                var (addr, found) = VariableMemoryOps.ScanTable(
                    _memory, ops[0], ops[1], ops[2], form);
                StoreBranchAndAdvance(ref inst, addr, found);
                break;
            }
            case 24: // not (V5+)
                if (_version >= 5)
                    StoreAndAdvance(ref inst, ArithmeticOps.Not(ops[0]));
                else
                    _state.PC = inst.NextAddress;
                break;
            case 25: // call_vn (V5+)
                InvokeCall(ref inst, ops, inst.OperandCount - 1, store: false);
                break;
            case 26: // call_vn2 (V5+)
                InvokeCall(ref inst, ops, inst.OperandCount - 1, store: false);
                break;
            case 27: // tokenise (V5+)
                ExecuteTokenise(ops, inst.OperandCount);
                _state.PC = inst.NextAddress;
                break;
            case 28: // encode_text (V5+)
                _textOutputOps.EncodeText(ops[0], ops[1], ops[2], ops[3]);
                _state.PC = inst.NextAddress;
                break;
            case 29: // copy_table (V5+)
                VariableMemoryOps.CopyTable(_memory, ops[0], ops[1], (short)ops[2]);
                _state.PC = inst.NextAddress;
                break;
            case 30: // print_table (V5+)
            {
                ushort height = inst.OperandCount >= 3 ? ops[2] : (ushort)1;
                ushort skip = inst.OperandCount >= 4 ? ops[3] : (ushort)0;
                _textOutputOps.PrintTable(ops[0], ops[1], height, skip);
                _state.PC = inst.NextAddress;
                break;
            }
            case 31: // check_arg_count (branch)
                BranchAndAdvance(ref inst, _controlFlowOps.CheckArgCount(ops[0]));
                break;
            default:
                UnknownOpcode(ref inst);
                break;
        }
    }

    #endregion

    #region EXT dispatch

    private void DispatchEXT(ref Instruction inst)
    {
        switch (inst.Opcode)
        {
            case 0: case 1: case 2: case 3: case 4: case 9: case 10: case 12:
            case 19: // get_wind_prop (store)
                InstructionDecoder.DecodeStore(_memory, ref inst);
                break;
            case 6: // picture_data — branch only (no store)
                InstructionDecoder.DecodeBranch(_memory, ref inst);
                break;
        }

        var ops = ResolveOperands(ref inst);

        switch (inst.Opcode)
        {
            case 0: // save (store) — stub
                StoreAndAdvance(ref inst, 0);
                break;
            case 1: // restore (store) — stub
                StoreAndAdvance(ref inst, 0);
                break;
            case 2: // log_shift
                StoreAndAdvance(ref inst, ArithmeticOps.LogShift(ops[0], (short)ops[1]));
                break;
            case 3: // art_shift
                StoreAndAdvance(ref inst, ArithmeticOps.ArtShift(ops[0], (short)ops[1]));
                break;
            case 4: // set_font
                StoreAndAdvance(ref inst, _screenStyleOps.SetFont(ops[0]));
                break;
            case 5: // draw_picture pic y x
                ExecuteDrawPicture(ops);
                _state.PC = inst.NextAddress;
                break;
            case 6: // picture_data pic array
                ExecutePictureData(ref inst, ops);
                break;
            case 7: // erase_picture pic y x
                ExecuteErasePicture(ops);
                _state.PC = inst.NextAddress;
                break;
            case 9: // save_undo
                StoreAndAdvance(ref inst, _screenStyleOps.SaveUndo());
                break;
            case 10: // restore_undo
                StoreAndAdvance(ref inst, _screenStyleOps.RestoreUndo());
                break;
            case 11: // print_unicode
                _textOutputOps.PrintUnicode(ops[0]);
                _state.PC = inst.NextAddress;
                break;
            case 12: // check_unicode
                StoreAndAdvance(ref inst, _screenStyleOps.CheckUnicode(ops[0]));
                break;
            case 13: // set_true_colour fg bg [window]
                ExecuteSetTrueColour(ops);
                _state.PC = inst.NextAddress;
                break;
            case 8: // set_margins left right window
                if (_v6Windows != null)
                {
                    int win = ops.Length > 2 ? ops[2] : _v6Windows.SelectedWindow;
                    _v6Windows.SetMargins(ops[0], ops[1], win);
                }
                _state.PC = inst.NextAddress;
                break;
            case 16: // move_window window y x
                _v6Windows?.MoveWindow(ops[0], ops[1], ops.Length > 2 ? ops[2] : 0);
                _state.PC = inst.NextAddress;
                break;
            case 17: // window_size window height width
                _v6Windows?.WindowSize(ops[0], ops[1], ops.Length > 2 ? ops[2] : 0);
                _state.PC = inst.NextAddress;
                break;
            case 18: // window_style window flags operation
                _v6Windows?.WindowStyle(ops[0], ops[1], ops.Length > 2 ? ops[2] : 0);
                _state.PC = inst.NextAddress;
                break;
            case 19: // get_wind_prop window property → result
                StoreAndAdvance(ref inst,
                    (ushort)(_v6Windows?.GetWindProp(ops[0], ops[1]) ?? 0));
                break;
            case 20: // put_wind_prop window property value
                _v6Windows?.PutWindProp(ops[0], ops[1], ops[2]);
                _state.PC = inst.NextAddress;
                break;
            case 21: // scroll_window window pixels
                _v6Windows?.ScrollWindow(ops[0], (short)ops[1]);
                _state.PC = inst.NextAddress;
                break;
            case 22: // mouse_window window
                _v6Windows?.SetMouseWindow((short)ops[0]);
                _state.PC = inst.NextAddress;
                break;
            default:
                _state.PC = inst.NextAddress;
                break;
        }
    }

    /// <summary>
    /// EXT:5 @draw_picture pic y x — draws a picture at (y, x).
    /// Blorb "Placeholder Pictures" — drawing a Rect is an error (no-op).
    /// </summary>
    private void ExecuteDrawPicture(ushort[] ops)
    {
        if (_pictureProvider == null) return;

        int pic = ops[0];
        int y = ops.Length > 1 ? ops[1] : 1;
        int x = ops.Length > 2 ? ops[2] : 1;
        _pictureProvider.DrawPicture(pic, y, x);
    }

    /// <summary>
    /// EXT:6 @picture_data pic array — queries picture info.
    /// If pic==0: branches if pictures available, writes (release, count) to array.
    /// If pic>0: branches if picture exists, writes (height, width) to array.
    /// ZSpec11 "@picture_data".
    /// </summary>
    private void ExecutePictureData(ref Instruction inst, ushort[] ops)
    {
        int pic = ops[0];
        int arrayAddr = ops.Length > 1 ? ops[1] : 0;

        if (pic == 0)
        {
            bool available = _pictureProvider?.HasPictures ?? false;
            if (available && arrayAddr > 0)
            {
                _memory.WriteWord(arrayAddr, (ushort)(_pictureProvider!.PictureCount));
                _memory.WriteWord(arrayAddr + 2, (ushort)(_pictureProvider.ReleaseNumber));
            }
            BranchAndAdvance(ref inst, available);
        }
        else
        {
            bool exists = _pictureProvider?.HasPicture(pic) ?? false;
            if (exists && arrayAddr > 0)
            {
                var (w, h) = _pictureProvider!.GetPictureSize(pic);
                _memory.WriteWord(arrayAddr, (ushort)h);
                _memory.WriteWord(arrayAddr + 2, (ushort)w);
            }
            BranchAndAdvance(ref inst, exists);
        }
    }

    /// <summary>
    /// EXT:7 @erase_picture pic y x — erases the area occupied by a picture.
    /// Works for both real pictures and Rect placeholders.
    /// </summary>
    private void ExecuteErasePicture(ushort[] ops)
    {
        if (_pictureProvider == null) return;

        int pic = ops[0];
        int y = ops.Length > 1 ? ops[1] : 1;
        int x = ops.Length > 2 ? ops[2] : 1;
        _pictureProvider.ErasePicture(pic, y, x);
    }

    /// <summary>
    /// 2OP:27 <c>@set_colour fg bg</c> — sets foreground and background colours.
    /// In V6, also updates window properties 11 (colour data) and 16/17
    /// (true colours) using the standard colour equivalences.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@set_colour" — colour 15 = transparent (V6 bg only).
    /// Transparent foreground produces a diagnostic.
    /// </remarks>
    private void ExecuteSetColour(ushort[] ops)
    {
        int fg = (short)ops[0];
        int bg = (short)ops[1];

        // ZSpec11 — transparent foreground is invalid
        if (fg == 15 && _version == 6)
            _blorbWarnings.Add("Transparent foreground is not valid (ZSpec11 \"@set_colour\")");

        _screenStyleOps.SetColour(fg, bg);

        // Update V6 window true colour properties from standard equivalences
        if (_v6Windows != null && _trueColourManager != null)
        {
            var w = _v6Windows.Current;

            int resolvedFg = fg == 0 ? _screenStyleOps.ForegroundColor : fg;
            int resolvedBg = bg == 0 ? _screenStyleOps.BackgroundColor : bg;

            if (resolvedBg == 15)
                w.TrueBackground = -4;
            else if (resolvedBg >= 2 && resolvedBg <= 12)
                w.TrueBackground = TrueColourManager.GetStandardTrueColour(resolvedBg);

            if (resolvedFg >= 2 && resolvedFg <= 12)
                w.TrueForeground = TrueColourManager.GetStandardTrueColour(resolvedFg);

            w.ColourData = (resolvedBg << 8) | resolvedFg;
        }
    }

    /// <summary>
    /// EXT:13 <c>@set_true_colour fg bg [window]</c> — sets true colours
    /// using 15-bit sRGB values. Updates V6 window properties 16/17 and
    /// the colour number in property 11.
    /// </summary>
    /// <remarks>
    /// ZSpec11 "@set_true_colour" — magic values -1 to -4.
    /// ZSpec11 "Colour numbers" — non-standard colours tracked as 16–255.
    /// </remarks>
    private void ExecuteSetTrueColour(ushort[] ops)
    {
        if (_trueColourManager == null) return;

        int fg = (short)ops[0];
        int bg = (short)ops[1];

        // V6 optional window parameter
        int targetWindow = -1;
        if (_version == 6 && ops.Length > 2)
            targetWindow = ops[2];

        string? diagnostic = _trueColourManager.SetTrueColour(fg, bg);
        if (diagnostic != null)
            _blorbWarnings.Add(diagnostic);

        // Update V6 window properties 16/17 (true colours) and 11 (colour data)
        if (_v6Windows != null)
        {
            var w = targetWindow >= 0
                ? _v6Windows.GetWindow(targetWindow)
                : _v6Windows.Current;

            w.TrueForeground = _trueColourManager.TrueForeground;
            w.TrueBackground = _trueColourManager.TrueBackground;

            int fgNum = _trueColourManager.TrueColourToNumber(
                _trueColourManager.TrueForeground);
            int bgNum = _trueColourManager.TrueColourToNumber(
                _trueColourManager.TrueBackground);
            w.ColourData = (bgNum << 8) | fgNum;
        }

        // Also update ScreenStyleOps colour state
        int fgColour = _trueColourManager.TrueColourToNumber(
            _trueColourManager.TrueForeground);
        int bgColour = _trueColourManager.TrueColourToNumber(
            _trueColourManager.TrueBackground);
        _screenStyleOps.SetColour(fgColour, bgColour);
    }

    /// <summary>
    /// ZSpec S9, ZSpec11 "@sound_effect" — VAR:245 21.
    /// Op 1: sound number. Op 2: action (1=prepare, 2=play, 3=stop, 4=unload).
    /// Op 3: volume (low byte) + repeats (high byte).
    /// Op 4: callback routine address.
    /// </summary>
    private void ExecuteSoundEffect(ref Instruction inst, ushort[] ops)
    {
        _state.PC = inst.NextAddress;

        if (_soundEngine == null) return;

        int number = ops.Length > 0 ? ops[0] : 0;
        int action = ops.Length > 1 ? ops[1] : 2;

        switch (action)
        {
            case 1: // prepare
                _soundEngine.PrepareSound(number);
                break;
            case 2: // play
            {
                int volumeRepeats = ops.Length > 2 ? ops[2] : 0x0008;
                int volume = volumeRepeats & 0xFF;
                int repeats = (volumeRepeats >> 8) & 0xFF;
                ushort callback = ops.Length > 3 ? ops[3] : (ushort)0;

                _soundEngine.PlaySound(number, volume, repeats, callback);
                break;
            }
            case 3: // stop
                _soundEngine.StopSound(number);
                break;
            case 4: // stop + unload
                _soundEngine.UnloadSound(number);
                break;
        }
    }

    #endregion

    #region Complex opcode helpers

    /// <summary>
    /// Dispatches a call instruction. The first operand is the packed
    /// routine address; remaining operands are arguments.
    /// </summary>
    private void InvokeCall(ref Instruction inst, ushort[] ops, int argCount, bool store)
    {
        ushort packedAddr = ops[0];

        if (store)
        {
            // Store-variant calls read the store byte from the instruction.
            if (!inst.HasStore)
                InstructionDecoder.DecodeStore(_memory, ref inst);
        }

        var args = new ushort[argCount];
        for (int i = 0; i < argCount; i++)
            args[i] = ops[i + 1];

        bool called = _controlFlowOps.Call(
            packedAddr, args, argCount,
            store ? inst.StoreVariable : (byte)0,
            !store,
            inst.NextAddress);

        if (!called)
        {
            // Address 0: store 0 (for store variants) and advance.
            if (store)
                _state.StoreResult(inst.StoreVariable, 0);
            _state.PC = inst.NextAddress;
        }
    }

    /// <summary>
    /// Executes the @read/@aread opcode. Shows status line in V1-3,
    /// reads input, processes text/parse buffers.
    /// </summary>
    private void ExecuteRead(ref Instruction inst, ushort[] ops)
    {
        // V1-3: show status line before reading.
        if (_version <= 3)
        {
            var (loc, score) = _statusLineHandler.BuildStatusLine();
        }

        int maxLen = _memory.ReadByte(ops[0]);
        var (text, _) = _inputStream.ReadLine(maxLen);

        ushort parseBuffer = inst.OperandCount >= 2 ? ops[1] : (ushort)0;
        int termChar = _readHandler.ProcessRead(text, ops[0], parseBuffer);

        if (_version >= 5)
            StoreAndAdvance(ref inst, (ushort)termChar);
        else
            _state.PC = inst.NextAddress;
    }

    /// <summary>
    /// Executes the @tokenise opcode (V5+).
    /// </summary>
    private void ExecuteTokenise(ushort[] ops, int operandCount)
    {
        int textBuf = ops[0];
        int parseBuf = ops[1];

        // Read the text from the text buffer (V5 format: byte 1 = count, text at byte 2).
        int count = _memory.ReadByte(textBuf + 1);
        var chars = new char[count];
        for (int i = 0; i < count; i++)
            chars[i] = (char)_memory.ReadByte(textBuf + 2 + i);
        string text = new(chars);

        int dictAddr = operandCount >= 3 && ops[2] != 0
            ? ops[2]
            : _memory.ReadWord(0x08);

        var dict = _dictionary;
        if (operandCount >= 3 && ops[2] != 0)
        {
            dict = new Dictionary(_memory, _version, _textEncoder);
            dict.Parse(ops[2]);
        }

        bool flag = operandCount >= 4 && ops[3] != 0;

        _tokenizer.Tokenize(text, dict, _memory, parseBuf, textBufferOffset: 2);
    }

    private void UnknownOpcode(ref Instruction inst)
    {
        string formName = inst.IsExtended ? "EXT" : inst.Form.ToString();
        throw new InvalidOperationException(
            $"Unknown opcode {formName}:{inst.Opcode} at ${inst.Address:X4}");
    }

    #endregion
}
