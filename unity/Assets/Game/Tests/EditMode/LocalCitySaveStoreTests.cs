#nullable enable

namespace PampaSkylines.Tests
{
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PampaSkylines.Core;
using PampaSkylines.SaveSync;

public sealed class LocalCitySaveStoreTests
{
    [Test]
    public void CitySnapshot_FromWorld_ClonesWorldState()
    {
        var state = WorldState.CreateNew("Detached Snapshot");
        var snapshot = CitySnapshot.FromWorld(state, "v-detached", "pc");

        state.CityName = "Mutated";
        state.Tick = 99;

        Assert.That(snapshot.State.CityName, Is.EqualTo("Detached Snapshot"));
        Assert.That(snapshot.State.Tick, Is.EqualTo(0));
    }

    [Test]
    public async Task LocalCitySaveStore_ListsSavedCities()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "pampa-save-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try
        {
            var store = new LocalCitySaveStore(rootPath);
            var alpha = CitySnapshot.FromWorld(WorldState.CreateNew("Alpha"), "v-alpha", "pc");
            var beta = CitySnapshot.FromWorld(WorldState.CreateNew("Beta"), "v-beta", "pc");

            await store.SaveAsync(alpha);
            await store.SaveAsync(beta);

            var cities = await store.ListCitiesAsync();

            Assert.That(cities.Count, Is.EqualTo(2));
            Assert.That(cities.Any(city => city.DisplayName == "Alpha"), Is.True);
            Assert.That(cities.Any(city => city.DisplayName == "Beta"), Is.True);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
}
