using Robust.Shared.GameObjects;

namespace Content.Client.Yama
{
    [RegisterComponent]
    public sealed partial class AlphaFadeComponent : Component
    {
        public float TargetAlpha = 1f;
        public float FadeSpeed = 2f; // Alpha units per second
    }
}
