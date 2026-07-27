using System.Runtime.InteropServices;
using Telemorph.Core.Infrastructure;

namespace Telemorph.Core.Pipeline;

public sealed record ToolchainDiagnostics(
    string FfmpegPath,
    string FfprobePath,
    string FfmpegVersion,
    string FfprobeVersion,
    bool HasVp9Encoder);

public sealed class FfmpegToolchain
{
    public string FfmpegPath { get; }
    public string FfprobePath { get; }

    private FfmpegToolchain(string ffmpegPath, string ffprobePath)
    {
        FfmpegPath = ffmpegPath;
        FfprobePath = ffprobePath;
    }

    public static FfmpegToolchain Resolve(string? ffmpegPath = null, string? ffprobePath = null)
    {
        var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
        var baseDirectory = AppContext.BaseDirectory;
        var toolsDirectory = Path.Combine(baseDirectory, "tools", RuntimeInformation.RuntimeIdentifier);

        var resolvedFfmpeg = ResolveTool(
            ffmpegPath,
            Path.Combine(toolsDirectory, $"ffmpeg{executableSuffix}"),
            Path.Combine(baseDirectory, $"ffmpeg{executableSuffix}"),
            "ffmpeg");

        var ffmpegDirectory = Path.GetDirectoryName(
            Path.IsPathFullyQualified(resolvedFfmpeg) ? resolvedFfmpeg : string.Empty);

        var siblingFfprobe = string.IsNullOrEmpty(ffmpegDirectory)
            ? string.Empty
            : Path.Combine(ffmpegDirectory, $"ffprobe{executableSuffix}");

        var resolvedFfprobe = ResolveTool(
            ffprobePath,
            siblingFfprobe,
            Path.Combine(toolsDirectory, $"ffprobe{executableSuffix}"),
            Path.Combine(baseDirectory, $"ffprobe{executableSuffix}"),
            "ffprobe");

        return new FfmpegToolchain(resolvedFfmpeg, resolvedFfprobe);
    }

    public async Task<ToolchainDiagnostics> DiagnoseAsync(CancellationToken cancellationToken = default)
    {
        var ffmpegVersion = await ProcessRunner.RunAsync(
            FfmpegPath,
            ["-version"],
            cancellationToken: cancellationToken);
        EnsureSuccess(ffmpegVersion, "ffmpeg");

        var ffprobeVersion = await ProcessRunner.RunAsync(
            FfprobePath,
            ["-version"],
            cancellationToken: cancellationToken);
        EnsureSuccess(ffprobeVersion, "ffprobe");

        var encoders = await ProcessRunner.RunAsync(
            FfmpegPath,
            ["-hide_banner", "-encoders"],
            cancellationToken: cancellationToken);
        EnsureSuccess(encoders, "ffmpeg encoder discovery");

        return new ToolchainDiagnostics(
            FfmpegPath,
            FfprobePath,
            FirstLine(ffmpegVersion.StandardOutput),
            FirstLine(ffprobeVersion.StandardOutput),
            encoders.StandardOutput.Contains("libvpx-vp9", StringComparison.Ordinal));
    }

    private static string ResolveTool(string? explicitPath, params string[] candidates)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        return candidates.FirstOrDefault(File.Exists) ?? candidates[^1];
    }

    private static void EnsureSuccess(ProcessResult result, string displayName)
    {
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"{displayName} failed with exit code {result.ExitCode}:{Environment.NewLine}{result.StandardError}");
    }

    private static string FirstLine(string value)
    {
        return value.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "unknown";
    }
}
