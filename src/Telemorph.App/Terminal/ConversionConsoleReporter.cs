using Telemorph.Core.Models;
using Telemorph.Core.Pipeline;

namespace Telemorph.App.Terminal;

internal static class ConversionConsoleReporter
{
    public static async Task<int> RunDoctorAsync(
        FfmpegToolchain toolchain,
        CancellationToken cancellationToken)
    {
        var diagnostics = await toolchain.DiagnoseAsync(cancellationToken);

        TerminalOutput.Status("ffmpeg:", diagnostics.FfmpegPath, ConsoleColor.Green);
        TerminalOutput.Plain($"  {diagnostics.FfmpegVersion}");
        TerminalOutput.Status("ffprobe:", diagnostics.FfprobePath, ConsoleColor.Green);
        TerminalOutput.Plain($"  {diagnostics.FfprobeVersion}");
        TerminalOutput.Status(
            "VP9:",
            diagnostics.HasVp9Encoder ? "libvpx-vp9 available" : "libvpx-vp9 missing",
            diagnostics.HasVp9Encoder ? ConsoleColor.Green : ConsoleColor.Red);

        return diagnostics.HasVp9Encoder ? 0 : 1;
    }

    public static void WritePlan(
        ConversionOptions options,
        FfmpegToolchain toolchain)
    {
        TerminalOutput.Plain($"Telemorph     {options.Profile.Kind} conversion");
        TerminalOutput.Plain($"Input:        {options.InputPath}");
        TerminalOutput.Plain($"Output:       {Path.GetFullPath(options.OutputPath)}");
        TerminalOutput.Plain(
            $"Profile:      {options.Profile.Width}x{options.Profile.Height}, " +
            $"max {options.Profile.MaxFps} FPS, " +
            $"max {options.Profile.MaxDurationSeconds:0.##}s");

        TerminalOutput.Plain($"Target size:  {options.TargetSizeBytes / 1024.0:0.#} KiB");
        TerminalOutput.Plain($"Start CRF:    {options.InitialCrf}");
        TerminalOutput.Plain(
            $"Optimization: {(options.OptimizeForSize ? $"up to {options.MaxEncodeAttempts} attempts" : "disabled")}");

        TerminalOutput.Plain($"ffmpeg:       {toolchain.FfmpegPath}");
        TerminalOutput.Plain($"ffprobe:      {toolchain.FfprobePath}");
        TerminalOutput.Plain(string.Empty);
    }

    public static void WriteResult(ConversionResult result, long targetSizeBytes)
    {
        TerminalOutput.Success("Conversion complete.");
        TerminalOutput.Status("Output:", result.OutputPath, ConsoleColor.Green);
        TerminalOutput.Status("Size:",
            $"{result.Validation.SizeBytes / 1024.0:0.0} KiB / {targetSizeBytes / 1024.0:0.0} KiB",
            result.Validation.MeetsTargetSize ? ConsoleColor.Green : ConsoleColor.Yellow);

        TerminalOutput.Status("Media:",
            $"{result.Validation.Media.Width}x{result.Validation.Media.Height}, " +
            $"{result.Validation.Media.FramesPerSecond:0.##} FPS, " +
            $"{result.Validation.Media.DurationSeconds:0.###}s", ConsoleColor.Green);

        var attempts = string.Join(", ",
            result.Attempts.Select(attempt => $"CRF {attempt.Crf}: {attempt.SizeBytes / 1024.0:0.0} KiB"));
        TerminalOutput.Status("Attempts:", attempts, ConsoleColor.DarkGray);

        if (!result.Validation.MeetsTargetSize)
            TerminalOutput.Warning("The output is structurally valid but exceeds the requested size.");
    }
}
