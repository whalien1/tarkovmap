#!/usr/bin/env python3
"""从 ref/Tarkov_webmap 的 maps_detail.json 抽取指定地图的 Marker，
生成 TarkovMap 自有 schema 的 markers 数组并写回对应 map.json。
用法: python extract_map.py <map_key>   例: python extract_map.py customs
"""
import json, sys, os

ROOT = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(ROOT, "..", "ref", "Tarkov_webmap", "data", "maps_detail.json")
OUT_BASE = os.path.join(ROOT, "..", "TarkovMap", "Data", "maps")

SOURCE = "Re5pawnn/Tarkov_webmap maps_detail.json (author: the-hideout/tarkov-dev-svg-maps)"

def load_map(key):
    d = json.load(open(SRC, encoding="utf-8"))
    for _, v in d.items():
        data = v.get("raw", {}).get("data", {})
        if data.get("key") == key and "21+" not in v.get("name", "") and "夜间" not in v.get("name", ""):
            return v["name"], data
    raise SystemExit(f"找不到地图: {key}")

def pos(p):
    if isinstance(p, list):
        return p[0], p[1]
    return p["x"], p["z"]

def mk(type_, name, x, z, id_=""):
    m = {"id": id_ or f"{type_}_{abs(hash((type_, round(x,1), round(z,1)))) % 99999}",
         "type": type_, "name": name, "x": round(x, 2), "z": round(z, 2),
         "metadata": {"source": SOURCE}}
    return m

def extract(key):
    name, data = load_map(key)
    hr = data.get("heightRange")
    def in_hr(p):
        if not hr or not isinstance(p, dict) or "y" not in p:
            return True
        return hr[0] <= p["y"] <= hr[1]

    markers = []

    for e in data.get("extracts") or []:
        if not in_hr(e.get("position", {})): continue
        x, z = pos(e["position"])
        f = (e.get("faction") or "shared").lower()
        t = {"pmc": "extract_pmc", "scav": "extract_scav"}.get(f, "extract_shared")
        markers.append(mk(t, e.get("name") or "撤离点", x, z, e.get("id", "")))

    for t in data.get("transits") or []:
        if not in_hr(t.get("position", {})): continue
        x, z = pos(t["position"])
        markers.append(mk("extract_transit", t.get("description") or t.get("name") or "转移点", x, z, str(t.get("id", ""))))

    spawns = data.get("spawns") or []
    for s in spawns:
        if not in_hr(s.get("position", {})): continue
        cats = [c.lower() for c in (s.get("categories") or [])]
        if "boss" in cats or "sniper" in cats: continue
        sides = [x.lower() for x in (s.get("sides") or [])]
        t = "spawn_pmc" if "pmc" in sides else "spawn_scav"
        x, z = pos(s["position"])
        markers.append(mk(t, s.get("zoneName") or "出生点", x, z))

    for b in data.get("bosses") or []:
        bname = b.get("boss", {}).get("name") or "Boss"
        for loc in b.get("spawnLocations") or []:
            hit = next((s for s in spawns if s.get("zoneName") == loc.get("spawnKey")), None)
            if not hit: continue
            x, z = pos(hit["position"])
            markers.append(mk("boss", f"{bname}（{loc.get('name','')}）", x, z))

    for l in data.get("locks") or []:
        if not in_hr(l.get("position", {})): continue
        x, z = pos(l["position"])
        keyname = (l.get("key") or {}).get("name") or l.get("lockType") or "门锁"
        markers.append(mk("lock", keyname, x, z))

    for h in data.get("hazards") or []:
        if not in_hr(h.get("position", {})): continue
        x, z = pos(h["position"])
        markers.append(mk("hazard", h.get("name") or h.get("hazardType") or "危险区", x, z))

    for w in data.get("stationaryWeapons") or []:
        if not in_hr(w.get("position", {})): continue
        x, z = pos(w["position"])
        markers.append(mk("stationary_weapon", (w.get("stationaryWeapon") or {}).get("name") or "固定武器", x, z))

    for lb in data.get("labels") or []:
        p = lb.get("position")
        if not p: continue
        x, z = pos(p)
        markers.append(mk("label", lb.get("text") or lb.get("name") or "", x, z))

    for c in data.get("lootContainers") or []:
        if not in_hr(c.get("position", {})): continue
        x, z = pos(c["position"])
        markers.append(mk("loot_container", (c.get("lootContainer") or {}).get("name") or "物资容器", x, z))

    out_dir = os.path.join(OUT_BASE, key)
    mj_path = os.path.join(out_dir, "map.json")
    mj = json.load(open(mj_path, encoding="utf-8"))
    mj["markers"] = markers
    json.dump(mj, open(mj_path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)

    from collections import Counter
    print(f"{name}({key}): 共 {len(markers)} 个 Marker")
    for t, n in sorted(Counter(m["type"] for m in markers).items()):
        print(f"  {t}: {n}")

if __name__ == "__main__":
    extract(sys.argv[1] if len(sys.argv) > 1 else "customs")
