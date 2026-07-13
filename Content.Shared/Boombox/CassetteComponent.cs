using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Boombox
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class CassetteComponent : Component
    {
        [DataField("trackName"), AutoNetworkedField]
        public string TrackName = "Неизвестный трек";

        [DataField("soundPath"), AutoNetworkedField]
        public string SoundPath = "";

        [DataField("duration"), AutoNetworkedField]
        public float Duration = 0f;
    }
}
