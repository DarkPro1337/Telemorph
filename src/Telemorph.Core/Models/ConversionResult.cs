namespace Telemorph.Core.Models;

public sealed record EncodeAttempt(int Crf, long SizeBytes, bool MeetsTargetSize);

public sealed record ConversionResult(
    string OutputPath,
    MediaInfo Source,
    ValidationResult Validation,
    IReadOnlyList<EncodeAttempt> Attempts);
