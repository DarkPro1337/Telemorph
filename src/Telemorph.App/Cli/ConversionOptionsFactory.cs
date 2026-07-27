using System.CommandLine;
using Telemorph.Core.Models;

namespace Telemorph.App.Cli;

internal sealed record ConversionOptionsBuildResult(
    ConversionOptions? Options,
    IReadOnlyList<string> Notices,
    IReadOnlyList<string> Warnings,
    string? Error)
{
    public bool IsSuccess => Options is not null && Error is null;
}

internal static class ConversionOptionsFactory
{
    public static ConversionOptionsBuildResult Create(ParseResult parseResult, TelemorphSymbols symbols)
    {
        var input = parseResult.GetValue(symbols.Input);
        if (string.IsNullOrWhiteSpace(input))
            return Failure("Input file is required. Use -h or --help to see usage.");

        var emoji = parseResult.GetValue(symbols.Emoji);
        var sticker = parseResult.GetValue(symbols.Sticker);
        if (emoji && sticker)
            return Failure("Choose either --emoji or --sticker, not both.");

        var notices = new List<string>();
        var warnings = new List<string>();
        if (!emoji && !sticker)
            notices.Add("Neither --sticker nor --emoji was specified. Defaulting to sticker.");

        var crf = parseResult.GetValue(symbols.Crf);
        var fps = parseResult.GetValue(symbols.Fps);
        var duration = parseResult.GetValue(symbols.Duration);
        var maxSizeKib = parseResult.GetValue(symbols.MaxSize);
        var maxAttempts = parseResult.GetValue(symbols.MaxAttempts);
        var threads = parseResult.GetValue(symbols.Threads);

        var validationError = ValidateNumbers(
            crf,
            fps,
            duration,
            maxSizeKib,
            maxAttempts,
            threads);

        if (validationError is not null)
            return Failure(validationError);

        if (fps > TelegramDefaults.MaxFps)
            warnings.Add($"Telegram expects at most {TelegramDefaults.MaxFps} FPS.");

        if (duration > TelegramDefaults.MaxDurationSeconds)
            warnings.Add($"Telegram expects at most {TelegramDefaults.MaxDurationSeconds:0.#} seconds.");

        var variableHeight = parseResult.GetValue(symbols.VariableHeight);
        if (emoji && variableHeight)
        {
            warnings.Add("--variable-height applies only to stickers and will be ignored.");
            variableHeight = false;
        }

        var profile = emoji
            ? ConversionProfile.Emoji(fps, duration)
            : ConversionProfile.Sticker(fps, duration, variableHeight);

        var inputPath = Path.GetFullPath(input);
        var outputPath = parseResult.GetValue(symbols.Output) ?? CreateDefaultOutput(inputPath, profile);
        var targetBytes = checked((long)Math.Round(maxSizeKib * 1024, MidpointRounding.AwayFromZero));

        var options = new ConversionOptions
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            Profile = profile,
            InitialCrf = crf,
            TargetSizeBytes = targetBytes,
            MaxEncodeAttempts = maxAttempts,
            Threads = threads,
            EnableRowMultithreading = !parseResult.GetValue(symbols.NoRowMultithreading),
            FitToMaxDuration = parseResult.GetValue(symbols.FitDuration),
            OptimizeForSize = !parseResult.GetValue(symbols.NoOptimize),
            Overwrite = !parseResult.GetValue(symbols.NoOverwrite)
        };

        return new ConversionOptionsBuildResult(options, notices, warnings, Error: null);
    }

    private static ConversionOptionsBuildResult Failure(string error)
    {
        return new ConversionOptionsBuildResult(Options: null, [], [], error);
    }

    private static string? ValidateNumbers(
        int crf,
        int fps,
        double duration,
        double maxSizeKib,
        int maxAttempts,
        int threads)
    {
        if (crf is < 0 or > 51)
            return "CRF must be between 0 and 51.";
        if (fps <= 0)
            return "FPS must be positive.";
        if (duration <= 0)
            return "Duration must be positive.";
        if (maxSizeKib <= 0 || maxSizeKib > long.MaxValue / 1024.0)
            return "Maximum size must be positive and representable in bytes.";
        if (maxAttempts <= 0)
            return "Maximum attempts must be positive.";
        if (threads <= 0)
            return "Threads must be positive.";
        if (threads > Environment.ProcessorCount)
            return $"Threads cannot exceed the available processor count ({Environment.ProcessorCount}).";
        return null;
    }

    private static string CreateDefaultOutput(string inputPath, ConversionProfile profile)
    {
        var directory = Path.GetDirectoryName(inputPath) ?? Directory.GetCurrentDirectory();
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory, $"{name}_{profile.Kind.ToString().ToLowerInvariant()}.webm");
    }
}
