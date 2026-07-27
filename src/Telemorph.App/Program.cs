using Telemorph.App.Cli;

namespace Telemorph.App;

internal static class Program
{
    private static Task<int> Main(string[] args) => TelemorphCli.CreateRootCommand().Parse(args).InvokeAsync();
}