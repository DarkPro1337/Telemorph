using System.CommandLine;
using Telemorph.App.Extensions;
using Telemorph.Core;
using Telemorph.Core.Models;

namespace Telemorph.App;

internal static class Program
{
    private const double TelegramMaxKb = 256.0;
    private const int TelegramMaxFps = 30;
    private const double TelegramMaxDurationSeconds = 3.0;

    private static async Task<int> Main(string[] args)
    {
        Argument<string> inputArg = new("input")
        {
            Description = "Input animated file (gif, webp, etc.)."
        };

        Option<string?> outputOption = new("--output", "-o")
        {
            Description = "Output .webm path. If omitted, uses <input>_<mode>.webm."
        };

        Option<bool> emojiOption = new("--emoji", "-e")
        {
            Description = "Convert to Telegram custom emoji (100x100)."
        };

        Option<bool> stickerOption = new("--sticker", "-s")
        {
            Description = "Convert to Telegram video sticker (512x512)."
        };

        Option<int> crfOption = new("--crf", "-c")
        {
            Description = "CRF (Constant Rate Factor) for VP9 (higher = smaller file, lower quality).",
            DefaultValueFactory = _ => 38
        };

        Option<int> fpsOption = new("--fps", "-f")
        {
            Description = "FPS for output video. Telegram limits up to 30.",
            DefaultValueFactory = _ => TelegramMaxFps
        };

        Option<double> durationOption = new("--duration", "-d")
        {
            Description = "Max duration for output video. Telegram limits up to 3 seconds.",
            DefaultValueFactory = _ => TelegramMaxDurationSeconds
        };

        Option<string> ffmpegOption = new("--ffmpeg")
        {
            Description = "Path to ffmpeg executable.",
            DefaultValueFactory = _ => "ffmpeg"
        };

        Option<string> magickOption = new("--magick")
        {
            Description = "Path to ImageMagick 'magick' executable.",
            DefaultValueFactory = _ => "magick"
        };

        Option<bool> fitDurationOption = new("--fit-duration", "-fd")
        {
            Description = "Time-fit the animation into the max duration (e.g., 3s) by proportionally scaling frame delays instead of cutting."
        };

        Option<bool> variableStickerHeightOption = new("--variable-height", "-vh")
        {
            Description = "For stickers: use Telegram-style scaling (one side 512px, other side ≤512) instead of 512×512 canvas.",
            DefaultValueFactory = _ => false
        };

        Option<int> threadsOption = new("--threads", "-t")
        {
            Description = "Number of threads to use for conversion.",
            DefaultValueFactory = _ => 4
        };

        Option<bool> rowBasedMultithreadingOption = new("--row-mt", "-rmt")
        {
            Description = "Enable row-based multithreading for ffmpeg.",
            DefaultValueFactory = _ => true
        };

        RootCommand rootCommand = new("Telemorph - tool for converting animated images to Telegram WEBM stickers/emoji")
        {
            inputArg,
            outputOption,
            emojiOption,
            stickerOption,
            crfOption,
            fpsOption,
            durationOption,
            ffmpegOption,
            magickOption,
            fitDurationOption,
            variableStickerHeightOption,
            threadsOption,
            rowBasedMultithreadingOption
        };

        rootCommand.SetAction(async parseResult =>
        {
            var input = parseResult.GetValue(inputArg);
            var output = parseResult.GetValue(outputOption);
            var emoji = parseResult.GetValue(emojiOption);
            var sticker = parseResult.GetValue(stickerOption);
            var crf = parseResult.GetValue(crfOption);
            var fps = parseResult.GetValue(fpsOption);
            var duration = parseResult.GetValue(durationOption);
            var ffmpegPath = parseResult.GetValue(ffmpegOption);
            var magickPath = parseResult.GetValue(magickOption);
            var fitDuration = parseResult.GetValue(fitDurationOption);
            var variableHeight = parseResult.GetValue(variableStickerHeightOption);
            var threads = parseResult.GetValue(threadsOption);
            var rowBasedMultithreading = parseResult.GetValue(rowBasedMultithreadingOption);

            if (string.IsNullOrEmpty(input))
            {
                Console.Error.WriteLine("Input file is required.");
                return 1;
            }

            if (string.IsNullOrEmpty(ffmpegPath))
            {
                Console.Error.WriteLine("ffmpeg path is required.");
                return 1;
            }

            if (string.IsNullOrEmpty(magickPath))
            {
                Console.Error.WriteLine("ImageMagick path is required.");
                return 1;
            }

            if (!sticker && !emoji)
            {
                ConsoleEx.WriteLine("Neither --sticker nor --emoji specified. Defaulting to --sticker mode.", ConsoleColor.DarkGray);
                sticker = true;
            }

            if (sticker && emoji)
            {
                Console.Error.WriteLine("Choose either --emoji or --sticker, not both.");
                return 1;
            }

            if (emoji && variableHeight)
            {
                ConsoleEx.WriteLine("Warning: --variable-sticker-height is only used for stickers and ignored for emoji.", ConsoleColor.Yellow);
            }

            if (crf is < 0 or > 51)
            {
                Console.Error.WriteLine("CRF must be between 0 and 51.");
                return 1;
            }

            switch (fps)
            {
                case <= 0:
                    Console.Error.WriteLine("FPS must be positive.");
                    return 1;
                case > TelegramMaxFps:
                    ConsoleEx.WriteLine($"To upload it to Telegram, FPS must be <= {TelegramMaxFps}.", ConsoleColor.Yellow);
                    break;
            }

            switch (duration)
            {
                case <= 0:
                    Console.Error.WriteLine("Duration must be positive.");
                    return 1;
                case > TelegramMaxDurationSeconds:
                    ConsoleEx.WriteLine($"To upload it to Telegram, duration must be <= {TelegramMaxDurationSeconds}.", ConsoleColor.Yellow);
                    break;
            }

            if (threads <= 0)
            {
                Console.Error.WriteLine("Threads must be positive.");
                return 1;
            }

            if (threads > Environment.ProcessorCount)
            {
                Console.Error.WriteLine($"Threads cannot exceed the number of available processors ({Environment.ProcessorCount}).");
                return 1;
            }

            var kind = emoji ? TargetKind.Emoji : TargetKind.Sticker;
            var profile = kind == TargetKind.Emoji
                ? ConversionProfile.Emoji(fps, duration)
                : ConversionProfile.Sticker(fps, duration, variableHeight);

            var inputFull = Path.GetFullPath(input);

            if (!File.Exists(inputFull))
            {
                await Console.Error.WriteLineAsync($"Input file not found: {inputFull}");
                return 1;
            }

            if (output is null)
            {
                var dir = Path.GetDirectoryName(inputFull)!;
                var name = Path.GetFileNameWithoutExtension(inputFull);
                output = Path.Combine(dir, $"{name}_{profile.Kind.ToString().ToLowerInvariant()}.webm");
            }

            Console.WriteLine($"Telemorph     {profile.Kind} conversion");
            Console.WriteLine($"Input:        {inputFull}");
            Console.WriteLine($"Output:       {output}");
            Console.WriteLine($"Profile:      {profile.Width}x{profile.Height}, max {profile.MaxFps} fps, max {profile.MaxDurationSeconds:0.##}s");
            Console.WriteLine($"CRF:          {crf}");
            Console.WriteLine($"ffmpeg:       {ffmpegPath}");
            Console.WriteLine($"magick:       {magickPath}");
            Console.WriteLine($"Fit duration: {(fitDuration ? "scale to max duration" : "cut at max duration")}");
            Console.WriteLine($"Threads:      {threads}");
            Console.WriteLine($"Row MT:       {rowBasedMultithreading}");
            if (sticker)
                Console.WriteLine($"Variable H:   {(variableHeight ? "enabled" : "disabled")}");

            Console.WriteLine();

            var converter = new FfmpegConverter(ffmpegPath, magickPath)
            {
                Threads = threads,
                EnableRowMultithreading = rowBasedMultithreading
            };

            try
            {
                await converter.ConvertAsync(inputFull, output, profile, crf, overwrite: true, fitDuration);

                var fi = new FileInfo(output);
                var sizeKb = fi.Length / 1024.0;
                ConsoleEx.WriteLine("Conversion complete.", ConsoleColor.Green);
                
                if (!(sizeKb > TelegramMaxKb))
                {
                    ConsoleEx.WriteStatus("Output size: ", $"{sizeKb:0.0} KB", ConsoleColor.Green);
                    return 0;
                }

                ConsoleEx.WriteStatus("Output size: ", $"{sizeKb:0.0} KB", ConsoleColor.Yellow);
                ConsoleEx.WriteStatus("Warning: ", "File is larger than 256 KB. Telegram may reject it. Try higher --crf (e.g. 42).", ConsoleColor.Yellow);
                return 0;
            }
            catch (Exception ex)
            {
                ConsoleEx.WriteLine("Conversion failed:", ConsoleColor.Red);
                Console.WriteLine(ex.Message);
                return 1;
            }
        });

        var parseResult = rootCommand.Parse(args);
        return await parseResult.InvokeAsync();
    }
}