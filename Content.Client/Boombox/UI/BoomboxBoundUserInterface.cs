using Content.Shared.Boombox;
using Robust.Client.GameObjects;
using System;

namespace Content.Client.Boombox.UI
{
    public sealed class BoomboxBoundUserInterface : BoundUserInterface
    {
        private BoomboxWindow? _window;

        public BoomboxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _window = new BoomboxWindow();
            _window.OnClose += Close;

            _window.OnPlayPressed += () => SendMessage(new BoomboxPlayMessage());
            _window.OnEjectPressed += () => SendMessage(new BoomboxEjectMessage());
            _window.OnSeek += time => SendMessage(new BoomboxSeekMessage(time));
            _window.OnVolumeChanged += volume => SendMessage(new BoomboxVolumeMessage(volume));

            _window.OpenToLeft();
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not BoomboxBoundUserInterfaceState boomboxState)
                return;

            _window?.UpdateState(boomboxState);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _window?.Dispose();
            }
        }
    }
}
