using System.CommandLine;
using Telemorph.App.Terminal;
using Telemorph.Core.Models;
using Telemorph.Core.Pipeline;

namespace Telemorph.App.Cli;

internal sealed class ConversionCommandHandler(TelemorphSymbols symbols)
{
    public async Task<int> ExecuteAsync(ParseResult parseResult)
    {
        using var cancellation = new ConsoleCancellationScope();

        try
        {
            var toolchain = FfmpegToolchain.Resolve(
                parseResult.GetValue(symbols.Ffmpeg),
                parseResult.GetValue(symbols.Ffprobe));

            if (parseResult.GetValue(symbols.Doctor))
                return await ConversionConsoleReporter.RunDoctorAsync(toolchain, cancellation.Token);

            var buildResult = ConversionOptionsFactory.Create(parseResult, symbols);
            if (!buildResult.IsSuccess)
            {
                TerminalOutput.Error(buildResult.Error!);
                return 1;
            }

            foreach (var notice in buildResult.Notices)
                TerminalOutput.Muted(notice);

            foreach (var warning in buildResult.Warnings)
                TerminalOutput.Warning($"Warning: {warning}");

            var options = buildResult.Options!;
            ConversionConsoleReporter.WritePlan(options, toolchain);

            var pipeline = new ConversionPipeline(toolchain);
            ConversionResult result;
            using (var progress = new ConsoleConversionProgress())
            {
                result = await pipeline.ConvertAsync(options, progress, cancellation.Token);
                progress.Complete();
            }

            ConversionConsoleReporter.WriteResult(result, options.TargetSizeBytes);
            return result.Validation.IsValid ? 0 : 2;
        }
        catch (OperationCanceledException)
        {
            TerminalOutput.Warning("Conversion cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            TerminalOutput.Error("Conversion failed:");
            TerminalOutput.Error(ex.Message);
            return 1;
        }
    }
}
