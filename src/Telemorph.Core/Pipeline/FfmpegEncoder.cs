using System.Globalization;
using Telemorph.Core.Infrastructure;
using Telemorph.Core.Models;

namespace Telemorph.Core.Pipeline;

public sealed class FfmpegEncoder(FfmpegToolchain toolchain)
{
    public async Task EncodeAsync(
        ConversionPlan plan,
        int attempt,
        int maxAttempts,
        IProgress<ConversionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string>
        {
            "-hide_banner",
            "-loglevel", "error",
            "-nostats",
            "-progress", "pipe:1",
            plan.Overwrite ? "-y" : "-n",
            "-i", plan.InputPath,
            "-map", "0:v:0",
            "-t", plan.Profile.MaxDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture),
            "-vf", plan.VideoFilter,
            "-fps_mode", "vfr",
            "-c:v", "libvpx-vp9",
            "-pix_fmt", "yuva420p",
            "-b:v", "0",
            "-crf", plan.Crf.ToString(CultureInfo.InvariantCulture),
            "-an",
            "-sn",
            "-metadata:s:v:0", "alpha_mode=1"
        };

        if (plan.EnableRowMultithreading)
        {
            arguments.Add("-row-mt");
            arguments.Add("1");
        }

        arguments.Add("-threads");
        arguments.Add(plan.Threads.ToString(CultureInfo.InvariantCulture));
        arguments.Add(plan.OutputPath);

        progress?.Report(new ConversionProgress(
            ConversionProgressStage.Encoding,
            "Encoding",
            attempt,
            maxAttempts,
            plan.Crf,
            Fraction: 0));

        var result = await ProcessRunner.RunAsync(
            toolchain.FfmpegPath,
            arguments,
            line => ReportFfmpegProgress(line, plan, attempt, maxAttempts, progress),
            cancellationToken);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");
    }

    private static void ReportFfmpegProgress(
        string line,
        ConversionPlan plan,
        int attempt,
        int maxAttempts,
        IProgress<ConversionProgress>? progress)
    {
        if (progress is null)
            return;

        if (line.Equals("progress=end", StringComparison.Ordinal))
        {
            progress.Report(new ConversionProgress(
                ConversionProgressStage.Encoding,
                "Encoding",
                attempt,
                maxAttempts,
                plan.Crf,
                Fraction: 1));
            return;
        }

        const string prefix = "out_time_us=";
        if (!line.StartsWith(prefix, StringComparison.Ordinal) ||
            !long.TryParse(line.AsSpan(prefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds))
            return;

        var duration = Math.Max(plan.Profile.MaxDurationSeconds, 0.001);
        var fraction = Math.Clamp(microseconds / 1_000_000.0 / duration, 0, 1);
        progress.Report(new ConversionProgress(
            ConversionProgressStage.Encoding,
            "Encoding",
            attempt,
            maxAttempts,
            plan.Crf,
            fraction));
    }
}
