
using Content.Shared.Interaction;
using Content.Shared.Mortar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using System;
using System.Numerics;

namespace Content.Server.Mortar
{
    public sealed class MortarSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _gameTiming = default!;
        [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
        [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
        [Dependency] private readonly ExplosionSystem _explosionSystem = default!;
        [Dependency] private readonly PopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MortarComponent, MortarFireMessage>(OnBuiMessageReceived);

            SubscribeLocalEvent<MortarComponent, InteractUsingEvent>(OnInteractUsing);
        }

        private void OnInteractUsing(EntityUid uid, MortarComponent component, InteractUsingEvent args)
        {
            if (component.Loaded)
            {
                _popupSystem.PopupEntity("Миномет уже заряжен.", uid, args.User);
                return;
            }

            var meta = MetaData(args.Used);

            if (meta.EntityPrototype?.ID != component.AllowedShellPrototype)
                return;

            _handsSystem.TryDrop(args.User, args.Used);
            Del(args.Used);

            component.Loaded = true;
            args.Handled = true;

            _popupSystem.PopupEntity("Вы зарядили миномет фугасным снарядом.", uid, args.User);
        }

        private void OnBuiMessageReceived(EntityUid uid, MortarComponent component, MortarFireMessage fireMsg)
        {
            var currentTime = (float) _gameTiming.CurTime.TotalSeconds;

            if (currentTime < component.NextFireTime) return;

            if (!component.Loaded)
            {
                _popupSystem.PopupEntity("Миномет не заряжен! Нужен фугасный снаряд миномета.", uid, fireMsg.Actor);
                return;
            }

            var mortarXform = Transform(uid);
            var mortarMapPos = _transformSystem.GetMapCoordinates(uid, mortarXform);

            var targetCoordinates = new MapCoordinates(new Vector2(fireMsg.TargetX, fireMsg.TargetY), mortarMapPos.MapId);

            _uiSystem.CloseUi(uid, MortarUiKey.Key, fireMsg.Actor);

            component.Loaded = false;
            component.NextFireTime = currentTime + component.Cooldown;

            _audio.PlayPvs(component.FireSound, uid);

            var whistleDelay = TimeSpan.FromSeconds(component.WhistleDelay);
            var impactDelay = TimeSpan.FromSeconds(component.FireDelay);

            Timer.Spawn(whistleDelay, () =>
            {
                if (!_mapSystem.MapExists(targetCoordinates.MapId))
                    return;

                var whistleCoords = _transformSystem.ToCoordinates(_mapSystem.GetMap(targetCoordinates.MapId), targetCoordinates);
                _audio.PlayPvs(component.IncomingSound, whistleCoords);
            });

            Timer.Spawn(impactDelay, () =>
            {
                if (!_mapSystem.MapExists(targetCoordinates.MapId))
                    return;

                _explosionSystem.QueueExplosion(
                    targetCoordinates,
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
