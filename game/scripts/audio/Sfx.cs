using Godot;
using System.Collections.Generic;

namespace PostalBen.Audio;

/// <summary>
/// Fires one-shot sound effects, positioned in the world where that matters.
///
/// Separate from VoiceBank because the rules are different: effects never carry
/// subtitles, never fall back across languages, and are cheap enough to fire dozens at
/// once. Voice is content; this is feedback.
/// </summary>
public partial class Sfx : Node
{
    private const string Root = "res://assets/audio/sfx";

    /// <summary>Concurrent positional voices. A gunfight is noisy.</summary>
    private const int PositionalVoices = 16;

    /// <summary>Non-positional voices, for sounds that happen to the player directly.</summary>
    private const int FlatVoices = 6;

    private readonly Dictionary<string, AudioStream?> _cache = new();
    private readonly List<AudioStreamPlayer3D> _world = new();
    private readonly List<AudioStreamPlayer> _flat = new();

    private ulong _seed = 0x5F3759DFUL;

    public override void _Ready()
    {
        for (var i = 0; i < PositionalVoices; i++)
        {
            var player = new AudioStreamPlayer3D
            {
                Name = $"World{i}",
                Bus = "Sfx",
                // Rolls off over a street rather than a room: a gunshot should carry.
                MaxDistance = 90f,
                UnitSize = 8f,
            };
            AddChild(player);
            _world.Add(player);
        }

        for (var i = 0; i < FlatVoices; i++)
        {
            var player = new AudioStreamPlayer { Name = $"Flat{i}", Bus = "Sfx" };
            AddChild(player);
            _flat.Add(player);
        }
    }

    /// <summary>Plays at a world position, audible by distance.</summary>
    public void PlayAt(string id, Vector3 position, float volumeDb = 0f, float pitchJitter = 0.08f)
    {
        var stream = Resolve(id);
        if (stream is null)
            return;

        var player = Lease(_world);
        player.Stream = stream;
        player.GlobalPosition = position;
        player.VolumeDb = volumeDb;
        player.PitchScale = 1f + NextFloat() * pitchJitter * 2f - pitchJitter;
        player.Play();
    }

    /// <summary>Plays without position - things that happen to Ben, not near him.</summary>
    public void Play(string id, float volumeDb = 0f, float pitchJitter = 0.06f)
    {
        var stream = Resolve(id);
        if (stream is null)
            return;

        var player = Lease(_flat);
        player.Stream = stream;
        player.VolumeDb = volumeDb;
        player.PitchScale = 1f + NextFloat() * pitchJitter * 2f - pitchJitter;
        player.Play();
    }

    /// <summary>Picks one of id_1..id_n at random. Used for footsteps.</summary>
    public void PlayVariantAt(string id, int variants, Vector3 position, float volumeDb = 0f)
    {
        var pick = 1 + (int)(NextFloat01() * variants) % variants;
        PlayAt($"{id}_{pick}", position, volumeDb);
    }

    private AudioStream? Resolve(string id)
    {
        if (_cache.TryGetValue(id, out var cached))
            return cached;

        var path = $"{Root}/{id}.wav";
        AudioStream? stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path) : null;

        if (stream is null)
            GD.PushWarning($"[Sfx] missing effect '{id}'");

        _cache[id] = stream;
        return stream;
    }

    /// <summary>
    /// Hands back a free voice, or steals the oldest. Stealing beats dropping: a missing
    /// gunshot is a bug the player can hear.
    /// </summary>
    private static T Lease<T>(List<T> pool) where T : Node
    {
        foreach (var player in pool)
        {
            var playing = player switch
            {
                AudioStreamPlayer3D p3 => p3.Playing,
                AudioStreamPlayer p => p.Playing,
                _ => false,
            };
            if (!playing)
                return player;
        }

        var stolen = pool[0];
        pool.RemoveAt(0);
        pool.Add(stolen);
        return stolen;
    }

    private float NextFloat01()
    {
        _seed = _seed * 6364136223846793005UL + 1442695040888963407UL;
        return ((_seed >> 33) & 0xFFFFFF) / (float)0xFFFFFF;
    }

    private float NextFloat() => NextFloat01();
}
