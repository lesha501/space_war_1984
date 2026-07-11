using Content.Shared.Standing;
using Content.Shared.Yama;
using Robust.Shared.GameObjects;

namespace Content.Server.Yama
{
    public sealed class YamaSystem : EntitySystem
    {
        [Dependency] private readonly StandingStateSystem _standing = default!;
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<YamaComponent, TransformComponent>();
            while (query.MoveNext(out var yamaUid, out var yama, out var yamaXform))
            {
                var yamaPos = _transform.GetMapCoordinates(yamaUid, yamaXform);
                foreach (var entity in _lookup.GetEntitiesInRange<StandingStateComponent>(yamaPos, 0.8f))
                {
                    if (_standing.IsDown(entity.Owner))
                    {
                        if (!HasComp<HiddenInYamaComponent>(entity.Owner))
                        {
                            var hidden = AddComp<HiddenInYamaComponent>(entity.Owner);
                            hidden.YamaEntity = yamaUid;
                            Dirty(entity.Owner, hidden);
                        }
                    }
                }
            }

            var hiddenQuery = EntityQueryEnumerator<HiddenInYamaComponent, TransformComponent>();
            while (hiddenQuery.MoveNext(out var playerUid, out var hidden, out var playerXform))
            {
                var yamaUid = hidden.YamaEntity;
                if (!Exists(yamaUid) || !HasComp<YamaComponent>(yamaUid))
                {
                    RemCompDeferred<HiddenInYamaComponent>(playerUid);
                    continue;
                }

                if (!_standing.IsDown(playerUid))
                {
                    RemCompDeferred<HiddenInYamaComponent>(playerUid);
                    continue;
                }

                var playerPos = _transform.GetWorldPosition(playerUid);
                var yamaPos = _transform.GetWorldPosition(yamaUid);
                if ((playerPos - yamaPos).Length() > 0.8f)
                {
                    RemCompDeferred<HiddenInYamaComponent>(playerUid);
                }
            }
        }
    }
}
