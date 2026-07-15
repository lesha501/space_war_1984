
using Content.Shared._WF.Mortar;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using System;
using Content.Client._WF.Mortar;

namespace Content.Client.Mortar.UI
{
    public sealed class MortarBoundUserInterface : BoundUserInterface
    {
        private MortarMenu? _menu;

        public MortarBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = new MortarMenu();
            _menu.OnClose += Close;

            _menu.OnFirePressed += (x, y) =>
            {
                SendMessage(new MortarFireMessage(x, y));
            };

            _menu.OpenCentered();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _menu?.Close();
            }
        }
    }
}
