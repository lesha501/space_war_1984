using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Trench
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class InsideTrenchComponent : Component
    {
        [DataField("trenchEntity"), AutoNetworkedField]
        public EntityUid TrenchEntity;
    }
}
