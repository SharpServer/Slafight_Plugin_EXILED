using ProjectMER.Features.Objects;

namespace Slafight_Plugin_EXILED.API.Interface;

/// <summary>Bind behavior to existing schematic geometry before Create is called.</summary>
public interface IObjectPrefabMarkerReceiver
{
    void BindMarker(SchematicObjectPrefabObject marker);
}
