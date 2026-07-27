using System.ComponentModel;
using System.Diagnostics;

namespace Telemorph.Core.Infrastructure;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string executable,
        IEnumerable<string> arguments,
        Action<string>? standardOutputLine = null,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process();
        process.StartInfo = startInfo;

        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Failed to start '{executable}'.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Unable to start '{executable}'. Use a bundled release or provide an explicit tool path.", ex);
        }

        var stdoutTask = ReadStreamAsync(process.StandardOutput, standardOutputLine, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, onLine: null, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // The process may already have exited.
            }

            throw;
        }

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }

    private static async Task<string> ReadStreamAsync(
        StreamReader reader,
        Action<string>? onLine,
        CancellationToken cancellationToken)
    {
        var output = new System.Text.StringBuilder();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            output.AppendLine(line);
            onLine?.Invoke(line);
        }

        return output.ToString();
    }
}
