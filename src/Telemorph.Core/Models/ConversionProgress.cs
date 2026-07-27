namespace Telemorph.Core.Models;

public enum ConversionProgressStage
{
    Probing,
    AttemptStarted,
    Encoding,
    Validating,
    AttemptCompleted,
    Searching,
    Finished
}

public sealed record ConversionProgress(
    ConversionProgressStage Stage,
    string Message,
    int Attempt = 0,
    int MaxAttempts = 0,
    int Crf = 0,
    double Fraction = 0,
    long SizeBytes = 0,
    bool MeetsTargetSize = false);
