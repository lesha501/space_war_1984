using Content.Server.Atlanta.GameTicking.Rules.Components;
using Content.Server.Audio;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.KillTracking;
using Content.Server.Mind;
using Content.Server.RoundEnd;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Content.Server.Parallax;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Content.Server.Station.Systems;
using Content.Shared.Station.Components;
using Content.Server.Spawners.EntitySystems;
using Robust.Shared.Prototypes;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using System.Linq;
using System.Numerics;
using Content.Server.Spawners.Components;
namespace Content.Server.Atlanta.GameTicking.Rules;

public sealed class WesternFrontRuleSystem : GameRuleSystem<WesternFrontRuleComponent>
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _globalSound = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawning = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        SubscribeLocalEvent<PlayerSpawningEvent>(OnPlayerSpawning, before: new []{ typeof(SpawnPointSystem) });
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var query = EntityQueryEnumerator<WesternFrontRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var wf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            var mapId = _ticker.DefaultMap;
            if (!_map.MapExists(mapId))
                continue;

            var mapUid = _map.GetMapOrInvalid(mapId);

            // Biome generation removed to allow ready-made map loading

            EntityUid? stationUid = null;
            var stationQuery = EntityQueryEnumerator<StationDataComponent>();
            while (stationQuery.MoveNext(out var station, out _))
            {
                if (Prototype(station)?.ID == "WesternFrontStation")
                {
                    stationUid = station;
                    break;
                }
            }

            if (stationUid == null)
            {
                stationUid = Spawn("WesternFrontStation", MapCoordinates.Nullspace);
            }

            if (stationUid != null)
            {
                var spawnPointQuery = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
                while (spawnPointQuery.MoveNext(out var spUid, out _, out var xform))
                {
                    if (xform.MapID == mapId)
                    {
                        _stationSystem.SetStation(spUid, stationUid.Value);
                    }
                }
            }
        }
    }

    // ── Таймер грейс-периода ──────────────────────────────────────────────────

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WesternFrontRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var wf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            if (wf.GameState != WesternFrontGameState.WaitingForBattle)
                continue;

            wf.GraceTimeRemaining -= TimeSpan.FromSeconds(frameTime);

            if (wf.GraceTimeRemaining <= TimeSpan.Zero)
            {
                wf.GameState = WesternFrontGameState.InProgress;
            }
        }
    }

    // ── Регистрация игрока при спавне ─────────────────────────────────────────

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        var query = EntityQueryEnumerator<WesternFrontRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var wf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            var mob = ev.Mob;

            EnsureComp<KillTrackerComponent>(mob);

            if (!_mind.TryGetMind(mob, out var mindId, out var mind))
                continue;

            if (!_jobs.MindTryGetJob(mindId, out var jobProto))
                continue;

            var jobId = jobProto.ID;
            var name  = mind.CharacterName ?? MetaData(mob).EntityName;

            if (wf.RusJobIds.Contains(jobId))
            {
                wf.AliveRus.Add(mob);
                wf.AllPlayers.Add((mindId, name, "rus"));
            }
            else if (wf.UkrJobIds.Contains(jobId))
            {
                wf.AliveUkr.Add(mob);
                wf.AllPlayers.Add((mindId, name, "ukr"));
            }
        }
    }

    // ── Обработка смерти ─────────────────────────────────────────────────────

    private void OnKillReported(ref KillReportedEvent ev)
    {
        var dead = ev.Entity;

        var query = EntityQueryEnumerator<WesternFrontRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var wf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            if (wf.GameState == WesternFrontGameState.Ended)
                continue;

            var wasRus = wf.AliveRus.Remove(dead);
            var wasUkr = wf.AliveUkr.Remove(dead);

            if (!wasRus && !wasUkr)
                continue;

            if (wf.GameState == WesternFrontGameState.WaitingForBattle)
                continue;

            CheckWinCondition(uid, wf);
        }
    }

    // ── Проверка условия победы ───────────────────────────────────────────────

    private void CheckWinCondition(EntityUid ruleUid, WesternFrontRuleComponent wf)
    {
        var rusAlive = wf.AliveRus.Count > 0;
        var ukrAlive = wf.AliveUkr.Count > 0;

        if (rusAlive && ukrAlive)
            return;

        wf.GameState = WesternFrontGameState.Ended;

        if (!rusAlive && !ukrAlive)
        {
            _chat.DispatchServerAnnouncement("Обе команды уничтожены одновременно! Ничья!", Color.Orange);
            _globalSound.PlayAdminGlobal(Filter.Broadcast(), _audio.GetSound(wf.DrawSound), AudioParams.Default);
        }
        else
        {
            var winner = rusAlive ? "ВС РФ" : "ВСУ";
            var loser  = rusAlive ? "ВСУ" : "ВС РФ";

            _chat.DispatchServerAnnouncement($"Команда {winner} уничтожила {loser}! Победа за {winner}!", Color.Gold);
            _globalSound.PlayAdminGlobal(Filter.Broadcast(), _audio.GetSound(wf.WinSound), AudioParams.Default);
        }

        _chat.DispatchServerAnnouncement("Итоги раунда:");
        foreach (var (_, name, team) in wf.AllPlayers)
        {
            var teamLabel = team == "rus" ? "ВС РФ" : "ВСУ";
            _chat.DispatchServerAnnouncement($"  [{teamLabel}] {name}");
        }

        var roundEnd = EntityManager.EntitySysManager.GetEntitySystem<RoundEndSystem>();
        roundEnd.EndRound(wf.RestartDelay);
    }

    private void OnPlayerSpawning(PlayerSpawningEvent args)
    {
        if (args.SpawnResult != null)
            return;

        // Check if there are any spawn points for this job on the station
        var queryPoints = EntityQueryEnumerator<SpawnPointComponent, TransformComponent>();
        bool hasSpawnPoint = false;
        while (queryPoints.MoveNext(out var spUid, out var spawnPoint, out var xform))
        {
            if (spawnPoint.Job == args.Job && _stationSystem.GetOwningStation(spUid, xform) == args.Station)
            {
                hasSpawnPoint = true;
                break;
            }
        }

        if (hasSpawnPoint)
            return;

        var query = EntityQueryEnumerator<WesternFrontRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var wf, out var rule))
        {
            if (!GameTicker.IsGameRuleActive(uid, rule))
                continue;

            if (args.Job == null)
                continue;

            EntityCoordinates spawnLoc;
            var mapId = _ticker.DefaultMap;
            var mapUid = _map.GetMapOrInvalid(mapId);

            if (wf.RusJobIds.Contains(args.Job))
            {
                spawnLoc = new EntityCoordinates(mapUid, new Vector2(0, 0));
            }
            else if (wf.UkrJobIds.Contains(args.Job))
            {
                spawnLoc = new EntityCoordinates(mapUid, new Vector2(250, 0));
            }
            else if (args.Job == "WFReporter")
            {
                spawnLoc = new EntityCoordinates(mapUid, new Vector2(125, 0));
            }
            else
            {
                continue;
            }

            args.SpawnResult = _stationSpawning.SpawnPlayerMob(
                spawnLoc,
                args.Job,
                args.HumanoidCharacterProfile,
                args.Station);
            return;
        }
    }
}
