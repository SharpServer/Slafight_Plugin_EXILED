using Slafight_Plugin_EXILED.MainHandlers;
using Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;
using Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.Contexts;

namespace Slafight_Plugin_EXILED.API.Features;

public static class UnitPackBootstrap
{
    public static void RegisterAllPacks()
    {
        UnitPackRegistry.Clear();

        DefaultUnitPacks.Register();
        FacilityTerminationPacks.Register();
        MtfSeeNoEvilUnitPacks.Register();
        MtfCoreForceUnitPacks.Register();
        SnowWarriersPacks.Register();
    }

    public static void UnregisterAllPacks()
    {
        UnitPackRegistry.Clear();
    }
}

public static class SpawnContextBootstrap
{
    public static void RegisterAllContexts(SpawnSystem.SpawnConfig config)
    {
        SpawnContextRegistry.Clear();

        DefaultContexts.Register(config);
        FacilityTerminationContexts.Register();

        SpawnContextRegistry.SetActive("Default");
    }

    public static void UnregisterAllContexts()
    {
        SpawnContextRegistry.Clear();
    }
}