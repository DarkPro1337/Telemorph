using Telemorph.Core.Models;

namespace Telemorph.App.Terminal;

internal sealed class ConsoleConversionProgress : IProgress<ConversionProgress>, IDisposable
{
    private const int BarWidth = 24;
    private readonly Lock _gate = new();
    private readonly bool _interactive = !Console.IsOutputRedirected;
    private int _lastRenderedLength;
    private int _lastPercent = -1;
    private DateTime _lastRender = DateTime.MinValue;
    private bool _disposed;

    public void Report(ConversionProgress value)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (!_interactive)
            {
                ReportRedirected(value);
                return;
            }

            switch (value.Stage)
            {
                case ConversionProgressStage.Probing:
                    RenderTransient("Probing source...");
                    break;

                case ConversionProgressStage.AttemptStarted:
                    _lastPercent = -1;
                    RenderTransient($"[{value.Attempt}/{value.MaxAttempts}] CRF {value.Crf}  preparing...");
                    break;

                case ConversionProgressStage.Encoding:
                    RenderEncoding(value);
                    break;

                case ConversionProgressStage.Validating:
                    RenderTransient($"[{value.Attempt}/{value.MaxAttempts}] CRF {value.Crf}  validating...");
                    break;

                case ConversionProgressStage.AttemptCompleted:
                    CompleteAttempt(value);
                    break;

                case ConversionProgressStage.Searching:
                    RenderTransient($"Selecting next quality: {value.Message.ToLowerInvariant()}...");
                    break;

                case ConversionProgressStage.Finished:
                    ClearTransient();
                    break;
            }
        }
    }

    public void Complete()
    {
        lock (_gate)
            ClearTransient();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            ClearTransient();
            _disposed = true;
        }
    }

    private void RenderEncoding(ConversionProgress value)
    {
        var percent = (int)Math.Round(value.Fraction * 100, MidpointRounding.AwayFromZero);
        var now = DateTime.UtcNow;
        if (percent == _lastPercent && now - _lastRender < TimeSpan.FromMilliseconds(100))
            return;

        _lastPercent = percent;
        _lastRender = now;

        var completed = (int)Math.Round(value.Fraction * BarWidth, MidpointRounding.AwayFromZero);
        completed = Math.Clamp(completed, 0, BarWidth);
        var bar = new string('█', completed) + new string('░', BarWidth - completed);

        RenderTransient($"[{value.Attempt}/{value.MaxAttempts}] CRF {value.Crf}  [{bar}] {percent,3}%");
    }

    private void CompleteAttempt(ConversionProgress value)
    {
        ClearTransient();

        TerminalOutput.ColoredFragment(
            value.MeetsTargetSize ? "  ✓ " : "  ↻ ",
            value.MeetsTargetSize ? ConsoleColor.Green : ConsoleColor.Yellow);

        Console.Write(
            $"Attempt {value.Attempt}/{value.MaxAttempts} · CRF {value.Crf} · " +
            $"{value.SizeBytes / 1024.0:0.0} KiB · ");

        TerminalOutput.ColoredFragment(
            value.MeetsTargetSize ? "fits target" : "too large",
            value.MeetsTargetSize ? ConsoleColor.Green : ConsoleColor.Yellow);

        Console.WriteLine();
    }

    private void ReportRedirected(ConversionProgress value)
    {
        switch (value.Stage)
        {
            case ConversionProgressStage.Probing:
                Console.WriteLine("Progress: probing source");
                break;

            case ConversionProgressStage.AttemptStarted:
                Console.WriteLine($"Progress: attempt {value.Attempt}/{value.MaxAttempts}, CRF {value.Crf}, encoding");
                break;

            case ConversionProgressStage.AttemptCompleted:
                Console.WriteLine(
                    $"Progress: attempt {value.Attempt}/{value.MaxAttempts}, CRF {value.Crf}, " +
                    $"{value.SizeBytes / 1024.0:0.0} KiB, " +
                    (value.MeetsTargetSize ? "fits target" : "too large"));
                break;
        }
    }

    private void RenderTransient(string text)
    {
        var availableWidth = GetAvailableWidth();
        if (text.Length > availableWidth)
            text = text[..Math.Max(1, availableWidth - 1)] + "…";

        Console.Write('\r');
        Console.Write(text);

        if (_lastRenderedLength > text.Length)
            Console.Write(new string(' ', _lastRenderedLength - text.Length));

        _lastRenderedLength = text.Length;
    }

    private void ClearTransient()
    {
        if (!_interactive || _lastRenderedLength == 0)
            return;

        Console.Write('\r');
        Console.Write(new string(' ', _lastRenderedLength));
        Console.Write('\r');
        _lastRenderedLength = 0;
    }

    private static int GetAvailableWidth()
    {
        try
        {
            return Math.Max(20, Console.WindowWidth - 1);
        }
        catch
        {
            return 80;
        }
    }
}
