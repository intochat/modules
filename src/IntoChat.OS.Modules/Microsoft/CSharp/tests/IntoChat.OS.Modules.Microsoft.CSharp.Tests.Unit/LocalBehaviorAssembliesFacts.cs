using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using IntoChat.OS.Modules.Microsoft.CSharp.Assemblies;
using IntoChat.OS.Modules.Microsoft.CSharp.Catalog;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace IntoChat.OS.Modules.Microsoft.CSharp.Tests.Unit;

public sealed class LocalBehaviorAssembliesFacts
{
    [Fact]
    public async Task Put_PrunesOldestBehaviors_WhenExceedingMaxCached()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "intochat-behaviors-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IntoChatOS:Home"] = tempDir,
            }).Build();

            var protection = new EphemeralDataProtectionProvider();
            var services = new ServiceCollection().BuildServiceProvider();
            var options = Options.Create(new CSharpOptions { MaxCachedBehaviors = 2 });

            var store = new LocalBehaviorAssemblies(config, protection, services, options);

            var hash1 = new string('a', 64);
            var hash2 = new string('b', 64);
            var hash3 = new string('c', 64);

            var b1 = new CompiledBehavior([1, 2, 3], [4, 5, 6], hash1);
            var b2 = new CompiledBehavior([7, 8, 9], [10, 11, 12], hash2);
            var b3 = new CompiledBehavior([13, 14, 15], [16, 17, 18], hash3);

            await store.Put(b1, CancellationToken.None);
            Assert.True(await store.Exists(hash1, CancellationToken.None));

            await store.Put(b2, CancellationToken.None);
            Assert.True(await store.Exists(hash1, CancellationToken.None));
            Assert.True(await store.Exists(hash2, CancellationToken.None));

            // Putting 3rd exceeds max limit of 2: b1 should be pruned as oldest
            await store.Put(b3, CancellationToken.None);

            Assert.False(await store.Exists(hash1, CancellationToken.None), "Oldest behavior pair b1 must be pruned");
            Assert.True(await store.Exists(hash2, CancellationToken.None), "b2 must still exist");
            Assert.True(await store.Exists(hash3, CancellationToken.None), "b3 must exist");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
    }
}
