namespace ZMachine.Core;

/// <summary>
/// Splits player input into tokens (words and separators), encodes each
/// token for dictionary lookup, and writes the results to a parse buffer.
/// Used by <c>@read</c> and <c>@tokenise</c>.
/// </summary>
/// <remarks>
/// ZSpec S13 — Tokenisation: the input is split on spaces and word
/// separators. Each token is encoded via TextEncoder and looked up in
/// the dictionary. Results are written to the parse buffer.
/// ZSpec S15 — Parse buffer layout:
///   byte 0: max words (set by game)
///   byte 1: word count (set by interpreter)
///   then per word: 2-byte dict address, 1-byte length, 1-byte text position
/// </remarks>
public class Tokenizer
{
    private readonly int _version;
    private readonly TextEncoder _encoder;

    public Tokenizer(int version, TextEncoder encoder)
    {
        _version = version;
        _encoder = encoder;
    }

    /// <summary>
    /// Represents a single token extracted from input text.
    /// </summary>
    public readonly record struct Token(string Text, int Position, int Length);

    /// <summary>
    /// Splits input text into tokens based on spaces and dictionary
    /// word separators. Separator characters are emitted as their own
    /// single-character tokens.
    /// </summary>
    /// <remarks>
    /// ZSpec S13.1 — Spaces separate words but are not tokens themselves.
    /// Word separators (from the dictionary header) are tokens on their own.
    /// </remarks>
    public static List<Token> SplitIntoTokens(string input, char[] separators)
    {
        var tokens = new List<Token>();
        int i = 0;

        while (i < input.Length)
        {
            char c = input[i];

            // Spaces delimit but are not tokens.
            if (c == ' ')
            {
                i++;
                continue;
            }

            // Check if this character is a word separator.
            if (Array.IndexOf(separators, c) >= 0)
            {
                tokens.Add(new Token(c.ToString(), i, 1));
                i++;
                continue;
            }

            // Accumulate a word until space, separator, or end.
            int start = i;
            while (i < input.Length
                && input[i] != ' '
                && Array.IndexOf(separators, input[i]) < 0)
            {
                i++;
            }

            tokens.Add(new Token(input[start..i], start, i - start));
        }

        return tokens;
    }

    /// <summary>
    /// Tokenizes input and writes results to the parse buffer, looking
    /// up each token in the given dictionary.
    /// </summary>
    /// <param name="input">The player's input text (already lowercased).</param>
    /// <param name="dictionary">The parsed dictionary to look up words in.</param>
    /// <param name="memory">Story memory for writing the parse buffer.</param>
    /// <param name="parseBufferAddr">
    /// Address of the parse buffer. Byte 0 = max words (pre-set by game),
    /// byte 1 = word count (written by this method), then 4 bytes per word.
    /// </param>
    /// <param name="textBufferOffset">
    /// Offset added to token positions when writing to the parse buffer.
    /// For V1-4 this is 1 (text starts at byte 1 of the text buffer).
    /// For V5+ this is 2 (text starts at byte 2).
    /// </param>
    /// <param name="skipUnrecognized">
    /// If true, tokens not found in the dictionary are not written to
    /// the parse buffer (ZSpec11 "@tokenise" 4th operand flag).
    /// </param>
    public void Tokenize(
        string input,
        Dictionary dictionary,
        Memory memory,
        int parseBufferAddr,
        int textBufferOffset = 0,
        bool skipUnrecognized = false)
    {
        int maxWords = memory.ReadByte(parseBufferAddr);
        var tokens = SplitIntoTokens(input, dictionary.Separators);

        int wordCount = 0;
        int entryAddr = parseBufferAddr + 2;

        foreach (var token in tokens)
        {
            if (wordCount >= maxWords)
                break;

            int dictAddr = dictionary.Lookup(token.Text);

            if (skipUnrecognized && dictAddr == 0)
                continue;

            // Write 4-byte parse buffer entry:
            //   word: dictionary address (0 if not found)
            //   byte: token text length
            //   byte: position in text buffer (1-based for V1-4, 2-based for V5+)
            memory.WriteWord(entryAddr, (ushort)dictAddr);
            memory.WriteByte(entryAddr + 2, (byte)token.Length);
            memory.WriteByte(entryAddr + 3, (byte)(token.Position + textBufferOffset));

            entryAddr += 4;
            wordCount++;
        }

        memory.WriteByte(parseBufferAddr + 1, (byte)wordCount);
    }
}
