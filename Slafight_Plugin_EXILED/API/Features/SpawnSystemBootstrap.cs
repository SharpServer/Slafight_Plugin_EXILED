using Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.Contexts;
using Slafight_Plugin_EXILED.API.Features.SpawnSystemDictionaries.UnitPacks;
using Slafight_Plugin_EXILED.MainHandlers;

namespace Slafight_Plugin_EXILED.API.Features;

public static class UnitPackBootstrap
{
    public static void RegisterAllPacks()
    {
        UnitPackRegistry.Clear();

        DefaultUnitPacks.Register();
        FacilityTerminationPacks.Register();
        MtfCoreForceUnitPacks.Register();
        MtfSeeNoEvilUnitPacks.Register();
        MtfPandrasBoxUnitPacks.Register();
        SnowWarriorsPacks.Register();
        HorizonInitiativeUnitPacks.Register();
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