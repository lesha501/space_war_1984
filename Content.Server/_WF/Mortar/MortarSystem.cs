using Content.Shared.Interaction;
using Content.Shared._WF.Mortar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Verbs;
using Content.Shared.Item;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Construction.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System;
using System.Numerics;

namespace Content.Server.Mortar
{
    public sealed class MortarSystem : EntitySystem
    {
        private const string FoldedPrototypeId = "MortarFoldedItem";
        private const string DeployedPrototypeId = "WeaponMortar";
        private const float DeployDelay = 3f;

        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedMapSystem _maps = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly ExplosionSystem _explosion = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;
        [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
        [Dependency] private readonly AnchorableSystem _anchorable = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MortarComponent, MortarFireMessage>(OnFireMessage);
            SubscribeLocalEvent<MortarComponent, InteractUsingEvent>(OnInteractUsing);

            SubscribeLocalEvent<MortarFoldableComponent, GetVerbsEvent<InteractionVerb>>(AddFoldVerb);
            SubscribeLocalEvent<MortarFoldableComponent, MortarFoldDoAfterEvent>(OnFoldDoAfter);

            SubscribeLocalEvent<ItemComponent, AfterInteractEvent>(OnAfterInteract);
            SubscribeLocalEvent<ItemComponent, MortarDeployDoAfterEvent>(OnDeployDoAfter);
        }

        private void AddFoldVerb(EntityUid uid, MortarFoldableComponent component, GetVerbsEvent<InteractionVerb> args)
        {
            if (!args.CanAccess || !args.CanInteract)
                return;

            args.Verbs.Add(new InteractionVerb
            {
                Act = () => TryFold(uid, component, args.User),
                Text = "Сложить",
            });
        }

        private void TryFold(EntityUid uid, MortarFoldableComponent component, EntityUid user)
        {
            if (!TryComp<MortarComponent>(uid, out var mortar) || mortar.Loaded)
            {
                _popup.PopupEntity("Нельзя сложить заряженный миномет.", uid, user);
                return;
            }

            var ev = new MortarFoldDoAfterEvent();
            var args = new DoAfterArgs(EntityManager, user, component.FoldDelay, ev, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            };

            if (_doAfter.TryStartDoAfter(args))
                _popup.PopupEntity("Складывание миномета...", uid, user);
        }

        private void OnFoldDoAfter(EntityUid uid, MortarFoldableComponent component, MortarFoldDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            var coords = _transform.GetMoverCoordinates(uid);
            var folded = Spawn(component.FoldedEntity, coords);

            if (TryComp<PhysicsComponent>(uid, out var physics) && physics.BodyType == BodyType.Static)
                _transform.Unanchor(uid);

            _hands.TryPickup(args.User, folded);
            QueueDel(uid);
            args.Handled = true;
        }

        private void OnAfterInteract(EntityUid uid, ItemComponent item, AfterInteractEvent args)
        {
            if (args.Handled || !args.CanReach || args.Target != null)
                return;

            if (MetaData(uid).EntityPrototype?.ID != FoldedPrototypeId)
                return;

            if (!TryComp<PhysicsComponent>(uid, out var body))
                return;

            if (!_anchorable.TileFree(args.ClickLocation, body))
            {
                _popup.PopupClient("Здесь нельзя установить миномет.", uid, args.User);
                return;
            }

            var ev = new MortarDeployDoAfterEvent();
            var args2 = new DoAfterArgs(EntityManager, args.User, DeployDelay, ev, uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            };

            if (_doAfter.TryStartDoAfter(args2))
            {
                _popup.PopupClient("Установка миномета...", uid, args.User);
                args.Handled = true;
            }
        }

        private void OnDeployDoAfter(EntityUid uid, ItemComponent item, MortarDeployDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            if (MetaData(uid).EntityPrototype?.ID != FoldedPrototypeId)
                return;

            var coords = _transform.GetMoverCoordinates(uid);
            var deployed = Spawn(DeployedPrototypeId, coords);

            if (!Transform(deployed).Anchored)
                _transform.AnchorEntity(deployed);

            QueueDel(uid);
            args.Handled = true;
        }

        private void OnInteractUsing(EntityUid uid, MortarComponent component, InteractUsingEvent args)
        {
            if (component.Loaded)
            {
                _popup.PopupEntity("Миномет уже заряжен.", uid, args.User);
                return;
            }

            if (MetaData(args.Used).EntityPrototype?.ID != component.AllowedShellPrototype)
                return;

            _hands.TryDrop(args.User, args.Used);
            Del(args.Used);

            component.Loaded = true;
            args.Handled = true;

            _popup.PopupEntity("Вы зарядили миномет фугасным снарядом.", uid, args.User);
        }

        private void OnFireMessage(EntityUid uid, MortarComponent component, MortarFireMessage fireMsg)
        {
            var now = (float)_timing.CurTime.TotalSeconds;

            if (now < component.NextFireTime)
                return;

            if (!component.Loaded)
            {
                _popup.PopupEntity("Миномет не заряжен! Нужен фугасный снаряд миномета.", uid, fireMsg.Actor);
                return;
            }

            var mortarPos = _transform.GetMapCoordinates(uid);
            var target = new MapCoordinates(new Vector2(fireMsg.TargetX, fireMsg.TargetY), mortarPos.MapId);

            if (component.Scatter > 0f)
            {
                var angle = _random.NextFloat(0f, MathF.Tau);
                var dist = _random.NextFloat(0f, component.Scatter);
                target = new MapCoordinates(
                    target.Position + new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist),
                    target.MapId);
            }

            _ui.CloseUi(uid, MortarUiKey.Key, fireMsg.Actor);

            component.Loaded = false;
            component.NextFireTime = now + component.Cooldown;

            _audio.PlayPvs(component.FireSound, uid);

            _appearance.SetData(uid, MortarVisuals.Firing, true);
            Timer.Spawn(500, () => _appearance.SetData(uid, MortarVisuals.Firing, false));

            var whistleDelay = TimeSpan.FromSeconds(component.WhistleDelay);
            var impactDelay = TimeSpan.FromSeconds(component.FireDelay);

            Timer.Spawn(whistleDelay, () =>
            {
                if (!_maps.MapExists(target.MapId))
                    return;

                var whistleCoords = _transform.ToCoordinates(_maps.GetMap(target.MapId), target);
                _audio.PlayPvs(component.IncomingSound, whistleCoords);
            });

            Timer.Spawn(impactDelay, () =>
            {
                if (!_maps.MapExists(target.MapId))
                    return;

                _explosion.QueueExplosion(
                    target,
                    "Default",
                    totalIntensity: 40f,
                    slope: 4f,
                    maxTileIntensity: 10f,
                    cause: null,
                    tileBreakScale: 0f,
                    maxTileBreak: 0);
            });
        }
    }
}
