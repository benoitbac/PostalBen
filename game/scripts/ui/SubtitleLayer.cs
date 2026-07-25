using Godot;
using System.Collections.Generic;
using PostalBen.Audio;

namespace PostalBen.UI;

/// <summary>
/// Renders voice lines as on-screen text. Driven entirely by VoiceBank signals, so a
/// line with no recorded audio yet still reads on screen - subtitles are the reason
/// the game is playable in French before a single French take exists.
/// </summary>
public partial class SubtitleLayer : CanvasLayer
{
    /// <summary>Reading speed used to size how long a line stays up.</summary>
    private const float CharsPerSecond = 14f;
    private const float MinDuration = 1.4f;
    private const float MaxDuration = 7f;
    private const int MaxVisible = 3;

    private VBoxContainer _stack = null!;
    private readonly Dictionary<string, string> _speakerColours = new()
    {
        ["ben"] = "ffe9a3",
        ["civilian"] = "ffffff",
        ["clerk"] = "cfe8ff",
        ["police"] = "b7d5ff",
        ["hoa"] = "d9c6ff",
        ["influencer"] = "ffc2e8",
    };

    public bool Enabled { get; set; } = true;

    public override void _Ready()
    {
        Layer = 100;

        var margin = new MarginContainer { AnchorRight = 1f, AnchorTop = 1f, AnchorBottom = 1f };
        margin.SetAnchorsPreset(Control.LayoutPreset.BottomWide, keepOffsets: false);
        margin.AddThemeConstantOverride("margin_bottom", 72);
        margin.AddThemeConstantOverride("margin_left", 96);
        margin.AddThemeConstantOverride("margin_right", 96);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(margin);

        _stack = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _stack.AddThemeConstantOverride("separation", 6);
        margin.AddChild(_stack);

        var voice = GetNode<VoiceBank>("/root/VoiceBank");
        voice.LineStarted += OnLineStarted;

        LoadSettings();
    }

    private void OnLineStarted(string key, string subtitle, string speaker)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(subtitle))
            return;

        // An untranslated key comes back from Godot unchanged. Showing "ben.wake.day1"
        // to a player is worse than showing nothing, so drop it and shout in the log.
        if (subtitle == key)
        {
            GD.PushWarning($"[Subtitles] missing translation for '{key}'");
            return;
        }

        while (_stack.GetChildCount() >= MaxVisible)
            _stack.GetChild(0).QueueFree();

        var colour = _speakerColours.TryGetValue(speaker, out var c) ? c : "ffffff";
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Text = $"[center][color=#{colour}]{subtitle}[/color][/center]",
        };
        label.AddThemeFontSizeOverride("normal_font_size", 26);
        label.AddThemeColorOverride("default_color", Colors.White);
        _stack.AddChild(label);

        var duration = Mathf.Clamp(subtitle.Length / CharsPerSecond, MinDuration, MaxDuration);
        var tween = CreateTween();
        tween.TweenInterval(duration);
        tween.TweenProperty(label, "modulate:a", 0f, 0.35f);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }

    private void LoadSettings()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Systems.LocaleManager.ConfigPath) != Error.Ok)
            return;
        Enabled = (bool)cfg.GetValue("ui", "subtitles", true);
    }
}
