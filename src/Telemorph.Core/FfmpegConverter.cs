using System.Diagnostics;
using System.Globalization;
using System.Text;
using Telemorph.Core.Models;

namespace Telemorph.Core;

/// <summary>
/// Handles conversion from animated images (gif/webp/etc) to VP9 WEBM
/// using ImageMagick (magick) to extract frames and ffmpeg to encode.
/// </summary>
public sealed class FfmpegConverter(string ffmpegPath = "ffmpeg", string magickPath = "magick")
{
    /// <summary>
    /// Optional explicit thread count for ffmpeg. Defaults to 4.
    /// </summary>
    public int Threads { get; init; }

    /// <summary>
    /// Enables ffmpeg row-based multithreading (-row-mt 1) when true.
    /// </summary>
    public bool EnableRowMultithreading { get; init; }

    public async Task ConvertAsync(
        string inputPath,
        string outputPath,
        ConversionProfile profile,
        int crf,
        bool overwrite = true,
        bool fitToMaxDuration = false,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException("Input file not found.", inputPath);

        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        var tempDir = Path.Combine(Path.GetTempPath(), "telemorph_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            await RunMagickAsync(inputPath, tempDir, cancellationToken);
            var delays = await ReadFrameDelaysAsync(inputPath, cancellationToken);
            var concatPath = await WriteConcatFileAsync(tempDir, delays, profile.MaxDurationSeconds, fitToMaxDuration, cancellationToken);
            await RunFfmpegAsync(concatPath, outputPath, profile, crf, overwrite, cancellationToken);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Telemorph] Failed to delete temp dir '{tempDir}': {ex}");
            }
        }
    }

    /// <summary>
    /// Executes the ImageMagick command-line tool to extract and process frames
    /// from an input animated image, saving the frames to a specified directory.
    /// </summary>
    /// <param name="inputPath">The file path of the input animated image.</param>
    /// <param name="framesDir">The directory where extracted frames will be stored.</param>
    /// <param name="cancellationToken">Token used to signal the cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when ImageMagick produces no frames, typically indicating an invalid input.
    /// </exception>
    private async Task RunMagickAsync(string inputPath, string framesDir, CancellationToken cancellationToken)
    {
        var framePattern = Path.Combine(framesDir, "frame-%05d.png");

        var args = new StringBuilder();
        args.Append('"').Append(inputPath).Append('"').Append(' ');
        args.Append("-coalesce -alpha set -background none ");
        args.Append('"').Append(framePattern).Append('"');

        await RunProcessAsync(magickPath, args.ToString(), "magick", cancellationToken);

        if (!Directory.EnumerateFiles(framesDir, "frame-*.png").Any())
            throw new InvalidOperationException("ImageMagick produced no frames. Input may be invalid.");
    }

    /// <summary>
    /// Reads the per-frame delays from the input animated image using ImageMagick
    /// and converts them from centiseconds to seconds, clamping any zero delays to
    /// a minimal positive value to avoid encoding issues.
    /// </summary>
    /// <param name="inputPath">The file path of the input animated image.</param>
    /// <param name="cancellationToken">Token used to signal the cancellation of the operation.</param>
    /// <returns>An array of doubles representing the per-frame delays in seconds.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the operation fails to retrieve frame delays or if no delays are detected.
    /// </exception>
    private async Task<double[]> ReadFrameDelaysAsync(string inputPath, CancellationToken cancellationToken)
    {
        var args = new StringBuilder();
        args.Append("identify -format \"%T\\n\" ");
        args.Append('"').Append(inputPath).Append('"');

        var output = await RunProcessCaptureStdoutAsync(magickPath, args.ToString(), "magick identify", cancellationToken);
        var lines = output.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<double>(lines.Length);
        var hadInvalidLines = false;

        foreach (var line in lines)
        {
            if (!int.TryParse(line.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cs))
            {
                hadInvalidLines = true;
                continue;
            }

            var seconds = Math.Max(cs / 100.0, 0.001);
            result.Add(seconds);
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Failed to read frame delays via ImageMagick identify.");

        if (hadInvalidLines)
            Debug.WriteLine("[Telemorph] Some frame delay lines could not be parsed and were skipped.");

        return result.ToArray();
    }

    /// <summary>
    /// Writes an FFmpeg concat file to associate video frames with their durations for processing.
    /// </summary>
    /// <param name="framesDir">The directory containing the video frames to be processed.</param>
    /// <param name="delays">An array of frame durations in seconds.</param>
    /// <param name="maxDurationSeconds">The maximum allowable duration for the resulting video animation.</param>
    /// <param name="fitToMaxDuration">A flag indicating whether the total animation duration should be scaled to fit
    /// within the specified maximum duration. </param>
    /// <param name="cancellationToken">Token used to signal the cancellation of the operation.</param>
    private static async Task<string> WriteConcatFileAsync(string framesDir, double[] delays, double maxDurationSeconds,
        bool fitToMaxDuration, CancellationToken cancellationToken)
    {
        var frameFiles = Directory.EnumerateFiles(framesDir, "frame-*.png")
            .OrderBy(f => f)
            .ToList();

        if (frameFiles.Count == 0)
            throw new InvalidOperationException("No frames found to create concat file.");

        var count = Math.Min(frameFiles.Count, delays.Length);

        var sb = new StringBuilder();
        sb.AppendLine("ffconcat version 1.0");

        const double minDur = 0.001; // seconds

        if (fitToMaxDuration)
        {
            var total = delays.Take(count).Sum();
            if (total <= 0)
                total = count * minDur;

            var scale = total > maxDurationSeconds ? (maxDurationSeconds / total) : 1.0;

            for (var i = 0; i < count; i++)
            {
                var file = frameFiles[i];
                var dur = delays[i] * scale;
                if (dur < minDur) dur = minDur;

                var fileEsc = file.Replace("\\", "/").Replace("'", "'\\''");
                sb.Append("file ").Append('\'').Append(fileEsc).Append('\'').AppendLine();
                sb.Append("duration ").Append(dur.ToString("0.########", CultureInfo.InvariantCulture)).AppendLine();
            }

            var lastFile = frameFiles[Math.Max(0, count - 1)].Replace("\\", "/").Replace("'", "'\\''");
            sb.Append("file ").Append('\'').Append(lastFile).Append('\'').AppendLine();
        }
        else
        {
            var acc = 0.0;
            var i = 0;
            for (; i < count; i++)
            {
                var file = frameFiles[i];
                var dur = delays[i];

                if (acc + dur > maxDurationSeconds)
                    break;

                var fileEsc = file.Replace("\\", "/").Replace("'", "'\\''");
                sb.Append("file ").Append('\'').Append(fileEsc).Append('\'').AppendLine();
                sb.Append("duration ").Append(dur.ToString("0.########", CultureInfo.InvariantCulture)).AppendLine();
                acc += dur;
            }

            // Add a final frame (no duration) as required by concat demuxer to mark the end.
            // Use the last included frame if any, otherwise the first frame.
            var lastFile = (i > 0 ? frameFiles[i - 1] : frameFiles[0]).Replace("\\", "/").Replace("'", "'\\''");
            sb.Append("file ").Append('\'').Append(lastFile).Append('\'').AppendLine();
        }

        var concatPath = Path.Combine(framesDir, "frames.ffconcat");
        await File.WriteAllTextAsync(concatPath, sb.ToString(), new UTF8Encoding(false), cancellationToken);
        return concatPath;
    }

    /// <summary>
    /// Executes the FFmpeg command-line tool to encode video frames from a concatenated file
    /// into a VP9 WEBM file while applying the specified conversion profile and settings.
    /// </summary>
    /// <param name="concatFile">The path to the concatenated file containing input frame data and timestamps.</param>
    /// <param name="outputPath">The file path where the encoded WEBM video will be stored.</param>
    /// <param name="profile">The conversion profile specifying video dimensions and limits.</param>
    /// <param name="crf">The constant rate factor (quality level) for VP9 encoding, where lower values result in higher quality.</param>
    /// <param name="overwrite">Indicates whether to overwrite the output file if it already exists.</param>
    /// <param name="cancellationToken">Token used to signal the cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the FFmpeg process fails during execution.
    /// </exception>
    private async Task RunFfmpegAsync(
        string concatFile,
        string outputPath,
        ConversionProfile profile,
        int crf,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var args = new StringBuilder();

        args.Append(overwrite ? "-y " : "-n ");
        args.Append("-f concat -safe 0 ");
        args.Append($"-i \"{concatFile}\" ");
        args.Append($"-t {profile.MaxDurationSeconds.ToString("0.###", CultureInfo.InvariantCulture)} ");
        args.Append($"-vf \"{BuildVideoFilter(profile)}\" ");
        args.Append("-fps_mode vfr -c:v libvpx-vp9 -pix_fmt yuva420p -b:v 0 -crf ")
            .Append(crf)
            .Append(" -an ");

        if (EnableRowMultithreading)
            args.Append("-row-mt 1 ");

        args.Append($"-threads {Threads} ");
        args.Append($"\"{outputPath}\"");

        await RunProcessAsync(ffmpegPath, args.ToString(), "ffmpeg", cancellationToken);
    }

    /// <summary>
    /// Constructs the video filter string used by ffmpeg for scaling, padding, and formatting
    /// based on the given conversion profile.
    /// </summary>
    /// <param name="profile">The conversion profile specifying dimensions, scaling options, and other settings
    /// for the target video.</param>
    /// <returns>A string representing the video filter configuration to be used by ffmpeg.</returns>
    private static string BuildVideoFilter(ConversionProfile profile)
    {
        if (profile is { Kind: TargetKind.Sticker, VariableHeight: true })
        {
            var maxSide = profile.Width;
            return
                $"scale='if(gt(iw,ih),{maxSide},-2)':'if(gt(iw,ih),-2,{maxSide})':flags=lanczos," +
                "format=yuva420p";
        }

        var width = profile.Width;
        var height = profile.Height;
        return
            $"scale={width}:{height}:force_original_aspect_ratio=decrease:flags=lanczos," +
            $"pad={width}:{height}:( {width}-iw)/2:( {height}-ih)/2:color=0x00000000," +
            "format=yuva420p";
    }

    /// <summary>
    /// Executes a specified process with given arguments and monitors its outputs and exit status.
    /// </summary>
    /// <param name="fileName">The name or path of the executable to run.</param>
    /// <param name="arguments">The command-line arguments to pass to the executable.</param>
    /// <param name="displayName">The display name of the process, used for error reporting.</param>
    /// <param name="cancellationToken">Token used to signal the cancellation of the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the process fails to start, or if the process exits with a non-zero exit code.
    /// </exception>
    private static async Task RunProcessAsync(
        string fileName,
        string arguments,
        string displayName,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;

        var stderr = new StringBuilder();

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start {displayName} process.");

        process.BeginErrorReadLine();

        await Task.Run(() =>
        {
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        /* ignore */
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                }

                Thread.Sleep(50);
            }
        }, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var errorText = stderr.ToString();
            throw new InvalidOperationException(
                $"{displayName} failed with exit code {process.ExitCode}:{Environment.NewLine}{errorText}");
        }
    }

    /// <summary>
    /// Executes a specified external process with provided arguments, capturing its standard output.
    /// </summary>
    /// <param name="fileName">The path to the executable file for the process to run.</param>
    /// <param name="arguments">The arguments to pass to the process.</param>
    /// <param name="displayName">A human-readable name for the process, used in error messages.</param>
    /// <param name="cancellationToken">Token used to signal the cancellation of the operation.</param>
    /// <returns>A task that, when completed, provides the captured standard output of the process as a string.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the process fails to start, does not produce a successful exit code,
    /// or encounters an issue during execution.
    /// </exception>
    private static async Task<string> RunProcessCaptureStdoutAsync(
        string fileName,
        string arguments,
        string displayName,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process();
        process.StartInfo = startInfo;
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                stderr.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start {displayName} process.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await Task.Run(() =>
        {
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                        /* ignore */
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                }
                Thread.Sleep(50);
            }
        }, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"{displayName} failed with exit code {process.ExitCode}:{Environment.NewLine}{stderr}");

        return stdout.ToString();
    }
}