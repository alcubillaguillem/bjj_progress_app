namespace BjjJournal.App.Ui;

/// <summary>Avisos breves en la parte inferior ("Sesión guardada").</summary>
public sealed class ToastService
{
    public event Action<string>? Shown;

    public void Show(string message) => Shown?.Invoke(message);
}
