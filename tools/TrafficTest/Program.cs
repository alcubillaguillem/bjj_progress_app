// Prueba de tráfico contra Supabase: ¿cuántas sincronizaciones por segundo aguanta el backend?
// Hace lo mismo que la app al sincronizar (bajar el diario y volver a subirlo) con cada vez más usuarios a la vez,
// hasta que empiezan los errores o las respuestas pasan de 2 segundos.
//
// Uso (desde la raíz del repo, con una cuenta de prueba):
//   BJJ_EMAIL=demo@... BJJ_PASSWORD=... dotnet run --project tools/TrafficTest
// En PowerShell:
//   $env:BJJ_EMAIL="demo@..."; $env:BJJ_PASSWORD="..."; dotnet run --project tools/TrafficTest
//
// Límite de seguridad: como mucho 50 peticiones a la vez y 10 segundos por escalón (~1 minuto en total).

using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

int[] steps = [1, 2, 5, 10, 20, 50];
var stepTime = TimeSpan.FromSeconds(10);
const double MaxErrorRate = 0.05;
const double MaxP95Ms = 2000;

var email = Environment.GetEnvironmentVariable("BJJ_EMAIL");
var password = Environment.GetEnvironmentVariable("BJJ_PASSWORD");
if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
{
    Console.WriteLine("Faltan las variables BJJ_EMAIL y BJJ_PASSWORD (cuenta de prueba).");
    return 1;
}

// La URL y la clave pública de Supabase son las de la app.
var settings = JsonDocument.Parse(File.ReadAllText(FindAppSettings())).RootElement.GetProperty("Supabase");
var url = settings.GetProperty("Url").GetString()!.TrimEnd('/');
var key = settings.GetProperty("PublishableKey").GetString()!;

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
http.DefaultRequestHeaders.Add("apikey", key);

// 1. Iniciar sesión como haría la app.
var login = await http.PostAsJsonAsync($"{url}/auth/v1/token?grant_type=password", new { email, password });
if (!login.IsSuccessStatusCode)
{
    Console.WriteLine($"No se pudo iniciar sesión: {(int)login.StatusCode}");
    return 1;
}

var auth = await login.Content.ReadFromJsonAsync<JsonElement>();
var token = auth.GetProperty("access_token").GetString()!;
var userId = auth.GetProperty("user").GetProperty("id").GetString()!;
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

// 2. Bajar el diario una vez para saber cuánto pesa cada sincronización.
var journalJson = await DownloadJournal();
Console.WriteLine($"Cuenta: {email} | diario de {journalJson.Length / 1024.0:F0} KB por sincronización");
Console.WriteLine();
Console.WriteLine("| A la vez | Sincronizaciones/s | Media (ms) | p95 (ms) | Errores | CPU proceso | Memoria |");
Console.WriteLine("|---|---|---|---|---|---|---|");

var process = Process.GetCurrentProcess();
var best = 0.0;
var bestStep = 0;
foreach (var users in steps)
{
    var latencies = new List<double>();
    var errors = 0;
    var cpuBefore = process.TotalProcessorTime;
    var clock = Stopwatch.StartNew();

    // Cada "usuario" sincroniza sin parar durante el escalón.
    await Task.WhenAll(Enumerable.Range(0, users).Select(async _ =>
    {
        while (clock.Elapsed < stepTime)
        {
            var watch = Stopwatch.StartNew();
            var ok = await Sync();
            watch.Stop();
            lock (latencies)
            {
                latencies.Add(watch.Elapsed.TotalMilliseconds);
                if (!ok)
                {
                    errors++;
                }
            }
        }
    }));

    clock.Stop();
    process.Refresh();
    latencies.Sort();
    var perSecond = (latencies.Count - errors) / clock.Elapsed.TotalSeconds;
    var p95 = latencies[(int)(latencies.Count * 0.95)];
    var errorRate = (double)errors / latencies.Count;
    var cpu = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds / clock.Elapsed.TotalMilliseconds / Environment.ProcessorCount;
    Console.WriteLine($"| {users} | {perSecond:F1} | {latencies.Average():F0} | {p95:F0} | {errorRate:P1} | {cpu:P0} | {process.WorkingSet64 / 1024 / 1024} MB |");

    if (errorRate > MaxErrorRate || p95 > MaxP95Ms)
    {
        Console.WriteLine();
        Console.WriteLine($"Parado en {users} a la vez: {(errorRate > MaxErrorRate ? "demasiados errores" : "respuestas demasiado lentas")}.");
        break;
    }

    if (perSecond > best)
    {
        best = perSecond;
        bestStep = users;
    }
}

Console.WriteLine();
Console.WriteLine($"Máximo sostenido: {best:F1} sincronizaciones/s con {bestStep} a la vez ({best * journalJson.Length * 2 / 1024 / 1024:F1} MB/s de tráfico).");
return 0;

// Una sincronización: bajar el diario y subirlo otra vez (sin cambios, para no tocar los datos).
async Task<bool> Sync()
{
    try
    {
        var data = await DownloadJournal();
        var body = $"{{\"user_id\":\"{userId}\",\"data\":{data},\"updated_at\":\"{DateTimeOffset.UtcNow:O}\"}}";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{url}/rest/v1/bjj_journals?on_conflict=user_id")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");
        using var push = await http.SendAsync(request);
        return push.IsSuccessStatusCode;
    }
    catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
        return false;
    }
}

async Task<string> DownloadJournal()
{
    var rows = await http.GetFromJsonAsync<JsonElement>($"{url}/rest/v1/bjj_journals?select=data&user_id=eq.{userId}");
    return rows.GetArrayLength() > 0 ? rows[0].GetProperty("data").GetRawText() : throw new InvalidOperationException("La cuenta no tiene diario en la nube");
}

static string FindAppSettings()
{
    for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir is not null; dir = dir.Parent)
    {
        var path = Path.Combine(dir.FullName, "BjjJournal", "wwwroot", "appsettings.json");
        if (File.Exists(path))
        {
            return path;
        }
    }

    throw new FileNotFoundException("Ejecútalo desde dentro del repo (no encuentro BjjJournal/wwwroot/appsettings.json).");
}
