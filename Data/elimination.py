# -*- coding: utf-8 -*-
"""
Dataloader'daki appointments listesinden:
- Aynı "sheet_key" (salt) için deterministik olarak tam 1/3'ünü (floor(n/3)) siler
- Ek olarak verilen Teyit No (id) listesinde olanları daima siler
- Çıktıyı <INPUT>_pruned.json olarak yazar (path'ler kod içinde sabit)

Her koşuda aynı 1/3'ün silinmesi için salt = sheet_key kullanılır:
- Öncelik: options.planning_horizon.start (varsa)
- Değilse: input dosya adı (stem)
"""

import hashlib
import json
from pathlib import Path
from typing import Any, Dict, List

# -------------------- CONFIG (paths kod içinde) --------------------
INPUT_DATALOADER  = Path("./scenarios/capacity_weight=1/technician_capacity_100-driving_speed_dynamic/dataloader-10_03_2025.json")
OUTPUT_DATALOADER = INPUT_DATALOADER.with_name(INPUT_DATALOADER.stem + "_pruned.json")
# ------------------------------------------------------------------

# Zorunlu silinecek teyit numaraları:
FORCED_REMOVE_IDS: List[str] = [
    "9098804873","9098808871","9098808440","9098807243","9098806414","9098796354",
    "9098805808","9098802968","9098798588","9098803807","9098800888","9098830524",
    "9098828650","9098833310","9098827938","9098826067","9098826784","9098822172",
    "9098840670","9098851580","9098852687","9098847389","9098848084","9098851302",
    "9098847547","9098849093","9098847243","9098847303",
]

def infer_sheet_key(obj: Dict[str, Any], fallback: str) -> str:
    """Varsayılan salt: options.planning_horizon.start, yoksa fallback (dosya adı)."""
    try:
        ph = (obj.get("options") or {}).get("planning_horizon") or {}
        start = ph.get("start")
        if isinstance(start, str) and start.strip():
            return start.strip()
    except Exception:
        pass
    return fallback

def stable_score(appt_id: str, sheet_key: str) -> int:
    """Deterministik sıralama skoru (aynı sheet_key için aynı sonuç)."""
    s = f"{sheet_key}|{appt_id}".encode("utf-8", errors="ignore")
    return int(hashlib.sha256(s).hexdigest(), 16)

def choose_one_third(ids: List[str], sheet_key: str) -> List[str]:
    """Aynı salt için her zaman aynı 1/3'ü seç (floor(n/3))."""
    n = len(ids)
    k = n // 3
    if k <= 0:
        return []
    ids_sorted = sorted(ids, key=lambda x: stable_score(x, sheet_key))
    return ids_sorted[:k]

def main():
    # Dataloader'ı yükle
    data = json.loads(INPUT_DATALOADER.read_text(encoding="utf-8"))
    appts = data.get("appointments") or []

    # Sheet salt'ını belirle
    sheet_key = infer_sheet_key(data, fallback=INPUT_DATALOADER.stem)

    # Tüm id'ler
    all_ids: List[str] = [str(a.get("id")) for a in appts if a.get("id") is not None]

    # Deterministik 1/3
    one_third_ids = set(choose_one_third(all_ids, sheet_key))

    # Zorunlu silinecekler
    forced = {str(x).strip() for x in FORCED_REMOVE_IDS if str(x).strip()}
    to_remove = one_third_ids | forced

    # Filtrele
    kept_appts = [a for a in appts if str(a.get("id")) not in to_remove]
    removed_count = len(appts) - len(kept_appts)

    # Yaz
    data["appointments"] = kept_appts
    OUTPUT_DATALOADER.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

    # Log
    print("✅ Pruning complete")
    print(f"Input : {INPUT_DATALOADER}")
    print(f"Output: {OUTPUT_DATALOADER}")
    print(f"Sheet key: {sheet_key}")
    print(f"Toplam appointments         : {len(appts)}")
    print(f"Planlı 1/3 (floor)          : {len(all_ids)//3}")
    print(f"Silinen toplam (1/3 ∪ zorunlu): {removed_count}")
    print(f"  - Zorunlu listede bulunup silinen: {len([x for x in forced if x in all_ids])}")
    print(f"  - 1/3 ile zorunlu çakışan        : {len(one_third_ids & forced)}")

if __name__ == "__main__":
    main()
