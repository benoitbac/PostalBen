using Godot;
using System.Collections.Generic;
using System.Linq;

namespace PostalBen.Systems;

/// <summary>
/// The list on the fridge. Errands are data, not scripted sequences: an errand knows
/// its stages and what completes each one, and the world reports events into here.
/// That keeps "get milk" solvable by paying, stealing, or waiting out a queue without
/// three separate quest scripts.
/// </summary>
public partial class ErrandLog : Node
{
    [Signal]
    public delegate void ErrandAddedEventHandler(string id);

    [Signal]
    public delegate void StageAdvancedEventHandler(string id, int stage);

    [Signal]
    public delegate void ErrandCompletedEventHandler(string id);

    [Signal]
    public delegate void AllRequiredCompleteEventHandler();

    private readonly List<Errand> _active = new();
    private readonly HashSet<string> _completed = new();

    public IReadOnlyList<Errand> Active => _active;
    public IReadOnlyCollection<string> CompletedIds => _completed;

    public void Add(Errand errand)
    {
        if (_completed.Contains(errand.Id) || _active.Any(e => e.Id == errand.Id))
            return;

        _active.Add(errand);
        EmitSignal(SignalName.ErrandAdded, errand.Id);

        if (errand.AnnounceVoiceKey is { Length: > 0 } key)
            GetNode<Audio.VoiceBank>("/root/VoiceBank").Say(key);
    }

    /// <summary>
    /// Reports a world event. Any active errand whose current stage is waiting on this
    /// token advances. One event can move several errands - buying milk at the same
    /// counter that cashes the cheque is intentional.
    /// </summary>
    public void Notify(string token)
    {
        // Iterate over a copy: completing an errand can add a follow-up errand.
        foreach (var errand in _active.ToList())
        {
            if (errand.CurrentStage?.CompletionToken != token)
                continue;

            errand.Advance();
            EmitSignal(SignalName.StageAdvanced, errand.Id, errand.StageIndex);

            if (errand.CurrentStage?.VoiceKey is { Length: > 0 } stageKey)
                GetNode<Audio.VoiceBank>("/root/VoiceBank").Say(stageKey);

            if (!errand.IsComplete)
                continue;

            _active.Remove(errand);
            _completed.Add(errand.Id);
            EmitSignal(SignalName.ErrandCompleted, errand.Id);

            if (errand.CompleteVoiceKey is { Length: > 0 } doneKey)
                GetNode<Audio.VoiceBank>("/root/VoiceBank").Say(doneKey);
        }

        if (_active.All(e => e.Optional) && _active.Count < 1)
            EmitSignal(SignalName.AllRequiredComplete);
    }

    public bool IsComplete(string id) => _completed.Contains(id);

    public void RestoreCompleted(IEnumerable<string> ids)
    {
        _completed.Clear();
        foreach (var id in ids)
            _completed.Add(id);
        _active.RemoveAll(e => _completed.Contains(e.Id));
    }

    public void ClearForNewDay()
    {
        _active.Clear();
        _completed.Clear();
    }

    /// <summary>
    /// One task on the list. NameKey/DescriptionKey are translation keys, never text -
    /// the log renders identically in both languages with no per-locale branching.
    /// </summary>
    public sealed class Errand
    {
        public required string Id { get; init; }
        public required string NameKey { get; init; }
        public required string DescriptionKey { get; init; }
        public bool Optional { get; init; }
        public string? AnnounceVoiceKey { get; init; }
        public string? CompleteVoiceKey { get; init; }
        public required IReadOnlyList<Stage> Stages { get; init; }

        public int StageIndex { get; private set; }
        public bool IsComplete => StageIndex >= Stages.Count;
        public Stage? CurrentStage => IsComplete ? null : Stages[StageIndex];

        public void Advance() => StageIndex++;
    }

    /// <summary>
    /// A step. <see cref="CompletionToken"/> is the world event that satisfies it -
    /// several different interactions can emit the same token, which is what gives
    /// the player more than one way through.
    /// </summary>
    public sealed class Stage
    {
        public required string CompletionToken { get; init; }
        public required string HintKey { get; init; }
        public string? VoiceKey { get; init; }
        public Vector3? MarkerPosition { get; init; }
    }
}
