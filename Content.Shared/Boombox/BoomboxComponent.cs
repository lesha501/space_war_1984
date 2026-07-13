using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Boombox
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class BoomboxComponent : Component
    {
        [DataField("isPlaying"), AutoNetworkedField]
        public bool IsPlaying = false;

        [DataField("currentTime"), AutoNetworkedField]
        public float CurrentTime = 0f;

        [DataField("insertedCassette"), AutoNetworkedField]
        public NetEntity? InsertedCassette = null;

        [DataField("trackName"), AutoNetworkedField]
        public string TrackName = "";

        [DataField("trackDuration"), AutoNetworkedField]
        public float TrackDuration = 0f;

        [DataField("soundPath"), AutoNetworkedField]
        public string SoundPath = "";

        [DataField("volume"), AutoNetworkedField]
        public float Volume = 1.0f;
    }
}
