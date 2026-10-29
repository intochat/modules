using System.Text;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Sandbox;

// Console output, standard error as much as standard output, is a behavior's log. One writer per stream is installed for the process and routes each
// line to the behavior whose code wrote it (an async-local sink); anything else goes to the real console.
internal static class BehaviorConsole
{
    private static readonly AsyncLocal<LineSink?> Current = new();
    private static readonly Lock Gate = new();
    private static bool _installed;

    public static IDisposable Capture(Action<string> line)
    {
        lock (Gate)
        {
            if (!_installed)
            {
                Console.SetOut(new Router(Console.Out));
                Console.SetError(new Router(Console.Error));
                _installed = true;
            }
        }
        var previous = Current.Value;
        Current.Value = new LineSink(line);
        return new Restore(previous);
    }

    private sealed class Restore(LineSink? previous) : IDisposable
    {
        public void Dispose()
        {
            Current.Value?.Flush();
            Current.Value = previous;
        }
    }

    private sealed class LineSink(Action<string> line)
    {
        private readonly StringBuilder _pending = new();

        public void Write(char value)
        {
            lock (_pending)
            {
                if (value == '\n') { Emit(); }
                else if (value != '\r') { _pending.Append(value); }
            }
        }

        public void Flush()
        {
            lock (_pending) { if (_pending.Length > 0) { Emit(); } }
        }

        private void Emit()
        {
            line(_pending.ToString());
            _pending.Clear();
        }
    }

    private sealed class Router(TextWriter host) : TextWriter
    {
        public override Encoding Encoding => host.Encoding;

        public override void Write(char value)
        {
            if (Current.Value is { } sink) { sink.Write(value); }
            else { host.Write(value); }
        }

        public override void Write(string? value)
        {
            if (Current.Value is { } sink) { foreach (var character in value ?? "") { sink.Write(character); } }
            else { host.Write(value); }
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }
    }
}
