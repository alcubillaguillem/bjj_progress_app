# BJJ Journal – Documentation

Final project for the C# course. Author: Guillem Alcubilla.

Live app: https://alcubillaguillem.github.io/bjj_progress_app/
Code: https://github.com/alcubillaguillem/bjj_progress_app

Other documents:
- [Specific, interesting and hard things](hard-things.md)
- [Load and traffic tests](load-tests.md)

---

## 1. What is the app?

BJJ Journal is a training diary for Brazilian Jiu-Jitsu. I train BJJ and I wanted a simple way to remember what I learn in each class and to see if I'm improving.

With the app you can:

- Log training sessions: date, duration, type (class, open mat, competition…), gi or no-gi, how you felt (1–5), the techniques you worked on and every roll (sparring round) with what happened in it (submission for or against, sweep, pass, escape).
- Keep a technique library grouped by category (submissions, sweeps, passes…), with notes, tags, links and progress (not started → learning → competent → mastered).
- See statistics on the home screen: weekly streak, hours, sessions per week, satisfaction trend, most used techniques, submissions ranking and "gaps" (categories you haven't trained in 30 days).
- Check the techniques expected for your belt, see your profile with radar charts and a level based on experience.
- Use a training mode with a timer for rounds.
- Use it without an account, or sign in with email or Google to sync between phone and computer.
- Export and import a JSON backup, switch between dark and light theme, and use it in Spanish or English.

It works on desktop and mobile, and it can be installed as an app (PWA).

## 2. Technologies

| What | Technology |
|---|---|
| Language | C# (.NET 10) |
| UI framework | Blazor WebAssembly (`.razor` components: HTML + C#) |
| Local storage | Browser `localStorage` (the whole journal as one JSON) |
| Login and cloud copy | Supabase (Auth + Postgres table with Row Level Security) |
| Tests | xUnit |
| Hosting and CI/CD | GitHub Pages + GitHub Actions |

### Why Blazor WebAssembly?

I needed one app for my phone and my computer, written in C#.

- A desktop app (WPF, WinForms) only runs on Windows.
- .NET MAUI needs a different build for each platform and publishing in app stores.
- A classic web app with a server (ASP.NET, Blazor Server) needs a server running all the time. It costs money and doesn't work offline.

Blazor WebAssembly compiles my C# so the browser can run it. The browser downloads the app once and runs it on the device. That means: any device with a browser, works offline, can be installed as a PWA, and it's hosted for free on GitHub Pages as static files.

The trade-off: the first load is slower because the browser downloads the .NET runtime (a few MB). After that it's cached.

## 3. Architecture

Everything I wrote is **front end**: it runs in the user's browser. The only back end is Supabase, which I didn't program; I only created one table and its security rules.

```
 ┌──────────────────────────── Browser (user's device) ───────────────────────────┐
 │                                                                                │
 │   Pages / Components (.razor)  ──►  Services                                   │
 │        what the user sees           Journal.cs  ── save/load ──► localStorage  │
 │                                     Stats.cs    (only calculations)            │
 │                                     Cloud.cs    ── HTTPS ──┐                   │
 └────────────────────────────────────────────────────────────┼───────────────────┘
                                                              ▼
                                               Supabase (back end as a service)
                                               - Auth: email + Google login
                                               - Table bjj_journals: 1 JSON per user, RLS
```

### Folder structure

```
BjjJournal/              the app (one project)
  Program.cs             start-up: language, services (dependency injection), load the journal
  App.razor              router: URL → page
  Models/                data classes (Models.cs) and starter techniques (SeedData.cs)
  Services/              logic: Journal.cs, Stats.cs, Cloud.cs
  Pages/                 one file per screen (16 pages)
  Components/            reusable pieces: layout/menu, SVG charts, cards, pickers, icons, toast
  Ui/                    texts and translation (Loc.cs, English.cs), labels and colours, date format
  wwwroot/               static files: index.html, CSS, fonts, images, bjj.js, manifest, service worker
BjjJournal.Tests/        xUnit tests (logic + load tests)
tools/TrafficTest/       console program for the traffic test against Supabase
supabase/                SQL of the cloud table and its security policies
samples/                 a demo journal (one year of data) to import
.github/workflows/       CI/CD: test, build and deploy to GitHub Pages
```

I keep the logic in `Services` and the pages only display data and call the services. This way I can test the logic without a browser.

### Data model

All the data is one object, `AppData`, saved as one JSON document:

- `UserProfile`: name, belt, stripes, gym, start date.
- `Position`: mount, closed guard, back…
- `Technique`: name, category, position, progress, notes, tags, links.
- `TrainingSession`: date, duration, type, gi/no-gi, satisfaction, techniques (`SessionTechnique`) and rolls (`Roll`), each roll with events (`RollEvent`).

Every stored item inherits from `Entity`: a unique `Id`, `UpdatedAt` (time of the last change) and `IsDeleted` (soft delete). These three fields are what make the sync possible (see below).

## 4. How data is saved

1. The user saves something in a page.
2. `Journal.cs` validates it (for example: name required, no duplicate technique ignoring accents, stripes 0–4).
3. `Journal.SaveAsync()` serialises the whole journal to JSON and writes it to `localStorage` through `wwwroot/js/bjj.js` (C# can't access localStorage directly; that small JS file is the only JavaScript in the app).
4. `Journal` raises the `Changed` event.
5. If the user is signed in, `Cloud.cs` waits 1.5 seconds (to group several changes) and syncs with Supabase.

localStorage is always the main storage. It's fast, it works offline and it doesn't need an account. Supabase is an optional copy for using several devices and for not losing the data if the browser data is cleared.

### Sync and conflicts

`Cloud.SyncAsync()` downloads the cloud copy, merges it with the local one and uploads the result. The merge (`Cloud.Merge`) works item by item:

- Items are matched by `Id`.
- If the same item is in both copies, the one with the newest `UpdatedAt` wins ("last write wins").
- Items that exist only on one side are kept.
- Deleted items are not removed, they are marked `IsDeleted`, so deletions also travel between devices.

Limitation: if you edit the same technique on two devices before syncing, the older edit is lost. For a personal diary used by one person I think this is acceptable, and it's much simpler than merging field by field.

## 5. Security

- The Supabase key in `appsettings.json` is the **publishable** key. It is designed to be public; it only identifies the project.
- When a user signs in, Supabase returns a signed JWT. The app sends it in every request.
- The table `bjj_journals` has **Row Level Security**: a user can only select, insert, update or delete the row where `user_id = auth.uid()`. Anonymous users have no permissions at all.
- So even if someone changes the app or calls the API by hand, they can't read another user's journal. The security is in the database, not in the app.
- All traffic uses HTTPS.
- When you sign out, the local data is deleted from that device (it stays in your account).

## 6. Tests

35 xUnit tests (30 test methods; three of them run with several inputs):

| File | What it tests |
|---|---|
| `JournalTests.cs` (13) | validation, duplicates ignoring accents, search, delete, backup export/import, import merge/replace |
| `StatsTests.cs` (9) | week starts on Monday, streak, 12-week chart, satisfaction moving average, top techniques, submissions ranking, gaps, level |
| `CloudTests.cs` (4) | sync merge (newest wins, deletions travel), reading the login token and the Google redirect. No internet needed |
| `LoadTests.cs` (7) | how the logic behaves with big journals (see [load-tests.md](load-tests.md)) |

The tests run on every push in GitHub Actions. If one fails, the app is not deployed.

I didn't write UI tests. The pages are thin (they only display data and call the services, where the rules are). UI tests would need extra tools like bUnit or Playwright, so I tested the UI by hand on desktop and on my phone.

## 7. Deployment

`.github/workflows/pages.yml` runs on every push to `main`:

1. `dotnet test`
2. `dotnet publish -c Release`
3. Fix the `<base href>` for the sub-folder `/bjj_progress_app/`, copy `index.html` to `404.html` (so direct links like `/sesiones` work) and add `.nojekyll`.
4. Deploy to GitHub Pages.

## 8. How to run it

Requires the .NET 10 SDK.

```bash
dotnet test                         # all tests
dotnet run --project BjjJournal     # open the URL shown in the console
```

Demo data: Settings › Import › `samples/demo-journal.json`.

## 9. Known limitations and future work

- **localStorage limit**: around 5 million characters per site. With my format that's about 3,300 sessions (more than 20 years training 3 days a week). See [load-tests.md](load-tests.md).
- Each sync uploads the whole journal. The traffic test reached 5.6 syncs per second against Supabase (limit: bandwidth). Fine for a personal diary, but it wouldn't scale to many users or very big journals.
- Last write wins can lose an edit made on two devices at the same time.
- The offline service worker doesn't install on GitHub Pages: the workflow changes `index.html` after the build, so its hash no longer matches. The app works, but not offline after the first visit on a new device.
- Ideas: weekly report, CSV export, a map of positions and transitions.
