using Telemorph.Core.Models;

namespace Telemorph.Core.Pipeline;

public sealed class OutputValidator(FfmpegProbe probe)
{
    public async Task<ValidationResult> ValidateAsync(
        string outputPath,
        ConversionProfile profile,
        long targetSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(outputPath))
            throw new InvalidOperationException("ffmpeg did not create an output file.");

        var media = await probe.ProbeAsync(outputPath, cancellationToken);
        var size = new FileInfo(outputPath).Length;
        var errors = new List<string>();

        if (!media.Format.Contains("webm", StringComparison.OrdinalIgnoreCase) &&
            !media.Format.Contains("matroska", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Unexpected output container: {media.Format}.");

        if (!media.Codec.Equals("vp9", StringComparison.OrdinalIgnoreCase))
            errors.Add($"Unexpected output codec: {media.Codec}.");

        if (!media.HasAlpha)
            errors.Add("The output does not advertise an alpha channel.");

        if (media.DurationSeconds > profile.MaxDurationSeconds + 0.05)
            errors.Add($"Duration {media.DurationSeconds:0.###}s exceeds {profile.MaxDurationSeconds:0.###}s.");

        if (media.FramesPerSecond > profile.MaxFps + 0.05)
            errors.Add($"Frame rate {media.FramesPerSecond:0.###} exceeds {profile.MaxFps} FPS.");

        if (profile is { Kind: TargetKind.Sticker, VariableHeight: true })
        {
            if (Math.Max(media.Width, media.Height) != profile.Width || media.Width > profile.Width || media.Height > profile.Height)
                errors.Add($"Unexpected variable sticker dimensions: {media.Width}x{media.Height}.");
        }
        else if (media.Width != profile.Width || media.Height != profile.Height)
        {
            errors.Add($"Unexpected output dimensions: {media.Width}x{media.Height}.");
        }

        return new ValidationResult(
            media,
            size,
            errors.Count == 0,
            size <= targetSizeBytes,
            errors);
    }
}
