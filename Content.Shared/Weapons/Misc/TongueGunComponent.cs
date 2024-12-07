using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Weapons.Misc;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TongueGunComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("lineColor"), AutoNetworkedField]
    public Color LineColor = Color.Orange;

    /// <summary>
    /// The entity the tethered target has a joint to.
    /// </summary>
    [DataField("tetherEntity"), AutoNetworkedField]
    public EntityUid? TetherEntity { get; set; }

    /// <summary>
    /// Maximum distance to throw entities.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("throwDistance"), AutoNetworkedField]
    public float ThrowDistance = 15f;

    [ViewVariables(VVAccess.ReadWrite), DataField("throwForce"), AutoNetworkedField]
    public float ThrowForce = 30f;

    /// <summary>
    /// The entity currently being pulled.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("entityBeingPulled"), AutoNetworkedField]
    public EntityUid? EntityBeingPulled { get; set; }

    /// <summary>
    /// Max force between the tether entity and the tethered target.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("maxForce"), AutoNetworkedField]
    public float MaxForce = 200f;

    [ViewVariables(VVAccess.ReadWrite), DataField("frequency"), AutoNetworkedField]
    public float Frequency = 10f;

    [ViewVariables(VVAccess.ReadWrite), DataField("dampingRatio"), AutoNetworkedField]
    public float DampingRatio = 2f;

    /// <summary>
    /// Maximum amount of mass a tethered entity can have.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("massLimit"), AutoNetworkedField]
    public float MassLimit = 100f;

    
}
