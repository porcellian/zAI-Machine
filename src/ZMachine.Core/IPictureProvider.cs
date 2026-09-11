namespace ZMachine.Core;

/// <summary>
/// Abstraction for picture resource access, decoupling the interpreter
/// engine from SkiaSharp image decoding. The IO layer implements this
/// to load and render pictures from Blorb resources.
/// </summary>
/// <remarks>
/// ZSpec11 "@picture_data" — queries dimensions and availability.
/// Blorb "Picture Resource Chunks" — PNG, JPEG, and Rect formats.
/// </remarks>
public interface IPictureProvider
{
    /// <summary>
    /// Returns true if any picture resources are available.
    /// Used by @picture_data 0 to decide the branch.
    /// </summary>
    bool HasPictures { get; }

    /// <summary>Total number of picture resources.</summary>
    int PictureCount { get; }

    /// <summary>
    /// Blorb release number for the picture set.
    /// Blorb "The Release Number Chunk".
    /// </summary>
    int ReleaseNumber { get; }

    /// <summary>Returns true if the specified picture exists.</summary>
    bool HasPicture(int number);

    /// <summary>
    /// Returns true if the picture is a Rect placeholder.
    /// Blorb "Placeholder Pictures" — exists for @picture_data
    /// and @erase_picture but @draw_picture is an error.
    /// </summary>
    bool IsPlaceholder(int number);

    /// <summary>
    /// Gets the dimensions of a picture resource.
    /// Returns (0, 0) if the picture does not exist.
    /// </summary>
    (int Width, int Height) GetPictureSize(int number);

    /// <summary>
    /// Draws a picture at the specified pixel position in the current window.
    /// Returns false if the picture is a Rect placeholder or does not exist.
    /// </summary>
    bool DrawPicture(int number, int y, int x);

    /// <summary>
    /// Erases the area occupied by a picture at the specified position,
    /// filling it with the current background color.
    /// Works for both real pictures and Rect placeholders.
    /// </summary>
    bool ErasePicture(int number, int y, int x);
}
