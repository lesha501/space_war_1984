using Content.Shared.Standing;
using Content.Shared.Yama;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using System.Collections.Generic;

namespace Content.Client.Yama
{
    public sealed class YamaClientSystem : EntitySystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SpriteSystem _spriteSystem = default!;

        private readonly HashSet<EntityUid> _currentlyHidden = new();

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var localPlayer = _playerManager.LocalPlayer?.ControlledEntity;
            if (localPlayer == null || !Exists(localPlayer.Value))
            {
                // If local player doesn't exist, restore everything and return
                RestoreAll();
                return;
            }

            var localUid = localPlayer.Value;
            var localPos = _transform.GetWorldPosition(localUid);
            var isLocalHidden = TryComp<HiddenInYamaComponent>(localUid, out var localHiddenComp);

            if (isLocalHidden)
            {
                EnsureComp<AlphaFadeComponent>(localUid).TargetAlpha = 0.5f;
            }
            else
            {
                if (TryComp<SpriteComponent>(localUid, out var localSprite) && localSprite.Color.A < 1f)
                {
                    EnsureComp<AlphaFadeComponent>(localUid).TargetAlpha = 1f;
                }
            }

            var shouldBeHidden = new HashSet<EntityUid>();

            var query = EntityQueryEnumerator<StandingStateComponent, SpriteComponent, TransformComponent>();
            while (query.MoveNext(out var mobUid, out var mob, out var sprite, out var mobXform))
            {
                if (mobUid == localUid)
                    continue;

                var mobPos = _transform.GetWorldPosition(mobUid);

                if (isLocalHidden)
                {
                    // If local player is in a yama, they can't see players further than 3 tiles away
                    if ((localPos - mobPos).Length() > 3f)
                    {
                        shouldBeHidden.Add(mobUid);
                    }
                }
                else
                {
                    // If other player is in a yama, they are hidden from local player unless local player is within 2 tiles of the yama
                    if (TryComp<HiddenInYamaComponent>(mobUid, out var otherHiddenComp))
                    {
                        var yamaUid = otherHiddenComp.YamaEntity;
                        if (Exists(yamaUid))
                        {
                            var yamaPos = _transform.GetWorldPosition(yamaUid);
                            if ((localPos - yamaPos).Length() > 2f)
                            {
                                shouldBeHidden.Add(mobUid);
                            }
                        }
                    }
                }
            }

            // Hide entities that should be hidden but aren't currently
            foreach (var uid in shouldBeHidden)
            {
                if (!_currentlyHidden.Contains(uid))
                {
                    if (TryComp<SpriteComponent>(uid, out var sprite))
                    {
                        // Ensure it is visible so it can be rendered, but fade it out
                        _spriteSystem.SetVisible((uid, sprite), true);
                        EnsureComp<AlphaFadeComponent>(uid).TargetAlpha = 0f;
                        _currentlyHidden.Add(uid);
                    }
                }
            }

            // Restore visibility of entities that shouldn't be hidden anymore
            var toRestore = new List<EntityUid>();
            foreach (var uid in _currentlyHidden)
            {
                if (!shouldBeHidden.Contains(uid))
                {
                    toRestore.Add(uid);
                }
            }

            foreach (var uid in toRestore)
            {
                if (Exists(uid) && TryComp<SpriteComponent>(uid, out var sprite))
                {
                    _spriteSystem.SetVisible((uid, sprite), true);
                    EnsureComp<AlphaFadeComponent>(uid).TargetAlpha = 1f;
                }
                _currentlyHidden.Remove(uid);
            }
        }

        private void RestoreAll()
        {
            foreach (var uid in _currentlyHidden)
            {
                if (Exists(uid) && TryComp<SpriteComponent>(uid, out var sprite))
                {
                    _spriteSystem.SetVisible((uid, sprite), true);
                    EnsureComp<AlphaFadeComponent>(uid).TargetAlpha = 1f;
                }
            }
            _currentlyHidden.Clear();
        }
    }
}
