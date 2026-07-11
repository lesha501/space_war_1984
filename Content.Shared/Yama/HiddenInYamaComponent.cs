using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Yama
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
    public sealed partial class HiddenInYamaComponent : Component
    {
        [DataField("yamaEntity"), AutoNetworkedField]
        public EntityUid YamaEntity;
    }
}
