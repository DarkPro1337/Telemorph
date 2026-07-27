namespace Telemorph.Core.Models;

public sealed record ConversionOptions
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public required ConversionProfile Profile { get; init; }
    public int InitialCrf { get; init; } = 38;
    public long TargetSizeBytes { get; init; } = 256 * 1024;
    public int MaxEncodeAttempts { get; init; } = 6;
    public int Threads { get; init; } = 4;
    public bool EnableRowMultithreading { get; init; } = true;
    public bool FitToMaxDuration { get; init; }
    public bool OptimizeForSize { get; init; } = true;
    public bool Overwrite { get; init; } = true;
}
