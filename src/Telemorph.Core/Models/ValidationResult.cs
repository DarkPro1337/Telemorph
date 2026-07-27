namespace Telemorph.Core.Models;

public sealed record ValidationResult(
    MediaInfo Media,
    long SizeBytes,
    bool IsStructurallyValid,
    bool MeetsTargetSize,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => IsStructurallyValid && MeetsTargetSize;
}
