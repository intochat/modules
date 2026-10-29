using System.Text.Json;

namespace IntoChat.OS.Modules.Flutter.Views;

/// <summary>Publishes changed trees in order, continuing the current generation after a script restart.</summary>
public sealed class ViewPublisher(IView view, long generation)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 128 };
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;
    private long version;
    private string? previous;

    public async Task Publish(UiNode root)
    {
        var serialized = JsonSerializer.Serialize(root, Json);
        var snapshot = JsonSerializer.Deserialize<UiNode>(serialized, Json)!;
        await gate.WaitAsync();
        try
        {
            if (!initialized)
            {
                var current = await view.Read();
                if (current?.Generation > generation)
                {
                    throw new InvalidOperationException("The view belongs to a newer program generation.");
                }
                if (current?.Generation == generation)
                {
                    version = current.Version;
                    previous = JsonSerializer.Serialize(current.Root, Json);
                }
                initialized = true;
            }
            if (serialized == previous) { return; }
            var next = checked(version + 1);
            await view.Publish(new(generation, next, snapshot));
            version = next;
            previous = serialized;
        }
        finally { gate.Release(); }
    }
}
