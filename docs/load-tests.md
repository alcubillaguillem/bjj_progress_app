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

Run on 25/11/2026 from my laptop against the real Supabase project (free plan, Frankfurt), with the demo account.

- Laptop: Windows 11 Home, Intel Core i7-1355U (10 cores, 12 threads), .NET SDK 10.0.404.
- Network: Wi-Fi 6 (not cable).
- Journal size: 301 KB per sync (it goes down and up, so ~600 KB of traffic per sync).

| Users at the same time | Syncs/s | Average (ms) | p95 (ms) | Errors | CPU (test program) | Memory |
|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1.9 | 514 | 1,158 | 0.0 % | 2 % | 72 MB |
| 2 | 3.3 | 597 | 1,474 | 0.0 % | 1 % | 63 MB |
| 5 | **5.6** | 868 | 1,431 | 0.0 % | 1 % | 80 MB |
| 10 | 5.6 | 1,710 | **3,177** | 0.0 % | 0 % | 81 MB |

The test stopped at 10 users because the p95 went above 2 seconds.

**Maximum sustained traffic: 5.6 syncs per second with 5 users at the same time (~3.3 MB/s).**

### What the results mean

- There were **no errors** at any step. Supabase never rejected a request; it just got slower.
- From 5 to 10 users the syncs per second stay the same (5.6) and only the response time doubles. That means a limit was reached and the extra requests are waiting in a queue.
- The test program almost didn't use CPU (2 % at most) or memory (~80 MB), so the limit is not my laptop. At the limit the traffic was ~3.3 MB/s, so it's probably the bandwidth (my Wi-Fi upload or the free Supabase instance), not the number of requests.
- In this app, the cost of a sync depends on the journal size, because each sync moves the whole journal. With a smaller journal more syncs per second would fit; with a bigger one, fewer.

### Is that enough?

Yes, for this app. One user only syncs after making changes, and the app waits 1.5 s to group them, so a normal user does a few syncs per training session. 5.6 syncs per second is more than 20,000 syncs per hour.

If the app had many users, the first things I would change are: upload only the items that changed instead of the whole journal, and compress the JSON. Both reduce the bandwidth, which is the real bottleneck.
