namespace Telemorph.App.Terminal;

internal static class TerminalOutput
{
    private const int StatusLabelWidth = 12;
    private static readonly Lock _gate = new();

    public static void Plain(string text) => WriteLine(Console.Out, text, color: null);
    public static void Muted(string text) => WriteLine(Console.Out, text, ConsoleColor.DarkGray);
    public static void Success(string text) => WriteLine(Console.Out, text, ConsoleColor.Green);
    public static void Warning(string text) => WriteLine(Console.Out, text, ConsoleColor.Yellow);
    public static void Error(string text) => WriteLine(Console.Error, text, ConsoleColor.Red);

    public static void Status(string label, string value, ConsoleColor valueColor = ConsoleColor.Gray)
    {
        lock (_gate)
        {
            WriteCore(Console.Out, label.PadRight(StatusLabelWidth), ConsoleColor.DarkGray, appendNewLine: false);
            WriteCore(Console.Out, value, valueColor, appendNewLine: true);
        }
    }

    internal static void ColoredFragment(string text, ConsoleColor color)
    {
        lock (_gate)
            WriteCore(Console.Out, text, color, appendNewLine: false);
    }

    private static void WriteLine(TextWriter writer, string text, ConsoleColor? color)
    {
        lock (_gate)
            WriteCore(writer, text, color, appendNewLine: true);
    }

    private static void WriteCore(TextWriter writer, string text, ConsoleColor? color, bool appendNewLine)
    {
        var useColor = color.HasValue && !IsRedirected(writer);
        var previousColor = Console.ForegroundColor;

        try
        {
            if (useColor)
                Console.ForegroundColor = color!.Value;

            if (appendNewLine)
                writer.WriteLine(text);
            else
                writer.Write(text);
        }
        finally
        {
            if (useColor)
                Console.ForegroundColor = previousColor;
        }
    }

    private static bool IsRedirected(TextWriter writer)
    {
        return ReferenceEquals(writer, Console.Error)
            ? Console.IsErrorRedirected
            : Console.IsOutputRedirected;
    }
}
