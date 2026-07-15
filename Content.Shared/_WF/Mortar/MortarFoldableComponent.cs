using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Mortar;

[RegisterComponent]
public sealed partial class MortarFoldableComponent : Component
{
    [DataField]
    public float FoldDelay = 3f;

    [DataField]
    public EntProtoId FoldedEntity = "MortarFoldedItem";
}
