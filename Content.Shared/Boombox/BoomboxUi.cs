using Robust.Shared.Serialization;
using System;

namespace Content.Shared.Boombox
{
    [Serializable, NetSerializable]
    public enum BoomboxUiKey : byte
    {
        Key
    }

    [Serializable, NetSerializable]
    public sealed class BoomboxBoundUserInterfaceState : BoundUserInterfaceState
    {
        public bool IsPlaying { get; }
        public float CurrentTime { get; }
        public string TrackName { get; }
        public float TrackDuration { get; }
        public bool HasCassette { get; }
        public float Volume { get; }

        public BoomboxBoundUserInterfaceState(bool isPlaying, float currentTime, string trackName, float trackDuration, bool hasCassette, float volume)
        {
            IsPlaying = isPlaying;
            CurrentTime = currentTime;
            TrackName = trackName;
            TrackDuration = trackDuration;
            HasCassette = hasCassette;
            Volume = volume;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BoomboxPlayMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class BoomboxEjectMessage : BoundUserInterfaceMessage
    {
    }

    [Serializable, NetSerializable]
    public sealed class BoomboxSeekMessage : BoundUserInterfaceMessage
    {
        public float SeekTime { get; }

        public BoomboxSeekMessage(float seekTime)
        {
            SeekTime = seekTime;
        }
    }

    [Serializable, NetSerializable]
    public sealed class BoomboxVolumeMessage : BoundUserInterfaceMessage
    {
        public float Volume { get; }

        public BoomboxVolumeMessage(float volume)
        {
            Volume = volume;
        }
    }
}
