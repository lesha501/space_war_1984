using Content.Shared._WF.Binoculars;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._WF.Binoculars;

public sealed class BinocularsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, float> _cooldowns = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BinocularsComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, BinocularsComponent component, AfterInteractEvent args)
    {
        if (args.Handled || args.Target != null)
            return;

        if (!TryComp<WieldableComponent>(uid, out var wieldable) || !wieldable.Wielded)
            return;

        var now = (float)_timing.CurTime.TotalSeconds;
        if (_cooldowns.TryGetValue(uid, out var nextUse) && now < nextUse)
            return;

        _cooldowns[uid] = now + 1.0f;

        var mapPos = _transform.ToMapCoordinates(args.ClickLocation);
        var x = (int)MathF.Round(mapPos.Position.X);
        var y = (int)MathF.Round(mapPos.Position.Y);

        _popup.PopupCursor($"Координаты: {x}, {y}", args.User);
        args.Handled = true;
    }
}
