using System.CommandLine;

namespace Telemorph.App.Cli;

internal sealed class TelemorphSymbols
{
    public Argument<string?> Input { get; } = new("input")
    {
        Description = "Input animated file (GIF, WebP, APNG, MP4, etc.).",
        DefaultValueFactory = _ => null
    };

    public Option<string?> Output { get; } = new("--output", "-o")
    {
        Description = "Output .webm path. Defaults to <input>_<mode>.webm."
    };

    public Option<bool> Emoji { get; } = new("--emoji", "-e")
    {
        Description = "Convert to a Telegram custom emoji (100x100)."
    };

    public Option<bool> Sticker { get; } = new("--sticker", "-s")
    {
        Description = "Convert to a Telegram video sticker."
    };

    public Option<int> Crf { get; } = new("--crf", "-c")
    {
        Description = "Starting VP9 CRF. Auto optimization raises it only when necessary.",
        DefaultValueFactory = _ => 38
    };

    public Option<int> Fps { get; } = new("--fps", "-f")
    {
        Description = "Maximum output FPS.",
        DefaultValueFactory = _ => TelegramDefaults.MaxFps
    };

    public Option<double> Duration { get; } = new("--duration", "-d")
    {
        Description = "Maximum output duration in seconds.",
        DefaultValueFactory = _ => TelegramDefaults.MaxDurationSeconds
    };

    public Option<double> MaxSize { get; } = new("--max-size-kb")
    {
        Description = "Target maximum output size in KiB.",
        DefaultValueFactory = _ => TelegramDefaults.MaxSizeKib
    };

    public Option<int> MaxAttempts { get; } = new("--max-attempts")
    {
        Description = "Maximum encode attempts used by automatic CRF optimization.",
        DefaultValueFactory = _ => 6
    };

    public Option<bool> NoOptimize { get; } = new("--no-optimize")
    {
        Description = "Encode once without automatically searching for a CRF that meets the size target."
    };

    public Option<string?> Ffmpeg { get; } = new("--ffmpeg")
    {
        Description = "Explicit ffmpeg path. Bundled ffmpeg is preferred when omitted."
    };

    public Option<string?> Ffprobe { get; } = new("--ffprobe")
    {
        Description = "Explicit ffprobe path. Bundled ffprobe is preferred when omitted."
    };

    public Option<bool> Doctor { get; } = new("--doctor")
    {
        Description = "Check ffmpeg, ffprobe, and the VP9 encoder, then exit."
    };

    public Option<bool> FitDuration { get; } = new("--fit-duration")
    {
        Description = "Speed up long animations to fit instead of cutting them."
    };

    public Option<bool> VariableHeight { get; } = new("--variable-height")
    {
        Description = "For stickers, make one side 512px and keep the other side at or below 512px."
    };

    public Option<int> Threads { get; } = new("--threads", "-t")
    {
        Description = "Number of encoder threads.",
        DefaultValueFactory = _ => Math.Min(4, Environment.ProcessorCount)
    };

    public Option<bool> NoRowMultithreading { get; } = new("--no-row-mt")
    {
        Description = "Disable libvpx row-based multithreading."
    };

    public Option<bool> NoOverwrite { get; } = new("--no-overwrite")
    {
        Description = "Fail if the output already exists."
    };
}
