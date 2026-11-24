# BJJ Journal

Diario de entrenamiento de jiu-jitsu brasileño. Trabajo final de C#.

**App:** https://alcubillaguillem.github.io/bjj_progress_app/

App web instalable (PWA) hecha en C# con **.NET 10 + Blazor WebAssembly**. Funciona en el ordenador y en el móvil, sin conexión. Los datos se guardan en el navegador y, si inicias sesión, se sincronizan con Supabase.

## Qué hace

| Función | Dónde |
| --- | --- |
| Bienvenida en 3 pasos (nombre, cinturón, técnicas de ejemplo) | `/bienvenida` |
| Inicio: racha, horas, gráficas por semana, técnicas más trabajadas, sumisiones, lagunas del juego | `/` |
| Sesiones en calendario; registrar con técnicas, rolls y eventos (sumisión, barrida, pase, escape) | `/sesiones` |
| Biblioteca de técnicas por categoría, búsqueda sin tildes, progreso, etiquetas, enlaces | `/tecnicas` |
| Checklist por cinturón | `/cinturon` |
| Perfil con nivel por experiencia y radares; ranking de sumisiones por mes | `/perfil`, `/sumisiones` |
| Cuenta (correo o Google) y sincronización; copia JSON; tema claro/oscuro; español/inglés | `/acceso`, `/ajustes` |

## Estructura

```
BjjJournal/                 la app (un solo proyecto)
  Program.cs                arranque: idioma y servicios
  App.razor                 rutas
  Models/
    Models.cs               todas las clases de datos y enums
    SeedData.cs             técnicas y posiciones de ejemplo
  Services/
    Journal.cs              el diario: cargar, guardar, validar, buscar, importar/exportar
    Stats.cs                cálculos de estadísticas
    Cloud.cs                cuenta y sincronización con Supabase
  Pages/                    una página por pantalla (.razor)
  Components/               piezas reutilizables: gráficas SVG, tarjetas, menú, avisos
  Ui/                       textos (español/inglés), nombres y colores, formato de fechas
  wwwroot/                  index.html, CSS, iconos, fuentes y bjj.js (localStorage)
BjjJournal.Tests/           tests xUnit de Journal, Stats y Cloud
samples/demo-journal.json   un año de datos de ejemplo (Ajustes › Importar)
supabase/                   SQL de la tabla en la nube
```

Cómo encaja: las páginas leen y guardan a través de `Journal`. `Journal` mantiene el diario en memoria (`AppData`) y lo guarda como JSON en localStorage tras cada cambio. `Cloud` escucha esos cambios y, si hay sesión, sube y baja el diario de Supabase (gana el cambio más reciente). `Stats` solo calcula.

## Ejecutar

Requiere el SDK de .NET 10.

```bash
dotnet test                          # tests
dotnet run --project BjjJournal      # abre la URL que muestra la consola
```

Cada push a `main` pasa los tests y publica en GitHub Pages (`.github/workflows/pages.yml`).
