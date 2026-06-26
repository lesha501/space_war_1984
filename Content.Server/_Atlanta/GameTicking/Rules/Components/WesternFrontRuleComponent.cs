using Robust.Shared.Audio;

namespace Content.Server.Atlanta.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class WesternFrontRuleComponent : Component
{
    [DataField]
    public WesternFrontGameState GameState = WesternFrontGameState.WaitingForBattle;

    /// <summary>Живые игроки команды ВСРФ.</summary>
    [DataField]
    public List<EntityUid> AliveRus = new();

    /// <summary>Живые игроки команды ВСУ.</summary>
    [DataField]
    public List<EntityUid> AliveUkr = new();

    /// <summary>Все участники (имя + команда) для итогового отчёта.</summary>
    [DataField]
    public List<(EntityUid mindId, string name, string team)> AllPlayers = new();

    /// <summary>Грейс-период перед началом подсчёта живых (2 минуты).</summary>
    [DataField]
    public TimeSpan GraceTimeRemaining = TimeSpan.FromMinutes(2);

    /// <summary>Задержка перезапуска раунда после объявления победителя.</summary>
    [DataField]
    public TimeSpan RestartDelay = TimeSpan.FromSeconds(30);

    /// <summary>Job-ID ролей команды ВСУ.</summary>
    [DataField]
    public HashSet<string> UkrJobIds = new()
    {
        "WFUkrRifleman",
        "WFUkrMachinegunner",
        "WFUkrCommander",
    };

    /// <summary>Job-ID ролей команды ВСРФ.</summary>
    [DataField]
    public HashSet<string> RusJobIds = new()
    {
        "WFRusDraftee",
        "WFRusRifleman",
        "WFRusMachinegunner",
        "WFRusCommander",
    };

    [DataField]
    public SoundSpecifier WinSound = new SoundPathSpecifier("/Audio/Misc/fanfare.ogg");

    [DataField]
    public SoundSpecifier DrawSound = new SoundPathSpecifier("/Audio/Effects/error.ogg");
}

public enum WesternFrontGameState
{
    /// <summary>Грейс-период — смерти не считаются.</summary>
    WaitingForBattle,
    /// <summary>Бой идёт — смерти отслеживаются.</summary>
    InProgress,
    /// <summary>Раунд завершён.</summary>
    Ended,
}
