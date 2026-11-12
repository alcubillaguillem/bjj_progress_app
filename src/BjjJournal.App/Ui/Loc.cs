using System.Globalization;

namespace BjjJournal.App.Ui;

/// <summary>
/// Idioma de la interfaz. El texto en español es la clave: si no hay traducción se muestra en español.
/// El idioma se elige en Ajustes, se guarda en el navegador y se aplica recargando la app.
/// </summary>
public sealed class Loc
{
    public const string StorageKey = "bjj-lang";

    private Loc(string lang) => Lang = lang;

    /// <summary>Instancia activa, para clases estáticas como <see cref="Labels"/> y <see cref="Format"/>.</summary>
    public static Loc Current { get; private set; } = new("es");

    public string Lang { get; }

    public bool IsEnglish => Lang == "en";

    public CultureInfo Culture => new(IsEnglish ? "en-GB" : "es-ES");

    public string this[string es] => IsEnglish && English.Map.TryGetValue(es, out var en) ? en : es;

    /// <summary>Texto con huecos {0}, {1}…</summary>
    public string F(string es, params object[] args) => string.Format(Culture, this[es], args);

    /// <summary>Singular o plural según n («1 sesión», «3 sesiones»).</summary>
    public string N(int n, string one, string many) => $"{n} {this[n == 1 ? one : many]}";

    public static Loc Init(string? lang)
    {
        Current = new Loc(lang == "en" ? "en" : "es");
        return Current;
    }
}
