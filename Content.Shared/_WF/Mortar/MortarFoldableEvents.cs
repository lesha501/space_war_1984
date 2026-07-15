using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Mortar;

[Serializable, NetSerializable]
public sealed partial class MortarFoldDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public sealed partial class MortarDeployDoAfterEvent : SimpleDoAfterEvent
{
}

[Serializable, NetSerializable]
public enum MortarVisuals : byte
{
    Firing
}
