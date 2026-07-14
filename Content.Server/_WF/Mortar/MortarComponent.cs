
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Mortar
{
    [RegisterComponent]
    public sealed partial class MortarComponent : Component
    {
        [DataField("loaded")]
        public bool Loaded = false;

        [DataField("cooldown")]
        public float Cooldown = 3f;

        public float NextFireTime = 0f;

        [DataField("allowedShellPrototype")]
        public string AllowedShellPrototype = "MortarShellItem";

        [DataField("fireDelay")]
        public float FireDelay = 10f;

        [DataField("whistleDelay")]
        public float WhistleDelay = 5f;

        [DataField("fireSound")]
        public SoundSpecifier? FireSound;

        [DataField("incomingSound")]
        public SoundSpecifier? IncomingSound;
    }
}
