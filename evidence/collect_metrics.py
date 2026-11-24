import os, re, json

ROOT = r"D:\web_kurs\src\ServiceCenter.Web"
EXTENSIONS = {".cs", ".js", ".css", ".html"}

stats = []
for dirpath, dirnames, filenames in os.walk(ROOT):
    dirnames[:] = [d for d in dirnames if d not in ("bin", "obj", "App_Data")]
    for fn in filenames:
        ext = os.path.splitext(fn)[1].lower()
        if ext in EXTENSIONS:
            full = os.path.join(dirpath, fn)
            rel = os.path.relpath(full, ROOT)
            with open(full, "r", encoding="utf-8") as f:
                lines = f.readlines()
            total = len(lines)
            non_empty = sum(1 for l in lines if l.strip())
            comments = sum(1 for l in lines if l.strip().startswith("//") or l.strip().startswith("/*") or l.strip().startswith("*") or l.strip().startswith("<!--"))
            stats.append({
                "file": rel.replace("\\", "/"),
                "ext": ext,
                "total_lines": total,
                "non_empty_lines": non_empty,
                "comment_lines": comments
            })

summary = {
    "files_count": len(stats),
    "total_lines": sum(s["total_lines"] for s in stats),
    "non_empty_lines": sum(s["non_empty_lines"] for s in stats),
    "comment_lines": sum(s["comment_lines"] for s in stats),
    "by_file": stats
}

with open(r"D:\web_kurs\evidence\code_metrics.json", "w", encoding="utf-8") as f:
    json.dump(summary, f, ensure_ascii=False, indent=2)

print(json.dumps(summary, ensure_ascii=False, indent=2))
