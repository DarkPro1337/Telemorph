using System.CommandLine;

namespace Telemorph.App.Cli;

internal static class TelemorphCli
{
    public static RootCommand CreateRootCommand()
    {
        var symbols = new TelemorphSymbols();
        RootCommand command = new(
            "Telemorph - Telegram-aware animated image to VP9 WebM converter")
        {
            symbols.Input,
            symbols.Output,
            symbols.Emoji,
            symbols.Sticker,
            symbols.Crf,
            symbols.Fps,
            symbols.Duration,
            symbols.MaxSize,
            symbols.MaxAttempts,
            symbols.NoOptimize,
            symbols.Ffmpeg,
            symbols.Ffprobe,
            symbols.Doctor,
            symbols.FitDuration,
            symbols.VariableHeight,
            symbols.Threads,
            symbols.NoRowMultithreading,
            symbols.NoOverwrite
        };

        var handler = new ConversionCommandHandler(symbols);
        command.SetAction(handler.ExecuteAsync);
        return command;
    }
}
