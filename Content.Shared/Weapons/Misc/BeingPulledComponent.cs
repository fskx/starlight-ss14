using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Misc;

/// <summary>
/// Added to entities tethered by a tethergun.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BeingPulledComponent : Component
{
    [DataField("puller"), AutoNetworkedField]
    public EntityUid Puller; // Basically smoker or whoever uses the tongue gun
}
