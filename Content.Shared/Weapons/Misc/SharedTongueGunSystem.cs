using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.ActionBlocker;
using Content.Shared.Buckle.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Misc;

public abstract class SharedTongueGunSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;

    private const string TetherJoint = "tether";

    private const float SpinVelocity = MathF.PI;
    private const float AngularChange = 1f;



    private ISawmill _sawmill = default!;
    public SharedTongueGunSystem()
    {
        _sawmill = Logger.GetSawmill("tongue");
    }

    public override void Initialize()
    {
        _sawmill.Error("init tongue gun");
        base.Initialize();

        SubscribeLocalEvent<TongueGunComponent, AfterInteractEvent>(OnTongueShot);
       // SubscribeLocalEvent<TongueGunComponent, ActivateInWorldEvent>(OnTongueActivate);
    }

    public override void Update(float frameTime) 
    {
        base.Update(frameTime);

        var tetheredQuery = EntityQueryEnumerator<TonguedComponent, PhysicsComponent>();

        while (tetheredQuery.MoveNext(out var uid, out _, out var physics)) // You're very much welcome to replace this all with PullVirtualController
        {
        var tetheredComp = EnsureComp<TonguedComponent>(uid);
        var tethererPos = TransformSystem.GetWorldPosition(tetheredComp.Tetherer);
        var targetPos = TransformSystem.GetWorldPosition(uid);

        if ((tethererPos - targetPos).Length() <=0.2f)
        {
            StopPulling(tetheredComp.Tetherer, EnsureComp<TongueGunComponent>(tetheredComp.Tetherer), true);
            continue;
        }

        var velocity = 10;
        var direction = (tethererPos - targetPos).Normalized();

        _physics.SetLinearVelocity(uid, direction * velocity, body: physics);
        }
    }

    private void OnTongueShot(EntityUid uid, TongueGunComponent component, ref AfterInteractEvent args)
    {
        _sawmill.Error("tongue gun shot");
        if (component.Tethered != null) {StopPulling(uid, component, true); return; }// Stops pulling if tether already active
        if (args.Target == null || !_mob.IsAlive(args.Target.Value)) return;
        StartPulling(uid, component, args.Target.Value, args.User);
        TransformSystem.SetCoordinates(component.TetherEntity!.Value, new EntityCoordinates(uid, new Vector2(0f, 0f)));
    }  
    protected virtual void StartPulling(EntityUid gunUid, TongueGunComponent component, EntityUid target, EntityUid? user,
    PhysicsComponent? targetPhysics = null, TransformComponent? targetXform = null)
    {   
        _sawmill.Error("on start tether");
        if (!Resolve(target, ref targetPhysics, ref targetXform)) return;
        // Target updates
        TransformSystem.Unanchor(target, targetXform);
        component.Tethered = target;
        var tethered = EnsureComp<TonguedComponent>(target);
        _physics.SetBodyStatus(target, targetPhysics, BodyStatus.InAir, false);
        _physics.SetSleepingAllowed(target, targetPhysics, false);
        tethered.Tetherer = gunUid;
        _physics.SetLinearDamping(target, targetPhysics, 0f);
        _physics.WakeBody(target, body: targetPhysics);
        var thrown = EnsureComp<ThrownItemComponent>(component.Tethered.Value);
        thrown.Thrower = gunUid;
        _blocker.UpdateCanMove(target);

        // Invisible tether entity
        var tether = Spawn("TetherEntity", TransformSystem.GetMapCoordinates(target));
        var tetherPhysics = Comp<PhysicsComponent>(tether);
        component.TetherEntity = tether;
        _physics.WakeBody(tether);

        Dirty(target, tethered);
        Dirty(gunUid, component);
    }

    protected virtual void StopPulling(EntityUid gunUid, TongueGunComponent component, bool land = true, bool transfer = false)
    {
        _sawmill.Error($"on stop pulling {gunUid}, {component}, ");
        if (component.Tethered == null)
            return;

        if (component.TetherEntity != null)
        {
            if (_netManager.IsServer)
                QueueDel(component.TetherEntity.Value);

            component.TetherEntity = null;
        }

        if (TryComp<PhysicsComponent>(component.Tethered, out var targetPhysics))
        {
            if (land)
            {
                var thrown = EnsureComp<ThrownItemComponent>(component.Tethered.Value);
                _thrown.LandComponent(component.Tethered.Value, thrown, targetPhysics, true);
                _thrown.StopThrow(component.Tethered.Value, thrown);
            }

            _physics.SetBodyStatus(component.Tethered.Value, targetPhysics, BodyStatus.OnGround);
            _physics.SetSleepingAllowed(component.Tethered.Value, targetPhysics, true);
        }

        TryComp<AppearanceComponent>(gunUid, out var appearance);
        _appearance.SetData(gunUid, TetherVisualsStatus.Key, false, appearance);
        _appearance.SetData(gunUid, ToggleableLightVisuals.Enabled, false, appearance);

        RemComp<TonguedComponent>(component.Tethered.Value);
        _sawmill.Error("deleted tongued? i think...");
        _blocker.UpdateCanMove(component.Tethered.Value);
        component.Tethered = null;
        Dirty(gunUid, component);
    }

    [Serializable, NetSerializable]
    protected sealed class RequestTetherMoveEvent : EntityEventArgs
    {
        public NetCoordinates Coordinates;
    }

    [Serializable, NetSerializable]
    public enum TetherVisualsStatus : byte
    {
        Key,
    }
}
