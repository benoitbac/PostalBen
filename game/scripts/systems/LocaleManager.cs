using Godot;
using System.Collections.Generic;
using System.Linq;

namespace PostalBen.Systems;

/// <summary>
/// Single source of truth for the active language. Everything that renders text or
/// picks a voice line reads from here — nothing else is allowed to call
/// TranslationServer.SetLocale directly.
/// </summary>
public partial class LocaleManager : Node
{
    public const string ConfigPath = "user://settings.cfg";

    /// <summary>Languages the game actually ships. Order drives the settings menu.</summary>
    public static readonly IReadOnlyList<LanguageOption> Supported = new[]
    {
        new LanguageOption("en", "English", "English"),
        new LanguageOption("fr", "French", "Français"),
    };

    /// <summary>
    /// Locale used whenever a translation key or a voice clip is missing in the
    /// active language. English is the authoring language, so it is always complete.
    /// </summary>
    public const string FallbackLocale = "en";

    [Signal]
    public delegate void LocaleChangedEventHandler(string locale);

    public string Current { get; private set; } = FallbackLocale;

    public override void _Ready()
    {
        Apply(LoadSavedLocale() ?? DetectSystemLocale());
    }

    /// <summary>
    /// Switches language at runtime. Safe to call mid-game: UI re-translates via
    /// Godot's notification, and in-flight voice lines finish in the old language
    /// rather than cutting out mid-word.
    /// </summary>
    public void Apply(string locale)
    {
        locale = Normalize(locale);
        if (locale == Current && TranslationServer.GetLocale() == locale)
            return;

        Current = locale;
        TranslationServer.SetLocale(locale);
        Persist(locale);
        EmitSignal(SignalName.LocaleChanged, locale);
        GD.Print($"[Locale] active language -> {locale}");
    }

    /// <summary>Maps anything the OS or a save file throws at us onto a shipped language.</summary>
    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            return FallbackLocale;

        // "fr_BE", "fr-CA" and "fr" all resolve to our single French build.
        var root = locale.Replace('-', '_').Split('_')[0].ToLowerInvariant();
        return Supported.Any(l => l.Code == root) ? root : FallbackLocale;
    }

    public LanguageOption CurrentOption =>
        Supported.First(l => l.Code == Current);

    private static string DetectSystemLocale() =>
        Normalize(OS.GetLocaleLanguage());

    private static string? LoadSavedLocale()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(ConfigPath) != Error.Ok)
            return null;
        return cfg.GetValue("general", "locale", Variant.From((string?)null)).AsString() is { Length: > 0 } s
            ? s
            : null;
    }

    private static void Persist(string locale)
    {
        var cfg = new ConfigFile();
        cfg.Load(ConfigPath); // keep unrelated settings intact
        cfg.SetValue("general", "locale", locale);
        cfg.Save(ConfigPath);
    }

    public readonly record struct LanguageOption(string Code, string EnglishName, string NativeName);
}
