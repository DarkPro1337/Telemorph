namespace Telemorph.App.Terminal;

internal sealed class ConsoleCancellationScope : IDisposable
{
    private readonly CancellationTokenSource _source = new();
    private bool _disposed;

    public ConsoleCancellationScope()
    {
        Console.CancelKeyPress += HandleCancelKeyPress;
    }

    public CancellationToken Token => _source.Token;

    public void Dispose()
    {
        if (_disposed)
            return;

        Console.CancelKeyPress -= HandleCancelKeyPress;
        _source.Dispose();
        _disposed = true;
    }

    private void HandleCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        if (!_disposed)
            _source.Cancel();
    }
}
