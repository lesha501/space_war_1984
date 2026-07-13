using Content.Shared.Boombox;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Hands.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using System;

namespace Content.Server.Boombox
{
    public sealed class BoomboxSystem : EntitySystem
    {
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly UserInterfaceSystem _ui = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<BoomboxComponent, ComponentInit>(OnInit);
            SubscribeLocalEvent<BoomboxComponent, InteractUsingEvent>(OnInteractUsing);
            SubscribeLocalEvent<BoomboxComponent, ActivateInWorldEvent>(OnActivate);

            // UI messages
            SubscribeLocalEvent<BoomboxComponent, BoomboxPlayMessage>(OnPlayMessage);
            SubscribeLocalEvent<BoomboxComponent, BoomboxEjectMessage>(OnEjectMessage);
            SubscribeLocalEvent<BoomboxComponent, BoomboxSeekMessage>(OnSeekMessage);
            SubscribeLocalEvent<BoomboxComponent, BoomboxVolumeMessage>(OnVolumeMessage);
        }

        private void OnInit(EntityUid uid, BoomboxComponent component, ComponentInit args)
        {
            _container.EnsureContainer<ContainerSlot>(uid, "cassette_container");
        }

        private void OnInteractUsing(EntityUid uid, BoomboxComponent component, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            if (!TryComp<CassetteComponent>(args.Used, out var cassette))
                return;

            var container = _container.EnsureContainer<ContainerSlot>(uid, "cassette_container");
            if (container.ContainedEntity != null)
            {
                _popup.PopupEntity("В бумбоксе уже есть кассета!", uid, args.User);
                return;
            }

            if (_container.Insert(args.Used, container))
            {
                component.InsertedCassette = GetNetEntity(args.Used);
                component.TrackName = cassette.TrackName;
                component.TrackDuration = cassette.Duration;
                component.SoundPath = cassette.SoundPath;
                component.CurrentTime = 0f;
                component.IsPlaying = false;
                Dirty(uid, component);

                _popup.PopupEntity("Вы вставили кассету в бумбокс.", uid, args.User);
                UpdateUserInterface(uid, component);
                args.Handled = true;
            }
        }

        private void OnActivate(EntityUid uid, BoomboxComponent component, ActivateInWorldEvent args)
        {
            if (args.Handled || !args.Complex)
                return;

            _ui.TryOpenUi(uid, BoomboxUiKey.Key, args.User);
            args.Handled = true;
        }

        private void OnPlayMessage(EntityUid uid, BoomboxComponent component, BoomboxPlayMessage args)
        {
            var container = _container.EnsureContainer<ContainerSlot>(uid, "cassette_container");
            if (container.ContainedEntity == null)
                return;

            component.IsPlaying = !component.IsPlaying;
            Dirty(uid, component);
            UpdateUserInterface(uid, component);
        }

        private void OnEjectMessage(EntityUid uid, BoomboxComponent component, BoomboxEjectMessage args)
        {
            var container = _container.EnsureContainer<ContainerSlot>(uid, "cassette_container");
            if (container.ContainedEntity is not { } cassette)
                return;

            component.IsPlaying = false;
            component.InsertedCassette = null;
            component.TrackName = "";
            component.TrackDuration = 0f;
            component.SoundPath = "";
            component.CurrentTime = 0f;
            Dirty(uid, component);

            if (_container.Remove(cassette, container))
            {
                _hands.PickupOrDrop(args.Actor, cassette);
                _popup.PopupEntity("Вы извлекли кассету.", uid, args.Actor);
            }

            UpdateUserInterface(uid, component);
        }

        private void OnSeekMessage(EntityUid uid, BoomboxComponent component, BoomboxSeekMessage args)
        {
            var container = _container.EnsureContainer<ContainerSlot>(uid, "cassette_container");
            if (container.ContainedEntity == null)
                return;

            component.CurrentTime = Math.Clamp(args.SeekTime, 0f, component.TrackDuration);
            Dirty(uid, component);
            UpdateUserInterface(uid, component);
        }

        private void OnVolumeMessage(EntityUid uid, BoomboxComponent component, BoomboxVolumeMessage args)
        {
            component.Volume = Math.Clamp(args.Volume, 0f, 1f);
            Dirty(uid, component);
            UpdateUserInterface(uid, component);
        }

        private void UpdateUserInterface(EntityUid uid, BoomboxComponent component)
        {
            var container = _container.EnsureContainer<ContainerSlot>(uid, "cassette_container");
            var hasCassette = container.ContainedEntity != null;

            var state = new BoomboxBoundUserInterfaceState(
                component.IsPlaying,
                component.CurrentTime,
                component.TrackName,
                component.TrackDuration,
                hasCassette,
                component.Volume
            );

            _ui.SetUiState(uid, BoomboxUiKey.Key, state);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<BoomboxComponent>();
            while (query.MoveNext(out var uid, out var component))
            {
                if (!component.IsPlaying)
                    continue;

                component.CurrentTime += frameTime;
                if (component.CurrentTime >= component.TrackDuration)
                {
                    // Track finished, stop playing
                    component.CurrentTime = 0f;
                    component.IsPlaying = false;
                }

                Dirty(uid, component);
                UpdateUserInterface(uid, component);
            }
        }
    }
}
