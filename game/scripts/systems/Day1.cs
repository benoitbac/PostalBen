using Godot;

namespace PostalBen.Systems;

/// <summary>
/// Day 1 of the week: get milk, cash the paycheck. Both are trivially completable and
/// both are designed to be obstructed by other people rather than by enemies - the
/// escalation has to come from the player, never from the level.
/// </summary>
public partial class Day1 : Node3D
{
    private ErrandLog _errands = null!;
    private GameState _state = null!;

    public override void _Ready()
    {
        _errands = GetNode<ErrandLog>("/root/ErrandLog");
        _state = GetNode<GameState>("/root/GameState");

        _errands.ClearForNewDay();
        GetNode<NotorietySystem>("/root/Notoriety").Reset();
        GetNode<Inventory>("/root/Inventory").ClearForNewDay();

        foreach (var errand in BuildErrands())
            _errands.Add(errand);

        _errands.AllRequiredComplete += OnListCleared;
        _state.DayEnded += OnDayEnded;

        _state.StartDay(1);
        GetNode<Audio.VoiceBank>("/root/VoiceBank").Say("ben.wake.day1");
    }

    private static ErrandLog.Errand[] BuildErrands() => new[]
    {
        new ErrandLog.Errand
        {
            Id = "milk",
            NameKey = "ui.errand.milk.name",
            DescriptionKey = "ui.errand.milk.desc",
            AnnounceVoiceKey = "ben.errand.milk.start",
            CompleteVoiceKey = "ben.errand.milk.done",
            Stages = new[]
            {
                new ErrandLog.Stage
                {
                    CompletionToken = "shop.entered",
                    HintKey = "ui.errand.milk.desc",
                    VoiceKey = "ben.errand.milk.arrive",
                    MarkerPosition = new Vector3(28f, 0f, -14f),
                },
                new ErrandLog.Stage
                {
                    CompletionToken = "milk.acquired",
                    HintKey = "ui.interact.pickup",
                    VoiceKey = "ben.errand.milk.queue",
                },
                // Paying and walking out both emit "shop.settled" - the shoplifting
                // route is a real route, it just costs notoriety instead of money.
                new ErrandLog.Stage
                {
                    CompletionToken = "shop.settled",
                    HintKey = "ui.interact.pay",
                },
            },
        },
        new ErrandLog.Errand
        {
            Id = "paycheck",
            NameKey = "ui.errand.paycheck.name",
            DescriptionKey = "ui.errand.paycheck.desc",
            AnnounceVoiceKey = "ben.errand.paycheck.start",
            CompleteVoiceKey = "ben.errand.paycheck.done",
            Stages = new[]
            {
                new ErrandLog.Stage
                {
                    CompletionToken = "bank.entered",
                    HintKey = "ui.errand.paycheck.desc",
                    VoiceKey = "ben.errand.paycheck.queue",
                    MarkerPosition = new Vector3(-34f, 0f, 22f),
                },
                new ErrandLog.Stage
                {
                    CompletionToken = "bank.form_taken",
                    HintKey = "ui.interact.talk",
                    VoiceKey = "ben.errand.paycheck.form",
                },
                new ErrandLog.Stage
                {
                    CompletionToken = "cheque.cashed",
                    HintKey = "ui.interact.queue",
                },
            },
        },
    };

    private void OnListCleared()
    {
        GetNode<Audio.VoiceBank>("/root/VoiceBank").Say("ben.errand.all_done");
    }

    private void OnDayEnded(int day)
    {
        GD.Print($"[Day{day}] ended - pacifist: {_state.PacifistToday}, kills: {_state.KillsToday}");
    }
}
