using Godot;
using System;
using System.Linq;

namespace PostalBen.Systems;

/// <summary>
/// The week. Tracks which day is running, the clock, and the tally the day-end report
/// is built from. Save/load lives here because every other system's state is derivable
/// from what this holds plus the errand log.
/// </summary>
public partial class GameState : Node
{
    /// <summary>In-game seconds per real second. A day runs ~25 real minutes.</summary>
    public const float TimeScale = 40f;

    public const int DayStartHour = 8;
    public const int DayEndHour = 22;

    [Signal]
    public delegate void DayStartedEventHandler(int day);

    [Signal]
    public delegate void DayEndedEventHandler(int day);

    [Signal]
    public delegate void HourPassedEventHandler(int hour);

    public int Day { get; private set; } = 1;
    public double ClockSeconds { get; private set; } = DayStartHour * 3600.0;
    public bool DayRunning { get; private set; }

    /// <summary>
    /// Kill count for the current day. The pacifist path is the game's real scoring
    /// axis, so this is checked, surfaced and saved rather than hidden in a stat block.
    /// </summary>
    public int KillsToday { get; private set; }
    public int KillsTotal { get; private set; }

    public bool PacifistToday => KillsToday == 0;
    public bool PacifistRun => KillsTotal == 0;

    public int Hour => (int)(ClockSeconds / 3600.0) % 24;
    public int Minute => (int)(ClockSeconds / 60.0) % 60;

    private int _lastSignalledHour = -1;

    public override void _Process(double delta)
    {
        if (!DayRunning)
            return;

        ClockSeconds += delta * TimeScale;

        if (Hour != _lastSignalledHour)
        {
            _lastSignalledHour = Hour;
            EmitSignal(SignalName.HourPassed, Hour);
        }

        // Shops close, the bank shuts, and the day ends whether the list is done or not.
        if (Hour >= DayEndHour)
            EndDay();
    }

    public void StartDay(int day)
    {
        Day = day;
        ClockSeconds = DayStartHour * 3600.0;
        KillsToday = 0;
        _lastSignalledHour = -1;
        DayRunning = true;
        EmitSignal(SignalName.DayStarted, day);
    }

    public void EndDay()
    {
        if (!DayRunning)
            return;
        DayRunning = false;
        EmitSignal(SignalName.DayEnded, Day);
    }

    public void RecordKill()
    {
        KillsToday++;
        KillsTotal++;
    }

    /// <summary>Formatted for the HUD clock. 24h in both locales - no am/pm to translate.</summary>
    public string ClockText => $"{Hour:D2}:{Minute:D2}";

    public void Save(int slot)
    {
        var cfg = new ConfigFile();
        cfg.SetValue("run", "day", Day);
        cfg.SetValue("run", "clock", ClockSeconds);
        cfg.SetValue("run", "kills_today", KillsToday);
        cfg.SetValue("run", "kills_total", KillsTotal);
        cfg.SetValue("run", "saved_at", DateTime.UtcNow.ToString("o"));

        var errands = GetNode<ErrandLog>("/root/ErrandLog");
        cfg.SetValue("errands", "completed", errands.CompletedIds.ToArray());

        var err = cfg.Save(SlotPath(slot));
        if (err != Error.Ok)
            GD.PushError($"[Save] slot {slot} failed: {err}");
    }

    public bool Load(int slot)
    {
        var cfg = new ConfigFile();
        if (cfg.Load(SlotPath(slot)) != Error.Ok)
            return false;

        Day = (int)cfg.GetValue("run", "day", 1);
        ClockSeconds = (double)cfg.GetValue("run", "clock", DayStartHour * 3600.0);
        KillsToday = (int)cfg.GetValue("run", "kills_today", 0);
        KillsTotal = (int)cfg.GetValue("run", "kills_total", 0);

        var completed = cfg.GetValue("errands", "completed", Array.Empty<string>()).AsStringArray();
        GetNode<ErrandLog>("/root/ErrandLog").RestoreCompleted(completed);

        _lastSignalledHour = -1;
        DayRunning = true;
        EmitSignal(SignalName.DayStarted, Day);
        return true;
    }

    public static bool SlotExists(int slot) => FileAccess.FileExists(SlotPath(slot));

    private static string SlotPath(int slot) => $"user://save{slot}.cfg";
}
