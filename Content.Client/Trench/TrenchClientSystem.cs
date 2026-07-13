using Content.Shared.Trench;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using System.Collections.Generic;

namespace Content.Client.Trench
{
    public sealed class TrenchClientSystem : EntitySystem
    {
        [Dependency] private readonly SpriteSystem _sprite = default!;
        [Dependency] private readonly AppearanceSystem _appearance = default!;

        private readonly Dictionary<EntityUid, (int DrawDepth, Color Color)> _originalStates = new();

        public override void Initialize()
        {
            base.Initialize();
            // Player visual adjustments
            SubscribeLocalEvent<InsideTrenchComponent, ComponentStartup>(OnInsideTrenchStartup);
            SubscribeLocalEvent<InsideTrenchComponent, ComponentShutdown>(OnInsideTrenchShutdown);

            // Trench sprite connection adjustments
            SubscribeLocalEvent<TrenchComponent, AppearanceChangeEvent>(OnAppearanceChange);
        }

        private void OnInsideTrenchStartup(EntityUid uid, InsideTrenchComponent component, ComponentStartup args)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                return;

            _originalStates[uid] = (sprite.DrawDepth, sprite.Color);

            // Lower draw depth to render behind floor objects / walls
            _sprite.SetDrawDepth((uid, sprite), (int)Shared.DrawDepth.DrawDepth.DeadMobs);

            // Darken the sprite to look like it is inside a shadowed trench
            sprite.Color = Color.FromHex("#888888");
        }

        private void OnInsideTrenchShutdown(EntityUid uid, InsideTrenchComponent component, ComponentShutdown args)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
                return;

            if (_originalStates.Remove(uid, out var state))
            {
                _sprite.SetDrawDepth((uid, sprite), state.DrawDepth);
                sprite.Color = state.Color;
            }
        }

        private void OnAppearanceChange(EntityUid uid, TrenchComponent component, ref AppearanceChangeEvent args)
        {
            if (args.Sprite == null)
                return;

            if (_appearance.TryGetData<int>(uid, TrenchVisuals.Connections, out var mask, args.Component))
            {
                // Map bitmask (North=1, South=2, East=4, West=8) to state names
                string state = "solo";
                switch (mask)
                {
                    case 0: state = "solo"; break;
                    case 1: state = "only_up"; break;
                    case 2: state = "only_down"; break;
                    case 4: state = "only_right"; break;
                    case 8: state = "only_left"; break;
                    case 3: state = "uo_and_down"; break;
                    case 5: state = "up_and_right"; break;
                    case 9: state = "left_and_up"; break;
                    case 6: state = "right_and_down"; break;
                    case 10: state = "left_and_down"; break;
                    case 12: state = "right_and_left"; break;
                    case 7: state = "up_right_down"; break;
                    case 11: state = "up_left_down"; break;
                    case 13: state = "up_right_left"; break;
                    case 14: state = "down_left_right"; break;
                    case 15: state = "all"; break;
                }

                args.Sprite.LayerSetState(0, state);
            }
        }
    }
}
