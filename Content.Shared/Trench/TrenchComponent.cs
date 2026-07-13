using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Trench
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class TrenchComponent : Component
    {
        [DataField("north"), AutoNetworkedField]
        public bool North;

        [DataField("south"), AutoNetworkedField]
        public bool South;

        [DataField("east"), AutoNetworkedField]
        public bool East;

        [DataField("west"), AutoNetworkedField]
        public bool West;
    }
}
