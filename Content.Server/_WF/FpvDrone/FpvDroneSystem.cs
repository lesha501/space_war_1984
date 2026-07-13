using Content.Server.Actions;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._WF.FpvDrone;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._WF.FpvDrone;

public sealed class FpvDroneSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _action = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _contacts = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FpvDroneComponent, ComponentStartup>(OnDroneStartup);
        SubscribeLocalEvent<FpvDroneComponent, FpvDroneEjectEvent>(OnDroneEject);
        SubscribeLocalEvent<FpvDroneComponent, EntityTerminatingEvent>(OnDroneTerminating);
        SubscribeLocalEvent<FpvDroneComponent, PreventCollideEvent>(OnDronePreventCollide);

        SubscribeLocalEvent<FpvDroneExplosiveComponent, ComponentInit>(OnExplosiveInit);
        SubscribeLocalEvent<FpvDroneExplosiveComponent, FpvDroneExplosiveEvent>(OnExplosiveAction);

        SubscribeLocalEvent<FpvDroneFoldableComponent, ActivateInWorldEvent>(OnFoldableActivate);
        SubscribeLocalEvent<FpvDroneFoldableComponent, FpvDroneFoldableDoAfterEvent>(OnFoldableDoAfter);
    }

    private void OnFoldableActivate(EntityUid uid, FpvDroneFoldableComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || _container.IsEntityInContainer(uid))
            return;

        var ev = new FpvDroneFoldableDoAfterEvent();
        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.UnfoldDelay, ev, uid)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
            args.Handled = true;
    }

    private void OnFoldableDoAfter(EntityUid uid, FpvDroneFoldableComponent component,
        FpvDroneFoldableDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var coords = _transform.GetMoverCoordinates(uid);
        Spawn(component.UnfoldEntity, coords);
        QueueDel(uid);
        args.Handled = true;
    }

    private void OnDroneStartup(EntityUid uid, FpvDroneComponent component, ComponentStartup args)
    {
        component.EjectAction = _action.AddAction(uid, component.EjectActionPrototypeId);
        component.ShotsUntilDestroy = RollShotsUntilDestroy(component);
        component.ShotsTaken = 0;
    }

    private int RollShotsUntilDestroy(FpvDroneComponent component)
    {
        var minShots = Math.Max(1, component.MinShotsToDestroy);
        var maxShots = Math.Max(minShots, component.MaxShotsToDestroy);
        var preferredMin = Math.Clamp(component.PreferredMinShotsToDestroy, minShots, maxShots);
        var preferredMax = Math.Clamp(component.PreferredMaxShotsToDestroy, preferredMin, maxShots);

        var totalWeight = 0f;
        for (var shots = minShots; shots <= maxShots; shots++)
            totalWeight += GetShotsToDestroyWeight(component, shots, preferredMin, preferredMax);

        if (totalWeight <= 0f)
            return maxShots;

        var roll = _random.NextFloat(totalWeight);
        for (var shots = minShots; shots <= maxShots; shots++)
        {
            roll -= GetShotsToDestroyWeight(component, shots, preferredMin, preferredMax);
            if (roll <= 0f)
                return shots;
        }

        return maxShots;
    }

    private static float GetShotsToDestroyWeight(
        FpvDroneComponent component,
        int shots,
        int preferredMin,
        int preferredMax)
    {
        if (shots == 1)
            return Math.Max(0f, component.OneShotDestroyWeight);

        if (shots >= preferredMin && shots <= preferredMax)
            return Math.Max(0f, component.PreferredShotsWeight);

        return 1f;
    }

    private void OnDronePreventCollide(EntityUid uid, FpvDroneComponent component, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryComp<ProjectileComponent>(args.OtherEntity, out var projectile) || projectile.ProjectileSpent)
            return;

        if (component.ShotsUntilDestroy <= 0)
            component.ShotsUntilDestroy = RollShotsUntilDestroy(component);

        component.ShotsTaken++;

        if (component.ShotsTaken < component.ShotsUntilDestroy)
        {
            args.Cancelled = true;
            return;
        }

        if (TryComp<FpvDroneExplosiveComponent>(uid, out var explosive))
            TryTriggerExplosive(uid, explosive);
        else
        {
            ReturnPilotToBody(uid, component, true);
            QueueDel(uid);
        }

        projectile.ProjectileSpent = true;
        if (projectile.DeleteOnCollide)
            QueueDel(args.OtherEntity);

        args.Cancelled = true;
    }

    private void OnDroneEject(EntityUid uid, FpvDroneComponent component, FpvDroneEjectEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ReturnPilotToBody(uid, component);
    }

    private void OnDroneTerminating(EntityUid uid, FpvDroneComponent component, EntityTerminatingEvent args)
    {
        component.FlyingStream = _audio.Stop(component.FlyingStream);
        ReturnPilotToBody(uid, component, true);
    }

    private void OnExplosiveInit(EntityUid uid, FpvDroneExplosiveComponent component, ComponentInit args)
    {
        if (component.ExplodeActionId != null)
            _action.AddAction(uid, ref component.ExplodeActionEntity, component.ExplodeActionId);
    }

    private void OnExplosiveAction(EntityUid uid, FpvDroneExplosiveComponent component, FpvDroneExplosiveEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryTriggerExplosive(uid, component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var flyingQuery = EntityQueryEnumerator<FpvDroneComponent, PhysicsComponent, InputMoverComponent>();
        while (flyingQuery.MoveNext(out var uid, out var drone, out var physics, out var mover))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            UpdateFlyingSound(uid, drone, physics, mover);
        }

        var query = EntityQueryEnumerator<FpvDroneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drone, out var droneXform))
        {
            if (drone.Pilot == null || TerminatingOrDeleted(uid))
                continue;

            if (!Exists(drone.Control))
            {
                ReturnPilotToBody(uid, drone, true);
                continue;
            }

            var controlXform = Transform(drone.Control);
            var dronePos = _transform.GetWorldPosition(droneXform);
            var controlPos = _transform.GetWorldPosition(controlXform);
            var distSq = (dronePos - controlPos).LengthSquared();
            var maxRangeSq = drone.MaxRange * drone.MaxRange;

            if (distSq > maxRangeSq || droneXform.MapID != controlXform.MapID)
                ReturnPilotToBody(uid, drone, true);
            else if (drone.SignalLost)
            {
                drone.SignalLost = false;
                Dirty(uid, drone);
            }
        }

        var explosiveQuery = EntityQueryEnumerator<FpvDroneExplosiveComponent, TransformComponent>();
        while (explosiveQuery.MoveNext(out var uid, out var explosive, out _))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            _contacts.Clear();
            _physics.GetContactingEntities(uid, _contacts);

            foreach (var contact in _contacts)
            {
                if (HasComp<MobStateComponent>(contact))
                {
                    TryTriggerExplosive(uid, explosive);
                    break;
                }
            }
        }
    }

    private void ReturnPilotToBody(EntityUid uid, FpvDroneComponent component, bool isSignalLost = false)
    {
        var pilot = component.Pilot;
        component.FlyingStream = _audio.Stop(component.FlyingStream);

        if (isSignalLost && !component.SignalLost)
        {
            component.SignalLost = true;
            Dirty(uid, component);
        }

        if (pilot != null && !TerminatingOrDeleted(pilot.Value))
        {
            if (isSignalLost)
            {
                if (component.SignalLostSound != null)
                {
                    _audio.PlayEntity(component.SignalLostSound, pilot.Value, pilot.Value,
                        AudioParams.Default.WithVolume(-2f));
                }

                _popup.PopupEntity(Loc.GetString("fpv-drone-ui-connection-lost"), pilot.Value, pilot.Value,
                    PopupType.LargeCaution);
            }
        }

        component.Pilot = null;
        component.Control = default;
        component.SignalLost = false;
        Dirty(uid, component);
    }

    private void DisconnectDrone(EntityUid uid, FpvDroneComponent component)
    {
        ReturnPilotToBody(uid, component);
    }

    public bool IsControlLinkInRange(EntityUid control, EntityUid drone, FpvDroneComponent? component = null)
    {
        if (!Resolve(drone, ref component, false) || TerminatingOrDeleted(control) || TerminatingOrDeleted(drone))
            return false;

        var droneXform = Transform(drone);
        var controlXform = Transform(control);
        if (droneXform.MapID != controlXform.MapID)
            return false;

        var dronePos = _transform.GetWorldPosition(droneXform);
        var controlPos = _transform.GetWorldPosition(controlXform);
        var maxRangeSq = component.MaxRange * component.MaxRange;
        return (dronePos - controlPos).LengthSquared() <= maxRangeSq;
    }

    public bool TryStartRemoteControl(EntityUid drone, EntityUid control, EntityUid user, FpvDroneComponent? component = null)
    {
        if (!Resolve(drone, ref component, false) || TerminatingOrDeleted(user))
            return false;

        if (component.Pilot != null && component.Pilot != user)
            return false;

        if (component.Control != default && component.Control != control)
            return false;

        if (!IsControlLinkInRange(control, drone, component))
            return false;

        component.Control = control;
        component.Pilot = user;
        component.SignalLost = false;

        Dirty(drone, component);
        return true;
    }

    private void UpdateFlyingSound(
        EntityUid uid,
        FpvDroneComponent drone,
        PhysicsComponent physics,
        InputMoverComponent mover)
    {
        var minSpeedSq = drone.FlyingSoundMinSpeed * drone.FlyingSoundMinSpeed;
        var moving = mover.HasDirectionalMovement ||
                     mover.WishDir.LengthSquared() > minSpeedSq ||
                     physics.LinearVelocity.LengthSquared() > minSpeedSq;

        if (moving && drone.FlyingLoopSound != null && drone.FlyingStream == null)
        {
            drone.FlyingStream = _audio.PlayPvs(drone.FlyingLoopSound, uid,
                AudioParams.Default.WithLoop(true).WithVolume(-5f))?.Entity;
            return;
        }

        if (!moving && drone.FlyingStream != null)
            drone.FlyingStream = _audio.Stop(drone.FlyingStream);
    }

    public bool StopRemoteControl(EntityUid drone, EntityUid? expectedPilot = null, bool isSignalLost = false, FpvDroneComponent? component = null)
    {
        if (!Resolve(drone, ref component, false) || component.Pilot == null)
            return false;

        if (expectedPilot != null && component.Pilot != expectedPilot)
            return false;

        ReturnPilotToBody(drone, component, isSignalLost);
        return true;
    }

    public bool TryTriggerExplosive(EntityUid drone, FpvDroneExplosiveComponent? component = null)
    {
        if (!Resolve(drone, ref component, false))
            return false;

        if (TryComp<FpvDroneComponent>(drone, out var fpvDrone))
            ReturnPilotToBody(drone, fpvDrone, true);

        _explosion.TriggerExplosive(drone, delete: true, totalIntensity: component.TotalIntensity, radius: component.Radius);
        return true;
    }

    public bool TryDisconnectDrone(EntityUid drone, FpvDroneComponent? component = null)
    {
        if (!Resolve(drone, ref component, false))
            return false;

        DisconnectDrone(drone, component);
        return true;
    }
}
