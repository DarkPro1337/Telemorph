namespace Telemorph.Core.Models;

public enum TargetKind
{
    Sticker,
    Emoji
}

/// <summary>
/// Represents a conversion profile that determines the type and dimensions
/// of media to be used during a conversion process.
/// </summary>
/// <remarks>
/// This class is designed for use with media conversion workflows, such as
/// generating Emoji or Sticker assets. The specified dimensions and target kind
/// determine the output format and resolution.
/// </remarks>
public sealed class ConversionProfile(
    TargetKind kind,
    int width,
    int height,
    int maxFps,
    double maxDurationSeconds,
    bool variableHeight = false)
{
    public TargetKind Kind { get; } = kind;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int MaxFps { get; } = maxFps;
    public double MaxDurationSeconds { get; } = maxDurationSeconds;
    public bool VariableHeight { get; } = variableHeight;

    /// <summary>
    /// Creates a predefined conversion profile for Sticker generation with
    /// specific dimensions and target kind.
    /// </summary>
    /// <returns>A <see cref="ConversionProfile"/> instance configured for Stickers with
    /// a width and height of 512x512 pixels.</returns>
    public static ConversionProfile Sticker(int maxFps, double maxDurationSeconds, bool variableHeight)
    {
        return new ConversionProfile(TargetKind.Sticker,
            width: 512,
            height: 512,
            maxFps,
            maxDurationSeconds,
            variableHeight);
    }

    /// <summary>
    /// Creates a predefined conversion profile for Emoji generation with
    /// specific dimensions and target kind.
    /// </summary>
    /// <returns>A <see cref="ConversionProfile"/> instance configured for Emojis with
    /// a width and height of 100x100 pixels.</returns>
    public static ConversionProfile Emoji(int maxFps, double maxDurationSeconds)
    {
        return new ConversionProfile(TargetKind.Emoji,
            width: 100,
            height: 100,
            maxFps,
            maxDurationSeconds);
    }
}