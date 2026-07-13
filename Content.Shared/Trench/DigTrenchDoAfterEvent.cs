using Content.Shared.DoAfter;
using Robust.Shared.Serialization;
using System.Numerics;

namespace Content.Shared.Trench
{
    [Serializable, NetSerializable]
    public sealed partial class DigTrenchDoAfterEvent : SimpleDoAfterEvent
    {
        [DataField("grid")]
        public NetEntity Grid;

        [DataField("pos")]
        public Vector2 Position;

        public DigTrenchDoAfterEvent(NetEntity grid, Vector2 position)
        {
            Grid = grid;
            Position = position;
        }
    }
}
