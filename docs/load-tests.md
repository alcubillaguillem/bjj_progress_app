# Load and traffic tests

The task was to find the maximum traffic the app allows and the resource consumption in that case, also for the requests that perform operations.

My app has no server of its own, so I had to think first about what "traffic" means here:

1. **Local operations.** Every save happens in the user's browser. The load is how big the journal is and how many operations per second the logic can do.
2. **Cloud requests.** The only network operation is the sync with Supabase (download the journal + upload it). The load is how many syncs per second the back end accepts.

I didn't load test GitHub Pages: it only serves static files, it's GitHub's infrastructure and hammering it would break their terms of use.

---

## 1. Local load tests

File: `BjjJournal.Tests/LoadTests.cs`. They run with the other tests:

```bash
dotnet test --filter "Category=Load" --logger "console;verbosity=detailed"
```

**How.** I build journals of 100, 1,000, 5,000 and 10,000 sessions (each one with 3 techniques and 4 rolls with 2 events, like a normal class) on top of the 63 starter techniques. Then I save 50 more sessions and measure the time per save, saves per second, the size of the JSON and the memory.

To measure the real cost, the journal writes to a fake `localStorage` (a fake `IJSRuntime`). That way each save does the same as in the browser: serialise the **whole** journal to JSON.

### Results

Machine: Linux container, .NET 10, 4 cores. In the browser, WebAssembly is slower than native .NET (I'd expect around 3–10× slower), so these numbers are a lower bound.

| Sessions | Time per save | Saves per second | JSON size | Memory | Fits in localStorage? |
|---:|---:|---:|---:|---:|:---:|
| 100 | 1.0 ms | ~1,000 | 0.25 M chars | 0.7 MB | Yes |
| 1,000 | 19 ms | ~52 | 1.6 M chars | 3.5 MB | Yes |
| 5,000 | 25 ms | ~41 | 7.5 M chars | 14.6 MB | **No** |
| 10,000 | 69 ms | ~15 | 15.0 M chars | 28.8 MB | **No** |

Other operations with 10,000 sessions:

| Operation | Time |
|---|---:|
| All home-screen statistics (streak, weekly chart, trend, top techniques, submissions, gaps, level) | ~120–250 ms |
| Sync merge of two copies of 10,000 sessions (1,000 edited on the other device) | ~11–20 ms |

### localStorage limit

- One session takes about **1,500 characters** of JSON.
- Browsers allow about **5 million characters** per website in localStorage (Chrome, Edge, Safari).
- So about **3,300 sessions** fit. Training 3 days a week (~150 sessions a year), that's **more than 20 years**.

### Conclusions

- Time and memory grow linearly with the number of sessions, because every save serialises the whole journal.
- For a real user (a few hundred sessions per year) saving takes a few milliseconds: instant.
- **The first limit is storage, not speed**: localStorage gets full before the app gets slow. If the app had to handle much more data, I would move to IndexedDB (much bigger limit) and save only the changed item instead of the whole journal.

---

## 2. Traffic test against Supabase

Program: `tools/TrafficTest` (a small console app).

**How.** It signs in with a test account and repeats the same requests the app does when it syncs: `GET` the user's journal and `POST` it back (without changes, so the data isn't touched). It increases the number of simultaneous users step by step: 1, 2, 5, 10, 20 and 50, for 10 seconds each. It stops when more than 5% of requests fail or when the 95th percentile of the response time goes above 2 seconds. For every step it measures:

- syncs per second (successful),
- average and p95 response time,
- error rate,
- CPU and memory of the test program.

For safety it never goes above 50 simultaneous users and lasts about one minute.

**How to run it** (from the repository folder, with the test account):

```bash
BJJ_EMAIL=demo@... BJJ_PASSWORD=... dotnet run --project tools/TrafficTest
```

PowerShell:

```powershell
$env:BJJ_EMAIL="demo@..."; $env:BJJ_PASSWORD="..."; dotnet run --project tools/TrafficTest
```

### Results

_Pending: to be filled in after running it against the real Supabase project._

### Expected limits

- Supabase free plan: the database runs on a small shared instance, so I expect the limit to be a few dozen syncs per second.
- Each sync moves the whole journal twice (down and up). With the demo journal (~620 KB) that's about 1.2 MB per sync, so bandwidth matters more than the number of requests.
- For this app that's more than enough: one user syncs once every few minutes at most, after making changes, and the app waits 1.5 s to group several changes into one sync.
