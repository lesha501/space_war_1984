using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System;

namespace Content.Client.Yama
{
    public sealed class AlphaFadeSystem : EntitySystem
    {
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            
            var query = EntityQueryEnumerator<AlphaFadeComponent, SpriteComponent>();
            while (query.MoveNext(out var uid, out var fade, out var sprite))
            {
                var currentAlpha = sprite.Color.A;
                
                if (Math.Abs(currentAlpha - fade.TargetAlpha) < 0.01f)
                {
                    sprite.Color = new Color(sprite.Color.R, sprite.Color.G, sprite.Color.B, fade.TargetAlpha);
                    RemComp<AlphaFadeComponent>(uid);
                    continue;
                }
                
                var newAlpha = MathHelper.Clamp(
                    currentAlpha + Math.Sign(fade.TargetAlpha - currentAlpha) * fade.FadeSpeed * frameTime,
                    0f, 1f
                );
                
                sprite.Color = new Color(sprite.Color.R, sprite.Color.G, sprite.Color.B, newAlpha);
            }
        }
    }
}
