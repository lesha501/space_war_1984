using Content.Shared.Trench;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Burial.Components;
using Content.Shared.DragDrop;
using Content.Shared.Physics;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System;
using System.Numerics;

namespace Content.Server.Trench
{
    public sealed class TrenchSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedMapSystem _mapSystem = default!;
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly AppearanceSystem _appearance = default!;

        public override void Initialize()
        {
            base.Initialize();

            // Digging with a shovel
            SubscribeLocalEvent<ShovelComponent, AfterInteractEvent>(OnShovelAfterInteract);
            SubscribeLocalEvent<ShovelComponent, DigTrenchDoAfterEvent>(OnDigTrenchComplete);

            // Climbing into/out of trenches
            SubscribeLocalEvent<TrenchComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
            SubscribeLocalEvent<InsideTrenchComponent, GetVerbsEvent<AlternativeVerb>>(OnPlayerAltVerb);
            SubscribeLocalEvent<TrenchComponent, CanDropTargetEvent>(OnCanDragDropOn);
            SubscribeLocalEvent<TrenchComponent, DragDropTargetEvent>(OnClimbableDragDrop);

            SubscribeLocalEvent<TrenchComponent, ClimbIntoTrenchDoAfterEvent>(OnClimbIntoComplete);
            SubscribeLocalEvent<TrenchComponent, ClimbOutTrenchDoAfterEvent>(OnClimbOutComplete);

            // Dynamic updates on creation and deletion
            SubscribeLocalEvent<TrenchComponent, MapInitEvent>(OnTrenchMapInit);
            SubscribeLocalEvent<TrenchComponent, ComponentShutdown>(OnTrenchShutdown);
        }

