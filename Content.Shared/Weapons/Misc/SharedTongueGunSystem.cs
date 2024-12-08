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
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffect;
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
using Content.Shared.Damage;
using Robust.Shared.Physics.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;
using Content.Shared.Stunnable;
using Content.Shared.Mobs;

namespace Content.Shared.Weapons.Misc;

public abstract class SharedTongueGunSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netManager = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;

    private const string TetherJoint = "tether";

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

        var tetheredQuery = EntityQueryEnumerator<BeingPulledComponent, PhysicsComponent>();

        while (tetheredQuery.MoveNext(out var uid, out _, out var physics)) // You're very much welcome to replace this all with PullVirtualController
        {
        var beingPulledComp = EnsureComp<BeingPulledComponent>(uid);
        var tethererPos = TransformSystem.GetWorldPosition(beingPulledComp.Puller);
        var targetPos = TransformSystem.GetWorldPosition(uid);

        var velocity = 10;
        var direction = (tethererPos - targetPos).Normalized();
        if ((tethererPos - targetPos).Length() <=0.3f)
        {
            StartEating(beingPulledComp.Puller, EnsureComp<TongueGunComponent>(beingPulledComp.Puller), uid);
            velocity = 0;
        }
        _physics.SetLinearVelocity(uid, direction * velocity, body: physics);
        }
    }

    private void OnTongueShot(EntityUid uid, TongueGunComponent component, ref AfterInteractEvent args)
    {
        _sawmill.Error("tongue gun shot");
        if (component.EntityBeingPulled != null) {StopPulling(uid, component, true); return; }// Stops pulling if tether already active
        if (args.Target == null || !_mob.IsAlive(args.Target.Value)) return;
        StartPulling(uid, component, args.Target.Value, args.User);
    }  
    protected virtual void StartPulling(EntityUid gunUid, TongueGunComponent component, EntityUid target, EntityUid? user,
    PhysicsComponent? targetPhysics = null, TransformComponent? targetXform = null)
    {   
        _sawmill.Error("on start tether");
        if (!Resolve(target, ref targetPhysics, ref targetXform)) return;
        // Target updates
        TransformSystem.Unanchor(target, targetXform);
        component.EntityBeingPulled = target;
        var tethered = EnsureComp<BeingPulledComponent>(target);
        _physics.SetBodyStatus(target, targetPhysics, BodyStatus.InAir, false);
        _physics.SetSleepingAllowed(target, targetPhysics, false);
        tethered.Puller = gunUid;
        _physics.SetLinearDamping(target, targetPhysics, 0f);
        _physics.WakeBody(target, body: targetPhysics);
        var thrown = EnsureComp<ThrownItemComponent>(component.EntityBeingPulled.Value);
        thrown.Thrower = gunUid;
        _blocker.UpdateCanMove(target);
        _statusEffect.TryAddStatusEffect<KnockedDownComponent>(target, "KnockedDown", TimeSpan.FromSeconds(1000), true);

        Dirty(target, tethered);
        Dirty(gunUid, component);
    }

    protected virtual void StartEating(EntityUid gunUid, TongueGunComponent component, EntityUid Prey)
    {
        StopPulling(gunUid, component, false);
        Spawn("EffectSmokerEating", TransformSystem.GetMapCoordinates(Prey));
        var passive_damage = EnsureComp<PassiveDamageComponent>(Prey);
        passive_damage.Damage = new DamageSpecifier();
        passive_damage.Damage.DamageDict.Add("Piercing", 8);
        passive_damage.AllowedStates.Add(MobState.Critical);
        passive_damage.Interval = 0.5f;
        passive_damage.DamageCap = 200;
        //entityManager.EnsureComponent<ZombifyOnDeathComponent>(Prey); (HORDE DO NOT ZOMBIFY!!!)
        //entityManager.EnsureComponent<PendingZombieComponent>(Prey);
    }

    protected virtual void StopPulling(EntityUid gunUid, TongueGunComponent component, bool wake = true, bool land = true, bool transfer = false)
    {
        _sawmill.Error($"on stop pulling {gunUid}, {component}, ");
        if (component.EntityBeingPulled == null)
            return;

        if (TryComp<PhysicsComponent>(component.EntityBeingPulled, out var targetPhysics))
        {
            if (land)
            {
                var thrown = EnsureComp<ThrownItemComponent>(component.EntityBeingPulled.Value);
                _thrown.LandComponent(component.EntityBeingPulled.Value, thrown, targetPhysics, true);
                _thrown.StopThrow(component.EntityBeingPulled.Value, thrown);
            }

            _physics.SetBodyStatus(component.EntityBeingPulled.Value, targetPhysics, BodyStatus.OnGround);
            _physics.SetSleepingAllowed(component.EntityBeingPulled.Value, targetPhysics, true);
        }

        TryComp<AppearanceComponent>(gunUid, out var appearance);
        _appearance.SetData(gunUid, TetherVisualsStatus.Key, false, appearance);
        _appearance.SetData(gunUid, ToggleableLightVisuals.Enabled, false, appearance);

        RemComp<BeingPulledComponent>(component.EntityBeingPulled.Value);
        if (wake) RemComp<KnockedDownComponent>(component.EntityBeingPulled.Value);
        _blocker.UpdateCanMove(component.EntityBeingPulled.Value);
        component.EntityBeingPulled = null;
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
