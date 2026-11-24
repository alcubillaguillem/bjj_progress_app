using System.Globalization;
using System.Runtime.InteropServices.JavaScript;
using BjjJournal;
using BjjJournal.Services;
using BjjJournal.Ui;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// Idioma guardado en Ajustes. Se fija antes de arrancar para que fechas y números salgan en ese idioma.
var loc = Loc.Init(BrowserLang.Get());
CultureInfo.DefaultThreadCurrentCulture = loc.Culture;
CultureInfo.DefaultThreadCurrentUICulture = loc.Culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Servicios: uno de cada para toda la app.
builder.Services.AddSingleton(loc);
builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton<Journal>();  // el diario (localStorage)
builder.Services.AddSingleton<Cloud>();    // cuenta y sincronización (Supabase)

var host = builder.Build();
await host.Services.GetRequiredService<Journal>().LoadAsync();
await host.RunAsync();

/// <summary>Lee el idioma guardado (wwwroot/js/bjj.js) antes de arrancar la app.</summary>
internal static partial class BrowserLang
{
    [JSImport("globalThis.bjjLang.get")]
    internal static partial string Get();
}
