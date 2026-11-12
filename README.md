# BJJ Journal

Diario de entrenamiento de jiu-jitsu brasileño. Trabajo final de C#.

App web instalable (PWA) hecha íntegramente en C# con **.NET 10 + Blazor WebAssembly**. Funciona en el ordenador y en el móvil, sin conexión, con los datos guardados en el propio dispositivo. Mismo enfoque y estilo visual que *Control Finanzas* (gastos-erasmus): barra inferior en el móvil, menú lateral en pantallas de 900 px o más, tema oscuro con acento lima.

## Qué hace (fase 1 + parte de la 2)

| Función | Dónde |
| --- | --- |
| Onboarding de 3 pasos (nombre, cinturón y grados, semilla de técnicas) | `/bienvenida` |
| Biblioteca de técnicas: CRUD, categorías, posición, etiquetas, notas, enlaces, veces trabajada | `/tecnicas` |
| Búsqueda sin tildes ni mayúsculas por nombre, posición, etiqueta o notas; filtros por categoría, posición y etiqueta | `/tecnicas` |
| Registro de sesión en 2 toques desde cualquier pantalla (+), con valores por defecto de la última sesión | `/sesiones/nueva` |
| Técnicas trabajadas con foco, creación de técnica nueva sin salir del formulario | formulario de sesión |
| Rolls con compañero, cinturón, duración y eventos (sumisión a favor/en contra, barrida, pase, escape) | formulario de sesión |
| Filtros de sesiones por fechas, tipo, gi/no-gi y satisfacción | `/sesiones` |
| Dashboard con las 6 métricas de F5 + lagunas del juego (30 días) | `/` |
| Checklist por cinturón con barra de progreso (plantilla blanco y azul) | `/cinturon` |
| Exportar/importar JSON versionado, con revisión de duplicados y fusionar o reemplazar | `/perfil` |
| Tema oscuro (por defecto) y claro | `/perfil` |

## Arquitectura

```
src/
  BjjJournal.Domain/          entidades, enums, Result<T>, reglas (sin paquetes NuGet)
  BjjJournal.Application/     servicios (casos de uso), DTOs, IAppDataStore, semilla
  BjjJournal.Infrastructure/  LocalStorageDataStore (localStorage vía JS interop)
  BjjJournal.App/             Blazor WebAssembly: páginas, componentes, gráficos SVG
tests/
  BjjJournal.Domain.Tests/       reglas de dominio y normalización de texto
  BjjJournal.Application.Tests/  casos de uso y métricas con FakeTimeProvider
```

- App → Application + Infrastructure; Infrastructure → Application; Application → Domain; Domain → nada.
- Las páginas solo llaman a servicios de Application, que devuelven DTOs.
- Hora vía `TimeProvider` inyectado; fechas en UTC y conversión a hora local solo en la UI.
- Ids `Guid` generados en el cliente, `UpdatedAt` e `IsDeleted` en todas las entidades: preparado para sincronizar en la fase 3.
- Los gráficos (barras, línea, donut) se generan como SVG en componentes Razor. El único JavaScript es `wwwroot/js/bjj.js` (localStorage, descarga de ficheros y tema).

### Cambios respecto al documento de contexto

| Contexto | Implementado | Motivo |
| --- | --- | --- |
| .NET MAUI | Blazor WebAssembly PWA | Que sea como Control Finanzas: web instalable en móvil y PC, sin tiendas ni compilar por plataforma |
| SQLite + EF Core | JSON en localStorage (`IAppDataStore`) | EF Core + SQLite en WebAssembly es frágil; la interfaz permite cambiarlo sin tocar Application |
| LiveCharts2 / SkiaSharp | SVG propio en Razor | Sin dependencias, respeta el tema claro/oscuro |
| `.resx` | Textos en `Ui/Labels.cs` | Pendiente si se quiere multidioma |

## Ejecutar

Requiere el SDK de .NET 10.

```bash
dotnet test                              # 70 tests
dotnet run --project src/BjjJournal.App  # abre la URL que muestra la consola
```

En el móvil: abrir la URL publicada y «Añadir a pantalla de inicio».

## Publicar en GitHub Pages

Sube el repositorio a GitHub, activa *Settings → Pages → Source: GitHub Actions* y haz push a `main`. El workflow `.github/workflows/pages.yml` pasa los tests, publica y ajusta el `base href` al nombre del repositorio.

## Siguiente (fase 2)

Mapa de técnicas (grafo), sistemas (diagramas de flujo), informe semanal, fotos, importar técnicas desde texto y exportar CSV.
