using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared.Trench
{
    [Serializable, NetSerializable]
    public sealed partial class ClimbIntoTrenchDoAfterEvent : SimpleDoAfterEvent
    {
    }

    [Serializable, NetSerializable]
    public sealed partial class ClimbOutTrenchDoAfterEvent : SimpleDoAfterEvent
    {
        [DataField("targetGrid")]
        public NetEntity TargetGrid;

        [DataField("targetPos")]
        public Vector2 TargetPosition;

        public ClimbOutTrenchDoAfterEvent(NetEntity targetGrid, Vector2 targetPosition)
        {
            TargetGrid = targetGrid;
            TargetPosition = targetPosition;
        }
    }
}
