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

namespace Content.Shared.Weapons.Misc;

public abstract class SharedTongueGunSystem : EntitySystem
{
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

    private void OnTongueShot(EntityUid uid, TongueGunComponent component, ref AfterInteractEvent args)
    {
        _sawmill.Error("tongue gun shot");

        if (args.Target != null) 
        {
            StartTether(gun, component, target, user)
        }
    }  

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // Just to set the angular velocity due to joint funnies
        var tetheredQuery = EntityQueryEnumerator<TetheredComponent, PhysicsComponent>();

        while (tetheredQuery.MoveNext(out var uid, out _, out var physics))
        {
             _sawmill.Error("tether update");
            var sign = Math.Sign(physics.AngularVelocity);

            if (sign == 0)
            {
                sign = 1;
            }

            var targetVelocity = MathF.PI * sign;

            var shortFall = Math.Clamp(targetVelocity - physics.AngularVelocity, -SpinVelocity, SpinVelocity);
            shortFall *= frameTime * AngularChange;

            _physics.ApplyAngularImpulse(uid, shortFall, body: physics);
        }
    }

        protected virtual void StartTether(EntityUid gunUid, BaseForceGunComponent component, EntityUid target, EntityUid? user,
        PhysicsComponent? targetPhysics = null, TransformComponent? targetXform = null)
    {
        _sawmill.Error("on start tether");
        if (!Resolve(target, ref targetPhysics, ref targetXform))
            return;

        if (component.Tethered != null)
        {
            StopTether(gunUid, component, true);
        }

        TryComp<AppearanceComponent>(gunUid, out var appearance);
        _appearance.SetData(gunUid, TetherVisualsStatus.Key, true, appearance);
        _appearance.SetData(gunUid, ToggleableLightVisuals.Enabled, true, appearance);

        // Target updates
        TransformSystem.Unanchor(target, targetXform);
        component.Tethered = target;
        var tethered = EnsureComp<TetheredComponent>(target);
        _physics.SetBodyStatus(target, targetPhysics, BodyStatus.InAir, false);
        _physics.SetSleepingAllowed(target, targetPhysics, false);
        tethered.Tetherer = gunUid;
        tethered.OriginalAngularDamping = targetPhysics.AngularDamping;
        _physics.SetAngularDamping(target, targetPhysics, 0f);
        _physics.SetLinearDamping(target, targetPhysics, 0f);
        _physics.SetAngularVelocity(target, SpinVelocity, body: targetPhysics);
        _physics.WakeBody(target, body: targetPhysics);
        var thrown = EnsureComp<ThrownItemComponent>(component.Tethered.Value);
        thrown.Thrower = gunUid;
        _blocker.UpdateCanMove(target);

        // Invisible tether entity
        var tether = Spawn("TetherEntity", TransformSystem.GetMapCoordinates(target));
        var tetherPhysics = Comp<PhysicsComponent>(tether);
        component.TetherEntity = tether;
        _physics.WakeBody(tether);

        var joint = _joints.CreateMouseJoint(tether, target, id: TetherJoint);

        SharedJointSystem.LinearStiffness(component.Frequency, component.DampingRatio, tetherPhysics.Mass, targetPhysics.Mass, out var stiffness, out var damping);
        joint.Stiffness = stiffness;
        joint.Damping = damping;
        joint.MaxForce = component.MaxForce;

        // Sad...
        if (_netManager.IsServer && component.Stream == null)
            component.Stream = _audio.PlayPredicted(component.Sound, gunUid, null)?.Entity;

        Dirty(target, tethered);
        Dirty(gunUid, component);
    }
}


//  (TryTether(uid, args.Target.Value, args.User, component))