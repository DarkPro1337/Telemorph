namespace Telemorph.Core.Models;

public sealed record ConversionPlan(
    string InputPath,
    string OutputPath,
    ConversionProfile Profile,
    MediaInfo Source,
    int Crf,
    int Threads,
    bool EnableRowMultithreading,
    bool Overwrite,
    string VideoFilter);
