using Content.Shared.Boombox;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using System;
using System.Collections.Generic;

namespace Content.Client.Boombox
{
    public sealed class BoomboxClientSystem : EntitySystem
    {
        [Dependency] private readonly SharedAudioSystem _audio = default!;

        private readonly Dictionary<EntityUid, (EntityUid Stream, string Path, float StartTime, double SystemTime)> _activeStreams = new();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<BoomboxComponent, ComponentShutdown>(OnShutdown);
        }

        private void OnShutdown(EntityUid uid, BoomboxComponent component, ComponentShutdown args)
        {
            if (_activeStreams.Remove(uid, out var state))
            {
                QueueDel(state.Stream);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var now = DateTime.UtcNow.Ticks / (double)TimeSpan.TicksPerSecond;
            var query = EntityQueryEnumerator<BoomboxComponent>();
            while (query.MoveNext(out var uid, out var boombox))
            {
                if (!boombox.IsPlaying || string.IsNullOrEmpty(boombox.SoundPath))
                {
                    if (_activeStreams.Remove(uid, out var state))
                    {
                        QueueDel(state.Stream);
                    }
                    continue;
                }

                if (_activeStreams.TryGetValue(uid, out var streamState))
                {
                    if (streamState.Path != boombox.SoundPath)
                    {
                        QueueDel(streamState.Stream);
                        _activeStreams.Remove(uid);
                        StartStream(uid, boombox, boombox.CurrentTime, now);
                    }
                    else
                    {
                        // Update volume dynamically
                        var volumeDb = SharedAudioSystem.GainToVolume(boombox.Volume);
                        _audio.SetVolume(streamState.Stream, volumeDb);

                        // Check drift
                        var elapsed = (float)(now - streamState.SystemTime);
                        var estimatedPos = streamState.StartTime + elapsed;

                        if (MathF.Abs(estimatedPos - boombox.CurrentTime) > 1.2f)
                        {
                            // Out of sync or seeked! Restart stream at correct time
                            QueueDel(streamState.Stream);
                            _activeStreams.Remove(uid);
                            StartStream(uid, boombox, boombox.CurrentTime, now);
                        }
                    }
                }
                else
                {
                    StartStream(uid, boombox, boombox.CurrentTime, now);
                }
            }
        }

        private void StartStream(EntityUid uid, BoomboxComponent boombox, float offset, double systemTime)
        {
            var soundSpec = new SoundPathSpecifier(boombox.SoundPath);
            var volumeDb = SharedAudioSystem.GainToVolume(boombox.Volume);
            var audioParams = AudioParams.Default
                .WithVolume(volumeDb)
                .WithMaxDistance(12f)
                .WithReferenceDistance(1f)
                .WithRolloffFactor(2.5f)
                .WithPlayOffset(offset);

            var stream = _audio.PlayEntity(soundSpec, Filter.Local(), uid, false, audioParams);
            if (stream != null)
            {
                _activeStreams[uid] = (stream.Value.Entity, boombox.SoundPath, offset, systemTime);
            }
        }
    }
}
