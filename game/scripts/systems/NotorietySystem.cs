using Godot;
using System;

namespace PostalBen.Systems;

/// <summary>
/// How much trouble Ben is in. Heat decays on its own, so hiding and behaving is a
/// real strategy rather than a loading screen - a pacifist player should be able to
/// walk a mistake back down to Calm without reloading.
/// </summary>
public partial class NotorietySystem : Node
{
    public enum Level
    {
        /// <summary>Nobody cares. NPCs go about their day.</summary>
        Calm = 0,
        /// <summary>Someone saw something. Civilians stare and back away.</summary>
        Noticed = 1,
        /// <summary>Police dispatched to the last known position.</summary>
        Reported = 2,
        /// <summary>Active manhunt. Police sweep and shoot on sight.</summary>
        Hunted = 3,
    }

    /// <summary>Heat needed to enter each level. Index matches the enum.</summary>
    private static readonly float[] Thresholds = { 0f, 20f, 55f, 100f };

    /// <summary>Heat bled off per second while unseen.</summary>
    private const float DecayPerSecond = 2.2f;

    /// <summary>Grace period after the last witnessed act before decay kicks in.</summary>
    private const float DecayDelay = 6f;

    private const float MaxHeat = 160f;

    [Signal]
    public delegate void LevelChangedEventHandler(int level);

    public float Heat { get; private set; }
    public Level Current { get; private set; } = Level.Calm;
    public Vector3 LastKnownPosition { get; private set; }
    public bool HasLastKnownPosition { get; private set; }

    private float _sinceLastIncident;

    public override void _Process(double delta)
    {
        _sinceLastIncident += (float)delta;
        if (_sinceLastIncident < DecayDelay || Heat <= 0f)
            return;

        Heat = Mathf.Max(0f, Heat - DecayPerSecond * (float)delta);
        Reevaluate();

        // Once the trail is cold, police stop converging on where he used to be.
        if (Current == Level.Calm)
            HasLastKnownPosition = false;
    }

    /// <summary>
    /// Reports an act that someone saw. Unwitnessed acts add nothing - what matters
    /// is being seen, which is the whole joke.
    /// </summary>
    public void Report(Incident incident, Vector3 position, int witnesses = 1)
    {
        if (witnesses <= 0)
            return;

        // Extra witnesses matter, but sub-linearly: a crowd of thirty is not thirty
        // times a single passer-by, it is a busy street.
        var multiplier = 1f + Mathf.Log(witnesses) / 2f;
        Heat = Mathf.Min(MaxHeat, Heat + HeatFor(incident) * multiplier);

        LastKnownPosition = position;
        HasLastKnownPosition = true;
        _sinceLastIncident = 0f;
        Reevaluate();
    }

    /// <summary>Clears everything. Used on arrest, day end, and load.</summary>
    public void Reset()
    {
        Heat = 0f;
        _sinceLastIncident = 0f;
        HasLastKnownPosition = false;
        Reevaluate();
    }

    private static float HeatFor(Incident incident) => incident switch
    {
        Incident.Rude => 4f,
        Incident.Trespass => 8f,
        Incident.Vandalism => 14f,
        Incident.WeaponDrawn => 22f,
        Incident.Assault => 30f,
        Incident.GunshotFired => 38f,
        Incident.Kill => 70f,
        _ => 0f,
    };

    private void Reevaluate()
    {
        var level = Level.Calm;
        for (var i = Thresholds.Length - 1; i >= 0; i--)
        {
            if (Heat >= Thresholds[i])
            {
                level = (Level)i;
                break;
            }
        }

        if (level == Current)
            return;

        Current = level;
        EmitSignal(SignalName.LevelChanged, (int)level);
    }

    /// <summary>Translation key for the current level, for the HUD.</summary>
    public string CurrentLabelKey => Current switch
    {
        Level.Calm => "ui.notoriety.calm",
        Level.Noticed => "ui.notoriety.noticed",
        Level.Reported => "ui.notoriety.reported",
        Level.Hunted => "ui.notoriety.hunted",
        _ => throw new ArgumentOutOfRangeException(),
    };

    public enum Incident
    {
        Rude,
        Trespass,
        Vandalism,
        WeaponDrawn,
        Assault,
        GunshotFired,
        Kill,
    }
}
