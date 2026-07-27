using Telemorph.Core.Models;

namespace Telemorph.Core.Pipeline;

public sealed class ConversionOptimizer(
    ConversionPlanner planner,
    FfmpegEncoder encoder,
    OutputValidator validator)
{
    public async Task<(ValidationResult Validation, IReadOnlyList<EncodeAttempt> Attempts)> RunAsync(
        ConversionOptions options,
        MediaInfo source,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
            Directory.CreateDirectory(outputDirectory);

        if (!options.Overwrite && File.Exists(options.OutputPath))
            throw new IOException($"Output file already exists: {options.OutputPath}");

        var attempts = new List<EncodeAttempt>();
        string? bestPath = null;
        ValidationResult? bestValidation = null;
        var lowerCrf = options.InitialCrf;
        var upperCrf = 51;
        var candidateCrf = options.InitialCrf;

        try
        {
            while (attempts.Count < options.MaxEncodeAttempts && candidateCrf <= 51)
            {
                var attemptNumber = attempts.Count + 1;
                var attemptPath = CreateAttemptPath(options.OutputPath, candidateCrf);
                var plan = planner.Create(options, source, attemptPath, candidateCrf);

                try
                {
                    progress?.Report(new ConversionProgress(
                        ConversionProgressStage.AttemptStarted,
                        "Starting encode",
                        attemptNumber,
                        options.MaxEncodeAttempts,
                        candidateCrf));

                    await encoder.EncodeAsync(
                        plan,
                        attemptNumber,
                        options.MaxEncodeAttempts,
                        progress,
                        cancellationToken);

                    progress?.Report(new ConversionProgress(
                        ConversionProgressStage.Validating,
                        "Validating output",
                        attemptNumber,
                        options.MaxEncodeAttempts,
                        candidateCrf,
                        Fraction: 1));

                    var validation = await validator.ValidateAsync(
                        attemptPath,
                        options.Profile,
                        options.TargetSizeBytes,
                        cancellationToken);

                    attempts.Add(new EncodeAttempt(candidateCrf, validation.SizeBytes, validation.MeetsTargetSize));
                    progress?.Report(new ConversionProgress(
                        ConversionProgressStage.AttemptCompleted,
                        validation.MeetsTargetSize ? "Target reached" : "Too large",
                        attemptNumber,
                        options.MaxEncodeAttempts,
                        candidateCrf,
                        Fraction: 1,
                        validation.SizeBytes,
                        validation.MeetsTargetSize));

                    if (!validation.IsStructurallyValid)
                        throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

                    if (!options.OptimizeForSize)
                    {
                        bestPath = attemptPath;
                        bestValidation = validation;
                        break;
                    }

                    if (validation.MeetsTargetSize)
                    {
                        DeleteIfExists(bestPath);
                        bestPath = attemptPath;
                        bestValidation = validation;
                        upperCrf = candidateCrf - 1;

                        if (candidateCrf == options.InitialCrf)
                            break;
                    }
                    else
                    {
                        DeleteIfExists(attemptPath);
                        lowerCrf = candidateCrf + 1;
                    }

                    if (lowerCrf > upperCrf)
                        break;

                    candidateCrf = attempts.Count == 1
                        ? upperCrf
                        : lowerCrf + ((upperCrf - lowerCrf) / 2);

                    progress?.Report(new ConversionProgress(
                        ConversionProgressStage.Searching,
                        $"Trying CRF {candidateCrf}",
                        attemptNumber,
                        options.MaxEncodeAttempts,
                        candidateCrf));
                }
                catch
                {
                    DeleteIfExists(attemptPath);
                    throw;
                }
            }

            if (bestPath is null || bestValidation is null)
            {
                var lastSize = attempts.LastOrDefault()?.SizeBytes ?? 0;
                throw new InvalidOperationException(
                    $"Unable to reach the target size of {options.TargetSizeBytes / 1024.0:0.#} KB " +
                    $"after {attempts.Count} encode attempt(s). Last size: {lastSize / 1024.0:0.#} KB.");
            }

            File.Move(bestPath, options.OutputPath, options.Overwrite);
            bestPath = null;

            progress?.Report(new ConversionProgress(
                ConversionProgressStage.Finished,
                "Conversion complete",
                attempts.Count,
                options.MaxEncodeAttempts,
                attempts[^1].Crf,
                Fraction: 1,
                bestValidation.SizeBytes,
                bestValidation.MeetsTargetSize));

            return (bestValidation, attempts);
        }
        finally
        {
            DeleteIfExists(bestPath);
        }
    }

    private static string CreateAttemptPath(string outputPath, int crf)
    {
        var directory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(outputPath);
        return Path.Combine(directory, $".{name}.crf-{crf}.{Guid.NewGuid():N}.webm");
    }

    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
            File.Delete(path);
    }
}
