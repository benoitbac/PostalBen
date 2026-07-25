using Godot;
using System;
using System.Collections.Generic;

namespace PostalBen.Audio;

/// <summary>
/// Owns the mixer. Everything that makes noise routes through a named bus so the
/// player's volume sliders and the "duck music under dialogue" rule live in one place.
/// </summary>
public partial class AudioDirector : Node
{
    public enum Bus { Master, Voice, Sfx, Music, Ambience }

    /// <summary>How far music drops while a voice line is playing.</summary>
    private const float DuckDb = -12f;
    private const float DuckAttack = 0.15f;
    private const float DuckRelease = 0.6f;

    private readonly Dictionary<Bus, int> _busIndex = new();
    private readonly Dictionary<Bus, float> _userVolume = new();

    private int _activeVoiceLines;
    private float _currentDuck;
    private float _targetDuck;

    public override void _Ready()
    {
        foreach (Bus bus in Enum.GetValues<Bus>())
        {
            var index = AudioServer.GetBusIndex(bus.ToString());
            if (index < 0)
            {
                GD.PushError($"[Audio] bus '{bus}' missing from the bus layout");
                continue;
            }
            _busIndex[bus] = index;
            _userVolume[bus] = 1f;
        }

        LoadSettings();

        var voice = GetNode<VoiceBank>("/root/VoiceBank");
        voice.LineStarted += (_, _, _) => { _activeVoiceLines++; _targetDuck = DuckDb; };
        voice.LineFinished += _ =>
        {
            _activeVoiceLines = Math.Max(0, _activeVoiceLines - 1);
            if (_activeVoiceLines == 0)
                _targetDuck = 0f;
        };
    }

    public override void _Process(double delta)
    {
        if (Mathf.IsEqualApprox(_currentDuck, _targetDuck))
            return;

        // Duck fast so dialogue is never buried, recover slowly so it doesn't pump.
        var rate = _targetDuck < _currentDuck ? DuckAttack : DuckRelease;
        _currentDuck = Mathf.MoveToward(_currentDuck, _targetDuck, (float)delta * Math.Abs(DuckDb) / rate);
        ApplyVolume(Bus.Music);
    }

    /// <summary>Sets a bus volume from a 0..1 slider value.</summary>
    public void SetVolume(Bus bus, float linear)
    {
        _userVolume[bus] = Mathf.Clamp(linear, 0f, 1f);
        ApplyVolume(bus);
        Persist(bus);
    }

    public float GetVolume(Bus bus) =>
        _userVolume.TryGetValue(bus, out var v) ? v : 1f;

    private void ApplyVolume(Bus bus)
    {
        if (!_busIndex.TryGetValue(bus, out var index))
            return;

        var linear = _userVolume[bus];
        if (linear <= 0.0001f)
        {
            AudioServer.SetBusMute(index, true);
            return;
        }

        AudioServer.SetBusMute(index, false);
        var db = Mathf.LinearToDb(linear);
        if (bus == Bus.Music)
            db += _currentDuck;

        AudioServer.SetBusVolumeDb(index, db);
    }

    private void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Systems.LocaleManager.ConfigPath) != Error.Ok)
        {
            foreach (var bus in _busIndex.Keys)
                ApplyVolume(bus);
            return;
        }

        foreach (Bus bus in Enum.GetValues<Bus>())
        {
            if (!_busIndex.ContainsKey(bus))
                continue;
            _userVolume[bus] = (float)cfg.GetValue("audio", bus.ToString().ToLowerInvariant(), 1f);
            ApplyVolume(bus);
        }
    }

    private void Persist(Bus bus)
    {
        var cfg = new ConfigFile();
        cfg.Load(Systems.LocaleManager.ConfigPath);
        cfg.SetValue("audio", bus.ToString().ToLowerInvariant(), _userVolume[bus]);
        cfg.Save(Systems.LocaleManager.ConfigPath);
    }
}