        private void OnTrenchMapInit(EntityUid uid, TrenchComponent component, MapInitEvent args)
        {
            var xform = Transform(uid);
            if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;

            var tile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
            UpdateAdjacency(xform.GridUid.Value, grid, tile.GridIndices);

            var dirs = new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right };
            foreach (var dir in dirs)
            {
                UpdateAdjacency(xform.GridUid.Value, grid, tile.GridIndices + dir);
            }
        }

        private void OnTrenchShutdown(EntityUid uid, TrenchComponent component, ComponentShutdown args)
        {
            var xform = Transform(uid);
            if (xform.GridUid == null || TerminatingOrDeleted(xform.GridUid.Value))
                return;

            if (!TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;

            var tile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
            var indices = tile.GridIndices;

            var dirs = new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right };
            foreach (var dir in dirs)
            {
                UpdateAdjacency(xform.GridUid.Value, grid, indices + dir, uid);
            }
        }

        private void OnShovelAfterInteract(EntityUid uid, ShovelComponent component, AfterInteractEvent args)
        {
            if (args.Handled || !args.CanReach)
                return;

            var gridUid = args.ClickLocation.GetGridUid(EntityManager);
            if (gridUid == null || !TryComp<MapGridComponent>(gridUid.Value, out var grid))
                return;

            var tile = _mapSystem.GetTileRef(gridUid.Value, grid, args.ClickLocation);
            if (tile.Tile.IsEmpty)
                return;

            // Check if there is already a trench on this tile
            if (HasTrenchOnTile(gridUid.Value, grid, tile.GridIndices))
            {
                _popupSystem.PopupEntity("Здесь уже выкопан окоп!", args.User, args.User);
                return;
            }

            var doAfter = new DoAfterArgs(EntityManager, args.User, 2.5f / component.SpeedModifier, new DigTrenchDoAfterEvent(GetNetEntity(gridUid.Value), args.ClickLocation.Position), uid, used: uid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true
            };

            // Play digging sound
            _audioSystem.PlayPvs("/Audio/Items/shovel_dig.ogg", args.User);

            _doAfterSystem.TryStartDoAfter(doAfter);
            args.Handled = true;
        }

        private void OnDigTrenchComplete(EntityUid uid, ShovelComponent component, DigTrenchDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            var gridUid = GetEntity(args.Grid);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;

            var coords = new EntityCoordinates(gridUid, args.Position);
            var tile = _mapSystem.GetTileRef(gridUid, grid, coords);
            if (tile.Tile.IsEmpty)
                return;

            if (HasTrenchOnTile(gridUid, grid, tile.GridIndices))
                return;

            // Spawn the trench at the center of the tile
            var tileCenter = _mapSystem.GridTileToLocal(gridUid, grid, tile.GridIndices);
            var trench = Spawn("Trench", tileCenter); // Trench entity

            // Add TrenchComponent if it doesn't have it (crater has Yama, let's ensure it has TrenchComponent too)
            EnsureComp<TrenchComponent>(trench);

            _popupSystem.PopupEntity("Окоп выкопан!", args.User, args.User);

            // Adjacency will be calculated automatically by MapInitEvent on trench!
        }

        private void OnGetAlternativeVerbs(EntityUid uid, TrenchComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            if (HasComp<InsideTrenchComponent>(args.User))
            {
                var verb = new AlternativeVerb
                {
                    Text = "Вылезти из окопа",
                    Act = () => StartClimbingOut(args.User, uid),
                    Priority = 100
                };
                args.Verbs.Add(verb);
            }
            else
            {
                var verb = new AlternativeVerb
                {
                    Text = "Залезть в окоп",
                    Act = () => StartClimbingInto(args.User, uid),
                    Priority = 100
                };
                args.Verbs.Add(verb);
            }
        }

        private void OnPlayerAltVerb(EntityUid uid, InsideTrenchComponent component, GetVerbsEvent<AlternativeVerb> args)
        {
            if (!args.CanInteract || !args.CanAccess)
                return;

            var verb = new AlternativeVerb
            {
                Text = "Вылезти из окопа",
                Act = () => StartClimbingOut(uid, component.TrenchEntity),
                Priority = 100
            };
            args.Verbs.Add(verb);
        }

        private void OnCanDragDropOn(EntityUid uid, TrenchComponent component, ref CanDropTargetEvent args)
        {
            if (args.Handled)
                return;

            args.CanDrop = true;
            args.Handled = true;
        }

        private void OnClimbableDragDrop(EntityUid uid, TrenchComponent component, ref DragDropTargetEvent args)
        {
            if (args.Handled)
                return;

            if (HasComp<InsideTrenchComponent>(args.Dragged))
            {
                StartClimbingOut(args.User, uid);
            }
            else
            {
                StartClimbingInto(args.User, uid, args.Dragged);
            }
            args.Handled = true;
        }

        private void StartClimbingInto(EntityUid user, EntityUid trenchUid, EntityUid? target = null)
        {
            var climber = target ?? user;
            EnsureComp<ClimbingIntoTrenchComponent>(climber);

            var doAfter = new DoAfterArgs(EntityManager, user, 1.5f, new ClimbIntoTrenchDoAfterEvent(), trenchUid, target: trenchUid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false
            };

            _doAfterSystem.TryStartDoAfter(doAfter);
        }

        private void OnClimbIntoComplete(EntityUid uid, TrenchComponent component, ClimbIntoTrenchDoAfterEvent args)
        {
            var user = args.User;
            RemComp<ClimbingIntoTrenchComponent>(user);

            if (args.Cancelled || args.Handled)
                return;

            var inside = EnsureComp<InsideTrenchComponent>(user);
            inside.TrenchEntity = uid;
            Dirty(user, inside);

            // Teleport them to the center of the trench
            var trenchXform = Transform(uid);
            _transform.SetCoordinates(user, trenchXform.Coordinates);

            _popupSystem.PopupEntity("Вы спустились в окоп.", user, user);
        }

        private void StartClimbingOut(EntityUid user, EntityUid trenchUid)
        {
            var xform = Transform(trenchUid);
            if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                return;

            var tile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);
            var indices = tile.GridIndices;

            Vector2i? targetIndices = null;
            var dirs = new[] { Vector2i.Up, Vector2i.Down, Vector2i.Left, Vector2i.Right };
            foreach (var dir in dirs)
            {
                var next = indices + dir;
                if (!HasTrenchOnTile(xform.GridUid.Value, grid, next))
                {
                    targetIndices = next;
                    break;
                }
            }

            targetIndices ??= indices + Vector2i.Up;
            var targetCoords = _mapSystem.GridTileToLocal(xform.GridUid.Value, grid, targetIndices.Value);

            var netGrid = GetNetEntity(xform.GridUid.Value);
            var doAfter = new DoAfterArgs(EntityManager, user, 1.5f, new ClimbOutTrenchDoAfterEvent(netGrid, targetCoords.Position), trenchUid, target: trenchUid)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = false
            };

            _doAfterSystem.TryStartDoAfter(doAfter);
        }

        private void OnClimbOutComplete(EntityUid uid, TrenchComponent component, ClimbOutTrenchDoAfterEvent args)
        {
            if (args.Cancelled || args.Handled)
                return;

            var user = args.User;
            RemComp<InsideTrenchComponent>(user);

            // Move the player to the target coordinate outside the trench
            var gridUid = GetEntity(args.TargetGrid);
            var targetCoords = new EntityCoordinates(gridUid, args.TargetPosition);
            _transform.SetCoordinates(user, targetCoords);

            _popupSystem.PopupEntity("Вы вылезли из окопа.", user, user);
        }

        private void UpdateAdjacency(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignoreUid = null)
        {
            var trenchUid = GetTrenchOnTile(gridUid, grid, indices, ignoreUid);
            if (trenchUid == null || !TryComp<TrenchComponent>(trenchUid.Value, out var trench))
                return;

            trench.North = HasTrenchOnTile(gridUid, grid, indices + Vector2i.Up, ignoreUid);
            trench.South = HasTrenchOnTile(gridUid, grid, indices + Vector2i.Down, ignoreUid);
            trench.East = HasTrenchOnTile(gridUid, grid, indices + Vector2i.Right, ignoreUid);
            trench.West = HasTrenchOnTile(gridUid, grid, indices + Vector2i.Left, ignoreUid);

            Dirty(trenchUid.Value, trench);

            // Build connection appearance/bitmask:
            // Bitmask: North=1, South=2, East=4, West=8
            int mask = 0;
            if (trench.North) mask |= 1;
            if (trench.South) mask |= 2;
            if (trench.East) mask |= 4;
            if (trench.West) mask |= 8;

            _appearance.SetData(trenchUid.Value, TrenchVisuals.Connections, mask);
        }

        private bool HasTrenchOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignoreUid = null)
        {
            return GetTrenchOnTile(gridUid, grid, indices, ignoreUid) != null;
        }

        private EntityUid? GetTrenchOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignoreUid = null)
        {
            var box = GetTileBox(grid, indices);
            var ents = _lookup.GetEntitiesIntersecting(gridUid, box);
            foreach (var ent in ents)
            {
                if (ent == ignoreUid)
                    continue;
                if (HasComp<TrenchComponent>(ent))
                    return ent;
            }
            return null;
        }

        private Box2 GetTileBox(MapGridComponent grid, Vector2i indices)
        {
            var tileSize = grid.TileSize;
            var pos = (Vector2)indices * tileSize;
            var size = new Vector2(tileSize, tileSize);
            return new Box2(pos, pos + size);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // 1. Players inside a trench - prevent stepping out to normal ground without climbing
            var insideQuery = EntityQueryEnumerator<InsideTrenchComponent, TransformComponent>();
            while (insideQuery.MoveNext(out var uid, out var inside, out var xform))
            {
                if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                    continue;

                var currentTile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

                if (HasTrenchOnTile(xform.GridUid.Value, grid, currentTile.GridIndices))
                {
                    inside.TrenchEntity = GetTrenchOnTile(xform.GridUid.Value, grid, currentTile.GridIndices) ?? inside.TrenchEntity;
                    continue;
                }

                // Teleport back to the center of the last valid trench tile
                if (Exists(inside.TrenchEntity))
                {
                    var trenchXform = Transform(inside.TrenchEntity);
                    _transform.SetCoordinates(uid, xform, trenchXform.Coordinates);
                    _popupSystem.PopupEntity("Вы уперлись в край окопа!", uid, uid);
                }
            }

            // 2. Players outside a trench - prevent stepping onto a trench tile without climbing
            // Optimized query: only check entities that are active moving humanoids (InputMoverComponent)
            var outsideQuery = EntityQueryEnumerator<InputMoverComponent, TransformComponent>();
            while (outsideQuery.MoveNext(out var uid, out var mover, out var xform))
            {
                if (HasComp<InsideTrenchComponent>(uid) || HasComp<ClimbingIntoTrenchComponent>(uid))
                    continue;

                if (xform.GridUid == null || !TryComp<MapGridComponent>(xform.GridUid.Value, out var grid))
                    continue;

                var currentTile = _mapSystem.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates);

                if (HasTrenchOnTile(xform.GridUid.Value, grid, currentTile.GridIndices))
                {
                    // Smooth vector-based displacement back from the tile center
                    var tileCenterLocal = _mapSystem.GridTileToLocal(xform.GridUid.Value, grid, currentTile.GridIndices);
                    var relativePos = xform.LocalPosition - tileCenterLocal.Position;

                    if (Math.Abs(relativePos.X) > Math.Abs(relativePos.Y))
                    {
                        // Horizontal displacement
                        var sign = Math.Sign(relativePos.X);
                        if (sign == 0) sign = 1;
                        xform.LocalPosition = new Vector2(tileCenterLocal.Position.X + (sign * 0.55f), xform.LocalPosition.Y);
                    }
                    else
                    {
                        // Vertical displacement
                        var sign = Math.Sign(relativePos.Y);
                        if (sign == 0) sign = 1;
                        xform.LocalPosition = new Vector2(xform.LocalPosition.X, tileCenterLocal.Position.Y + (sign * 0.55f));
                    }

                    _popupSystem.PopupEntity("Вы не можете просто наступить на окоп!", uid, uid);
                }
            }
        }
    }
}
