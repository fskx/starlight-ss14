using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Misc;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TongueGunComponent : Component
{
    /// <summary>
    /// The entity currently being pulled.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("entityBeingPulled"), AutoNetworkedField]
    public EntityUid? EntityBeingPulled { get; set; }
}
