using Godot;
using System.Collections.Generic;
using PostalBen.Systems;

namespace PostalBen.Audio;

/// <summary>
/// Resolves a voice-line key to an actual clip in the active language.
///
/// Layout on disk — one folder per locale, identical key set:
///   assets/audio/vo/en/ben.errand.milk.start.ogg
///   assets/audio/vo/fr/ben.errand.milk.start.ogg
///
/// A missing clip is never fatal: we fall back to English, and if that is missing
/// too we still fire the subtitle so the line lands. That is what lets Ben record
/// French one take at a time without the build ever breaking.
/// </summary>
public partial class VoiceBank : Node
{
    private const string VoRoot = "res://assets/audio/vo";

    /// <summary>Concurrent VO voices. Barks from a crowd shouldn't cut Ben off.</summary>
    private const int PlayerCount = 6;

    [Signal]
    public delegate void LineStartedEventHandler(string key, string subtitle, string speaker);

    [Signal]
    public delegate void LineFinishedEventHandler(string key);

    private readonly List<AudioStreamPlayer> _pool = new();
    private readonly Dictionary<string, AudioStream?> _cache = new();

    public override void _Ready()
    {
        for (int i = 0; i < PlayerCount; i++)
        {
            var player = new AudioStreamPlayer { Bus = "Voice", Name = $"VoVoice{i}" };
            AddChild(player);
            _pool.Add(player);
        }

        // Cached streams are locale-specific, so drop them when the language changes.
        GetNode<LocaleManager>("/root/Locale").LocaleChanged += _ => _cache.Clear();
    }

    /// <summary>
    /// Plays a line and raises its subtitle. <paramref name="key"/> is both the audio
    /// filename and the translation key for the subtitle, so text and audio can never
    /// drift apart.
    /// </summary>
    public void Say(string key, string speaker = "ben")
    {
        var subtitle = TranslationServer.Translate(key);
        EmitSignal(SignalName.LineStarted, key, subtitle, speaker);

        var stream = Resolve(key);
        if (stream is null)
        {
            // No audio yet for this line — subtitle carries it. Expected during
            // production while VO is still being recorded.
            GD.PushWarning($"[VO] no clip for '{key}' in any locale; subtitle only");
            EmitSignal(SignalName.LineFinished, key);
            return;
        }

        var player = LeaseVoice();
        player.Stream = stream;
        player.Play();
    }

    /// <summary>Looks up the clip in the active locale, then falls back to English.</summary>
    private AudioStream? Resolve(string key)
    {
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var locale = GetNode<LocaleManager>("/root/Locale").Current;
        var stream = Load(locale, key) ?? Load(LocaleManager.FallbackLocale, key);

        if (stream is not null && locale != LocaleManager.FallbackLocale && Load(locale, key) is null)
            GD.Print($"[VO] '{key}' missing in '{locale}', using '{LocaleManager.FallbackLocale}'");

        _cache[key] = stream;
        return stream;
    }

    private static AudioStream? Load(string locale, string key)
    {
        var path = $"{VoRoot}/{locale}/{key}.ogg";
        return ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;
    }

    /// <summary>
    /// Hands back a free voice, or the oldest busy one if every voice is speaking.
    /// Stealing beats silently dropping a line.
    /// </summary>
    private AudioStreamPlayer LeaseVoice()
    {
        foreach (var player in _pool)
        {
            if (!player.Playing)
                return player;
        }

        var stolen = _pool[0];
        _pool.RemoveAt(0);
        _pool.Add(stolen);
        stolen.Stop();
        return stolen;
    }
}
