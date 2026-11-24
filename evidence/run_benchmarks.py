import json, os, sqlite3, statistics, subprocess, time, urllib.request
from concurrent.futures import ThreadPoolExecutor
import matplotlib.pyplot as plt

DB_PATH = r"D:\web_kurs\evidence\bench.db"
RESULTS_PATH = r"D:\web_kurs\evidence\benchmark_results.json"
CHART_PATH = r"D:\web_kurs\docs\diagrams\fig5_1_perf_charts.png"
BASE_URL = "http://127.0.0.1:5085"

if os.path.exists(DB_PATH):
    os.remove(DB_PATH)

env = os.environ.copy()
env["ASPNETCORE_URLS"] = BASE_URL
env["ConnectionStrings__DefaultConnection"] = f"Data Source={DB_PATH}"

proc = subprocess.Popen(
    ["dotnet", "run", "--project", r"D:\web_kurs\src\ServiceCenter.Web\ServiceCenter.Web.csproj", "--no-build"],
    env=env,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE
)

def wait_ready():
    for _ in range(40):
        try:
            with urllib.request.urlopen(f"{BASE_URL}/health", timeout=1) as r:
                if r.status == 200:
                    return True
        except Exception:
            time.sleep(0.25)
    raise RuntimeError("Server did not start in time")

def login_cookie():
    req = urllib.request.Request(
        f"{BASE_URL}/api/auth/login",
        data=json.dumps({"email": "receiver@service.local", "password": "ReceiverPass123!"}).encode("utf-8"),
        headers={"Content-Type": "application/json"},
        method="POST"
    )
    with urllib.request.urlopen(req, timeout=5) as r:
        cookie = r.headers.get("Set-Cookie").split(";")[0]
        return cookie

def timed_get(path, cookie):
    t0 = time.perf_counter()
    req = urllib.request.Request(f"{BASE_URL}{path}", headers={"Cookie": cookie})
    with urllib.request.urlopen(req, timeout=10) as r:
        r.read()
        status = r.status
    return (time.perf_counter() - t0) * 1000.0, status

def seed_volume(target_count):
    conn = sqlite3.connect(DB_PATH)
    cur = conn.cursor()
    cur.execute("SELECT COUNT(*) FROM service_requests")
    existing = cur.fetchone()[0]
    to_add = target_count - existing
    if to_add <= 0:
        conn.close()
        return
    statuses = ["NEW", "ASSIGNED", "DIAGNOSING", "AWAITING_APPROVAL", "APPROVED", "IN_PROGRESS", "WORK_DONE", "COMPLETED"]
    rows = []
    now = "2026-11-24T12:00:00.0000000Z"
    for i in range(existing + 1, target_count + 1):
        st = statuses[i % len(statuses)]
        cid = 1 if i % 2 == 0 else 2
        tid = None if st == "NEW" else (4 if i % 2 == 0 else 5)
        rows.append((
            f"SR-2026-{i:05d}", cid, 1 + (i % 6), tid,
            f"Тестова несправність #{i} (перегрів або збій живлення)",
            "Діагностика виконана" if st != "NEW" else "",
            "Ремонт завершено" if st in ("WORK_DONE", "COMPLETED") else "",
            st, float((i % 15) * 150 + 300), 1, now, now
        ))
    cur.executemany("""
        INSERT INTO service_requests
        (request_number, client_id, equipment_id, technician_id, issue_description,
         diagnostics_notes, completion_result, status, total_cost, version, created_at, updated_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, rows)
    conn.commit()
    conn.close()

def pct(arr, p):
    s = sorted(arr)
    idx = min(len(s) - 1, max(0, int(round((p / 100.0) * (len(s) - 1)))))
    return round(s[idx], 2)

try:
    wait_ready()
    cookie = login_cookie()

    # 1. Тестування обсягом даних (100, 1000, 10000)
    volume_results = []
    for n in [100, 1000, 10000]:
        seed_volume(n)
        times_list = []
        times_filter = []
        for _ in range(15):
            ms, _ = timed_get("/api/requests?page=1&pageSize=20", cookie)
            times_list.append(ms)
            ms2, _ = timed_get("/api/requests?status=DIAGNOSING&search=Lenovo&page=1&pageSize=20", cookie)
            times_filter.append(ms2)
        volume_results.append({
            "records": n,
            "list_p50_ms": pct(times_list, 50),
            "list_p95_ms": pct(times_list, 95),
            "filter_p50_ms": pct(times_filter, 50),
            "filter_p95_ms": pct(times_filter, 95),
        })

    # 2. Навантажувальне тестування (5, 10, 25 віртуальних користувачів на базі 10 000 записів)
    load_results = []
    for users in [5, 10, 25]:
        def worker():
            lat = []
            errs = 0
            for _ in range(15):
                try:
                    ms, st = timed_get("/api/requests?page=1&pageSize=10", cookie)
                    lat.append(ms)
                    if st >= 400:
                        errs += 1
                except Exception:
                    errs += 1
            return lat, errs

        t_start = time.perf_counter()
        all_lat = []
        total_errs = 0
        with ThreadPoolExecutor(max_workers=users) as ex:
            for lat, errs in ex.map(lambda _: worker(), range(users)):
                all_lat.extend(lat)
                total_errs += errs
        elapsed = time.perf_counter() - t_start
        total_reqs = users * 15
        load_results.append({
            "virtual_users": users,
            "total_requests": total_reqs,
            "rps": round(total_reqs / elapsed, 2),
            "p50_ms": pct(all_lat, 50),
            "p95_ms": pct(all_lat, 95),
            "p99_ms": pct(all_lat, 99),
            "error_rate_pct": round((total_errs / total_reqs) * 100.0, 2)
        })

    output = {"volume_testing": volume_results, "load_testing": load_results}
    with open(RESULTS_PATH, "w", encoding="utf-8") as f:
        json.dump(output, f, ensure_ascii=False, indent=2)
    print(json.dumps(output, ensure_ascii=False, indent=2))

    # Графіки результатів
    fig, (ax1, ax2) = plt.subplots(1, 2, figsize=(10.5, 4.2), dpi=240)
    vols = [str(v["records"]) for v in volume_results]
    ax1.plot(vols, [v["list_p95_ms"] for v in volume_results], marker="o", label="Список (p95)")
    ax1.plot(vols, [v["filter_p95_ms"] for v in volume_results], marker="s", label="Пошук + фільтр (p95)")
    ax1.set_title("Тестування обсягом даних (SQLite)")
    ax1.set_xlabel("Кількість заявок у БД")
    ax1.set_ylabel("Час відповіді p95 (мс)")
    ax1.grid(True, linestyle="--", alpha=0.5)
    ax1.legend()

    vus = [str(l["virtual_users"]) for l in load_results]
    ax2.bar(vus, [l["p95_ms"] for l in load_results], color=["#3b82f6", "#2563eb", "#1d4ed8"], width=0.5)
    ax2.set_title("Навантажувальне тестування (10 000 записів)")
    ax2.set_xlabel("Одночасні клієнти (потоки)")
    ax2.set_ylabel("Затримка p95 (мс)")
    ax2.grid(True, axis="y", linestyle="--", alpha=0.5)

    fig.tight_layout()
    fig.savefig(CHART_PATH, dpi=240, bbox_inches="tight")
    plt.close(fig)
finally:
    proc.terminate()
    try:
        proc.wait(timeout=5)
    except Exception:
        proc.kill()
    if os.path.exists(DB_PATH):
        try:
            os.remove(DB_PATH)
        except Exception:
            pass
