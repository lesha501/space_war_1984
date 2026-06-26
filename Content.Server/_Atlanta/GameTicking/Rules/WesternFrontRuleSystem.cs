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

namespace Content.Server.Atlanta.GameTicking.Rules;

public sealed class WesternFrontRuleSystem : GameRuleSystem<WesternFrontRuleComponent>
{
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ServerGlobalSoundSystem _globalSound = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<KillReportedEvent>(OnKillReported);
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
}
