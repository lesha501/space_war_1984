using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.FpvDrone;

public static class FpvDroneConstants
{
    public const string ShaderId = "FpvDroneShader";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FpvDroneComponent : Component
{
    [DataField] [AutoNetworkedField] public EntityUid Control;
    [DataField] public EntityUid? EjectAction;
    [DataField] public string EjectActionPrototypeId = "ActionFpvDroneEject";

    [DataField] public SoundSpecifier? FlyingLoopSound =
        new SoundPathSpecifier("/Audio/_WF/FpvDrone/drone_fly_loop.ogg");

    [DataField] public float FlyingSoundMinSpeed = 0.1f;

    public EntityUid? FlyingStream;
    [DataField] [AutoNetworkedField] public float MaxRange = 50f;
    [DataField] public EntityUid? Pilot;
    [DataField] [AutoNetworkedField] public bool SignalLost;

    [DataField] public SoundSpecifier? SignalLostSound =
        new SoundPathSpecifier("/Audio/_WF/FpvDrone/drone_signal_lost.ogg");

    [DataField] public int MinShotsToDestroy = 1;

    [DataField] public int MaxShotsToDestroy = 10;

    /// <summary>
    ///     Hits in this range are the most likely to destroy the drone.
    /// </summary>
    [DataField] public int PreferredMinShotsToDestroy = 2;

    [DataField] public int PreferredMaxShotsToDestroy = 3;

    /// <summary>
    ///     Relative weight for preferred hit counts.
    /// </summary>
    [DataField] public float PreferredShotsWeight = 4f;

    /// <summary>
    ///     Relative weight for destroying the drone on the first aimed hit.
    /// </summary>
    [DataField] public float OneShotDestroyWeight = 0.75f;

    public int ShotsUntilDestroy;
    public int ShotsTaken;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FpvDroneExplosiveComponent : Component
{
    [DataField] public EntityUid? ExplodeActionEntity;
    [DataField] public EntProtoId? ExplodeActionId = "ActionFpvDroneExplosive";
    [DataField] [AutoNetworkedField] public float Radius = 5f;
    [DataField] [AutoNetworkedField] public float TotalIntensity = 50f;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FpvDroneFoldableComponent : Component
{
    [DataField] [AutoNetworkedField] public float UnfoldDelay = 1.5f;

    [DataField] [AutoNetworkedField] public EntProtoId UnfoldEntity = "FpvDroneObserver";
}
