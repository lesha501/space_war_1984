using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using System.Linq;

namespace Content.Shared.WesternFront
{
    public sealed class BulletSpeedSystem : EntitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            
            SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefresh);
        }

        private void OnGunRefresh(EntityUid uid, GunComponent comp, ref GunRefreshModifiersEvent args)
        {
            args.ProjectileSpeed *= 2f;
        }
    }
}
