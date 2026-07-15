using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;
using System;

namespace Content.Shared._WF.Mortar
{
    [Serializable, NetSerializable]
    public enum MortarUiKey : byte
    {
        Key
    }

    [Serializable, NetSerializable]
    public sealed class MortarFireMessage : BoundUserInterfaceMessage
    {
        public float TargetX { get; }
        public float TargetY { get; }

        public MortarFireMessage(float targetX, float targetY)
        {
            TargetX = targetX;
            TargetY = targetY;
        }
    }
}
