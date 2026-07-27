using Telemorph.Core.Models;

namespace Telemorph.Core.Pipeline;

public sealed class ConversionPipeline
{
    private readonly FfmpegProbe _probe;
    private readonly ConversionOptimizer _optimizer;

    public ConversionPipeline(FfmpegToolchain toolchain)
    {
        _probe = new FfmpegProbe(toolchain);
        var planner = new ConversionPlanner();
        var encoder = new FfmpegEncoder(toolchain);
        var validator = new OutputValidator(_probe);
        _optimizer = new ConversionOptimizer(planner, encoder, validator);
    }

    public async Task<ConversionResult> ConvertAsync(
        ConversionOptions options,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        var inputPath = Path.GetFullPath(options.InputPath);
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        var normalizedOptions = options with
        {
            InputPath = inputPath,
            OutputPath = Path.GetFullPath(options.OutputPath)
        };

        progress?.Report(new ConversionProgress(ConversionProgressStage.Probing, "Probing source"));
        var source = await _probe.ProbeAsync(inputPath, cancellationToken);
        var (validation, attempts) = await _optimizer.RunAsync(
            normalizedOptions,
            source,
            progress,
            cancellationToken);

        return new ConversionResult(
            normalizedOptions.OutputPath,
            source,
            validation,
            attempts);
    }

    private static void ValidateOptions(ConversionOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.InitialCrf, 0);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.InitialCrf, 51);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.TargetSizeBytes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxEncodeAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Threads, 1);
    }
}
