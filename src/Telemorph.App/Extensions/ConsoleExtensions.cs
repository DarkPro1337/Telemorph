namespace Telemorph.App.Extensions;

public static class ConsoleEx
{
    public static void Write(string text, ConsoleColor color)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = oldColor;
    }

    public static void WriteLine(string text, ConsoleColor color)
    {
        var oldColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ForegroundColor = oldColor;
    }

    public static void WriteStatus(string label, string value, ConsoleColor color)
    {
        Write(label.PadRight(12), ConsoleColor.DarkGray);
        WriteLine(value, color);
    }
}