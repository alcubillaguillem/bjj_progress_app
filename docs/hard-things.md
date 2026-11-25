# Specific, interesting and hard things

These are the parts of the project that took me the most time or that I found most interesting. For each one I explain the problem and how I solved it.

---

## 1. Running C# in the browser without a server

**Problem.** Browsers only run JavaScript, and I wanted to write everything in C#. I also didn't want to pay for a server.

**Solution.** Blazor WebAssembly. The browser downloads my compiled C# and a small .NET runtime in WebAssembly, and runs them on the device. The result is just static files, so GitHub Pages can host them for free.

**What was hard.** Some things only exist in JavaScript, like `localStorage` or downloading a file. I kept that to one small file (`wwwroot/js/bjj.js`, about 25 lines) and call it from C# with `IJSRuntime`. The language was trickier: it has to be known *before* Blazor starts, so dates and numbers come out in the right format from the first screen. For that I used `[JSImport]` in `Program.cs`, which calls JavaScript synchronously at start-up.

## 2. Offline-first with optional sync

**Problem.** The app must work without internet and without an account, but people who sign in want the same data on their phone and computer.

**Solution.** localStorage is always the main storage. Supabase is a copy. After every change, `Journal` raises an event and `Cloud` syncs 1.5 seconds later (so typing in a form doesn't send ten requests).

**What was hard.** Two devices can change things while offline. I solved conflicts with three fields in a base class `Entity`: `Id`, `UpdatedAt` and `IsDeleted`. The merge compares item by item and keeps the newest one ("last write wins"). Deleting is only marking `IsDeleted = true`, because if I really removed the item, the other device would upload it again on the next sync. This is in `Cloud.Merge` and it is covered by tests.

## 3. Login with Google in a static website

**Problem.** Google login usually needs a server to receive the response. My app has no server.

**Solution.** Supabase handles the OAuth flow and redirects back to my page with the tokens in the URL fragment (`#access_token=...`). The fragment never reaches a server, it stays in the browser. `Cloud.ParseFragment` reads it and `Cloud.ReadJwt` decodes the JWT to get the user id, email and expiry date (I decode the Base64Url part by hand). Then the session is saved and refreshed automatically before it expires.

**What was hard.** Configuring the redirect URLs in Supabase and Google so it works both on `localhost` and on GitHub Pages.

## 4. Security without a back end of my own

**Problem.** The Supabase key is in `appsettings.json`, which anyone can download.

**Solution.** That key is public by design. The real protection is **Row Level Security** in Postgres (`supabase/001_bjj_journals.sql`): every request carries the user's JWT, and the policies only allow the row where `user_id = auth.uid()`. Anonymous users have no permissions. I learned that in this kind of app the security has to be in the database, because the user can modify anything in the browser.

## 5. Charts drawn in C# (no chart library)

**Problem.** Chart libraries are JavaScript and add a lot of weight.

**Solution.** I generate the SVG in C# inside `.razor` components: `BarChart`, `LineChart`, `DonutChart` and `RadarChart`. For example, the radar calculates each point with `cos` and `sin` of the axis angle, scaled by the value. The line chart leaves gaps where there's no data (`null`) instead of drawing zero.

## 6. Search and duplicates ignoring accents

**Problem.** In Spanish, "Kímura", "kimura" and "KIMURA" should be the same technique, and searching "estrangulacion" should find "estrangulación".

**Solution.** `Journal.Normalize` converts the text to Unicode form D (letter + separate accent), removes the accent marks (`NonSpacingMark`) and lowercases it. I use it for search and for rejecting duplicates (same name and same position).

## 7. Translation with the Spanish text as the key

**Problem.** I wanted Spanish and English without filling the code with ids like `settings_title`.

**Solution.** The Spanish text itself is the key: `L["Ajustes"]`. `Loc` looks it up in `English.cs` (about 340 phrases). If a translation is missing, the Spanish text is shown, so the app never breaks. The code stays readable.

## 8. Dates, time zones and weeks

**Problem.** A session at 23:30 in Spain is already the next day in UTC. Streaks and weekly charts would be wrong.

**Solution.** Dates are always stored in UTC and converted to local time only when shown (`Ui/Format.cs`) or when calculating the day of a session (`TrainingSession.Day`). Weeks start on Monday (`Stats.WeekStart`), with a test for it. Stats tests use a fixed "today" so they don't depend on the day you run them.

## 9. A single-page app on GitHub Pages

**Problem.** GitHub Pages is for static websites. If you open `/bjj_progress_app/sesiones` directly, there's no such file and you get a 404. Also the app lives in a sub-folder and GitHub ignores folders starting with `_` (like `_framework`, where my app is).

**Solution.** The workflow changes `<base href>` to the sub-folder, copies `index.html` as `404.html` (so GitHub serves the app and Blazor shows the right page) and adds `.nojekyll`.

**Known bug I found.** Changing `index.html` after the build breaks the hash that the service worker checks, so the offline cache never installs. I documented it; fixing it means doing the change before the hashes are calculated.

## 10. Measuring the limits (load tests)

**Problem.** I don't have my own server, so "maximum traffic" is not obvious. What can actually break?

**Solution.** I measured two things (details in [load-tests.md](load-tests.md)):

- **Local:** how fast saving is and how much memory the app uses with 100 to 10,000 sessions. I used a fake `IJSRuntime` so the test measures the real JSON written to localStorage.
- **Cloud:** a console program (`tools/TrafficTest`) that does the same requests as a sync with more and more users at the same time, until errors appear or responses take more than 2 seconds.

**What I learned.** The first limit is not speed but **localStorage size**: about 1,500 characters per session, so around 3,300 sessions fit in the 5 million characters a browser allows. That is more than 20 years of training, so it's fine for this app, but it's the first thing to change if the app grew (for example, using IndexedDB).

In the cloud, the real Supabase project handled **5.6 syncs per second** with 5 users at the same time and no errors. With 10 users it didn't go faster, it only got slower (p95 above 3 s). Because each sync moves the whole journal (~300 KB each way), the bottleneck is bandwidth, not the number of requests.
