namespace Telemorph.Core.Models;

public sealed record MediaInfo(
    string Format,
    string Codec,
    int Width,
    int Height,
    double DurationSeconds,
    double FramesPerSecond,
    bool HasAlpha);
