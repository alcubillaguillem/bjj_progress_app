using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using BjjJournal.App;
using BjjJournal.App.Ui;
using BjjJournal.Application.Abstractions;
using BjjJournal.Application.Persistence;
using BjjJournal.Application.Services;
using BjjJournal.Infrastructure.Storage;
using BjjJournal.Infrastructure.Supabase;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

// Idioma guardado en Ajustes. Se lee antes de crear la app para fijar también fechas y números.
var loc = Loc.Init(BrowserLang.Get());
CultureInfo.DefaultThreadCurrentCulture = loc.Culture;
CultureInfo.DefaultThreadCurrentUICulture = loc.Culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// En Blazor WebAssembly "singleton" y "scoped" viven lo mismo: toda la pestaña.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IAppDataStore, LocalStorageDataStore>();
builder.Services.AddSingleton<IKeyValueStore, LocalStorageKeyValueStore>();

// Cuentas y sincronización (Supabase). Sin configuración, la app funciona solo en local.
var supabase = builder.Configuration.GetSection("Supabase").Get<SupabaseOptions>() ?? new SupabaseOptions();
builder.Services.AddSingleton(supabase);
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<IAuthApi, SupabaseAuthApi>();
builder.Services.AddSingleton<IRemoteJournalStore, SupabaseJournalStore>();
builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<SyncService>();
builder.Services.AddSingleton<SyncCoordinator>();
builder.Services.AddSingleton<JournalContext>();
builder.Services.AddSingleton<ProfileService>();
builder.Services.AddSingleton<PositionService>();
builder.Services.AddSingleton<TechniqueService>();
builder.Services.AddSingleton<SessionService>();
builder.Services.AddSingleton<StatsService>();
builder.Services.AddSingleton<BackupService>();
builder.Services.AddSingleton<ToastService>();
builder.Services.AddSingleton(_ => Loc.Current);

var host = builder.Build();
await host.Services.GetRequiredService<IJSRuntime>().InvokeVoidAsync("bjjLang.apply", loc.Lang);
await host.RunAsync();

/// <summary>Lectura síncrona del idioma guardado (bjj.js), disponible antes de construir el host.</summary>
internal static partial class BrowserLang
{
    [JSImport("globalThis.bjjLang.get")]
    internal static partial string Get();
}
