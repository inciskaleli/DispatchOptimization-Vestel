# -*- coding: utf-8 -*-
"""
Builds the requested dataloader JSON from the given Excel and existing
distance/duration matrices.
"""

import json
import re
from pathlib import Path
from typing import Dict, Tuple, Optional, List

import pandas as pd
from dateutil import parser as dtparser

# --------------------- CONFIG ---------------------
INPUT_XLSX = "28002357 Yiğit Klima 10-14 Mart 2025_SON Veri_Düzeltilmiştir.xlsx"
SHEET_RAPOR = "RAPOR-14_03_2025"
SHEET_TECH = "Teknisyen Yetkinlikleri"
SHEET_UGCTS = "Ürün grubu çağrı tipi süre"

DISTANCE_JSON = "distance.json"
DURATION_JSON = "duration.json"
#./scenarios/technician_capacity_120-driving_speed_60kmh/
OUTPUT_JSON = "./scenarios/capacity_weight=1/technician_capacity_100-driving_speed_dynamic/dataloader-14_03_2025.json"
# --------------------------------------------------

def normalize_text(s: Optional[str]) -> str:
    """Trim and collapse internal whitespace; return '' for None."""
    if s is None:
        return ""
    return " ".join(str(s).strip().split())

def _to_z_no_ms(dt_like) -> str:
    """Format datetime as 'YYYY-MM-DDTHH:MM:SSZ'."""
    ts = pd.to_datetime(dt_like)
    return ts.strftime("%Y-%m-%dT%H:%M:%SZ")

def _to_z_ms(dt_like) -> str:
    """Format datetime as 'YYYY-MM-DDTHH:MM:SS.000Z' (force .000)."""
    ts = pd.to_datetime(dt_like)
    return ts.strftime("%Y-%m-%dT%H:%M:%S") + ".000Z"

def build_duration_lookups(ugcts: pd.DataFrame):
    exact: Dict[Tuple[str, str], int] = {}
    wildcard: Dict[str, int] = {}

    if ugcts is None or ugcts.empty:
        return exact, wildcard

    ct_col = "Çağrı tipi"
    pg_col = "Ürün grubu"
    dur_col = "Süre"

    # Wildcard rows: Ürün grubu contains '*'
    wc_rows = ugcts[ugcts[pg_col].astype(str).str.contains(r"\*", na=False)]
    for _, row in wc_rows.iterrows():
        a_ct = normalize_text(row.get(ct_col))
        c_len = numeric_or_none(row.get(dur_col))
        if c_len is None:
            continue
        try:
            wildcard[a_ct] = int(float(c_len))
        except Exception:
            pass

    # Exact rows: Ürün grubu does NOT contain '*'
    ex_rows = ugcts[~ugcts[pg_col].astype(str).str.contains(r"\*", na=False)]
    for _, row in ex_rows.iterrows():
        a_ct = normalize_text(row.get(ct_col))
        b_pg = normalize_text(row.get(pg_col))
        c_len = numeric_or_none(row.get(dur_col))
        if c_len is None:
            continue
        try:
            exact[(a_ct, b_pg)] = int(float(c_len))
        except Exception:
            pass

    return exact, wildcard

def get_appt_duration_minutes(call_type: str, product_group: str,
                              exact: Dict[Tuple[str, str], int],
                              wildcard: Dict[str, int]) -> Optional[int]:
    ct = normalize_text(call_type)
    pg = normalize_text(product_group)
    if (ct, pg) in exact:
        return exact[(ct, pg)]
    return wildcard.get(ct, None)

def parse_coord_latlon_to_lonlat_str(val: str) -> Optional[str]:
    if not isinstance(val, str):
        return None
    s = val.strip()
    if not s:
        return None
    if ";" in s:
        lat_s, lon_s = [x.strip() for x in s.split(";", 1)]
    elif "," in s:
        lat_s, lon_s = [x.strip() for x in s.split(",", 1)]
    else:
        return None
    try:
        lat = float(lat_s.replace(",", "."))
        lon = float(lon_s.replace(",", "."))
        return f"{lon},{lat}"
    except Exception:
        return None

# ---------- NEW: robust normalization + parser ----------
def _normalize_aw(s: str) -> str:
    """Normalize different dash types and spaces; keep content intact."""
    if not isinstance(s, str):
        return ""
    # normalize dashes: figure dash, en dash, em dash, minus sign -> '-'
    s = (s.replace("\u2012", "-")
           .replace("\u2013", "-")
           .replace("\u2014", "-")
           .replace("\u2212", "-"))
    # NBSP -> space
    s = s.replace("\xa0", " ")
    # collapse whitespace (keeps single spaces)
    s = " ".join(s.split())
    return s

def parse_arrival_window(yy: str) -> Tuple[Optional[str], Optional[str]]:
    """
    Accept formats like:
      - '11.03.2025 08:00-10:00'
      - '11.03.2025 08:00:00-10:00:00'
      - '11.03.2025 10:00:00–12:00:00' (en dash)
      - '11.03.2025 15:00:0017:00:00'  (no separator; will be auto-fixed)
    Returns (start_iso, end_iso) in 'YYYY-MM-DDTHH:MM:SS'
    """
    if not isinstance(yy, str):
        return None, None

    s = _normalize_aw(yy)

    # --- fix jammed times: insert '-' if two time tokens are adjacent with no separator
    # e.g. '15:00:0017:00:00' -> '15:00:00-17:00:00'
    s = re.sub(
        r"(\d{1,2}:\d{2}(?::\d{2})?)\s*(?=\d{1,2}:\d{2}(?::\d{2})?)",
        r"\1-",
        s,
    )

    # 1) capture the date
    m_date = re.search(r"(\d{1,2}[./-]\d{1,2}[./-]\d{2,4})", s)
    if not m_date:
        return None, None
    date_part = m_date.group(1)
    try:
        d = dtparser.parse(date_part, dayfirst=True).date()
    except Exception:
        return None, None

    # 2) capture time tokens (HH:MM[:SS]); after auto-fix we should have at least 2
    times = re.findall(r"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", s)
    if len(times) < 2:
        # fallback: still try packed pattern just in case
        packed = re.search(r"(\d{1,2}:\d{2}(?::\d{2})?)(\d{1,2}:\d{2}(?::\d{2})?)", s)
        if packed:
            times = [packed.group(1), packed.group(2)]
        else:
            return None, None

    t_start, t_end = times[0], times[1]

    def to_iso(time_str: str) -> Optional[str]:
        try:
            ts = dtparser.parse(time_str).time()
            return f"{d.isoformat()}T{ts.strftime('%H:%M:%S')}"
        except Exception:
            return None

    return to_iso(t_start), to_iso(t_end)


# -------------------------------------------------------

def build_business_unit_id(call_type: str, product_group: str, competency_group: str) -> str:
    call_type = normalize_text(call_type)
    product_group = normalize_text(product_group)
    competency_group = normalize_text(competency_group)
    return f"{call_type}|{product_group}|{competency_group}"

def first_nonempty(series: pd.Series) -> Optional[str]:
    for v in series:
        if isinstance(v, str) and v.strip():
            return v.strip()
    return None

def numeric_or_none(x) -> Optional[float]:
    try:
        v = float(str(x).replace(",", "."))
        return v
    except Exception:
        return None

def main():
    # --- Load Excel ---
    xls = pd.ExcelFile(INPUT_XLSX, engine="openpyxl")
    rapor = pd.read_excel(xls, sheet_name=SHEET_RAPOR, dtype=str)
    tech = pd.read_excel(xls, sheet_name=SHEET_TECH, dtype=str)
    ugcts = pd.read_excel(xls, sheet_name=SHEET_UGCTS, dtype=str)

    exact_dur, wildcard_dur = build_duration_lookups(ugcts)

    # --- Load matrices ---
    with open(DISTANCE_JSON, "r", encoding="utf-8") as f:
        distance_obj = json.load(f)
    with open(DURATION_JSON, "r", encoding="utf-8") as f:
        duration_obj = json.load(f)

    # ----------------- OPTIONS -----------------
    options = {
        "account_id": None,
        "office": {"coordinate": "27.436587,38.626512", "zone": "ŞEHZADELER"},
        "planning_horizon": {"start": "2025-03-14T08:00:00", "end": "2025-03-14T23:00:00"},
        "run_time_limit": 120,
        "enable_buffer_slot": False,
        "distance_limit_between_jobs": 400000,
        "start_day_at_office": True,
        "start_point_after_unavailability": "office",
        "respect_scheduled_times": False,
        "call_grouping": False,
        "disable_drive_time_inclusion": False,
        "use_service_zones": False,
        "first_call_zone": False,
        "scheduling_headstart": 60,
        "last_job_close_to_home": False,
        "assign_priority_jobs_first": True,
        "minimize_weighted_completion_time": False,
        "capacity_weight": 1,
        "lunch_break": None,
    }
    ph_start = options["planning_horizon"]["start"]
    ph_end = options["planning_horizon"]["end"]
    office_coord = options["office"]["coordinate"]
    office_zone = options["office"]["zone"]

    # --- Appointments (diagnostic + tolerant) ---
    appointments: List[Dict] = []
    dropped = []  # collect reasons

    # knobs: choose behavior instead of skipping
    KEEP_IF_NO_DURATION = False          # if True -> default to 60 min when duration missing
    DEFAULT_DURATION_MIN = 60
    KEEP_IF_NO_WINDOW = False            # if True -> use planning horizon as arrival window when parsing fails
    USE_PH_AS_JOB_TIMES_IF_NO_WINDOW = False  # or also use PH as job start/end

    for idx, r in rapor.iterrows():
        row_id = str(r.get("Teyit No") or f"row{idx}")

        # --- coordinate / zone ---
        loc_coord = parse_coord_latlon_to_lonlat_str(r.get("Müşteri Koordinat"))
        zone_id = normalize_text(r.get("Müşteri İlçe"))
        if not loc_coord:
            # keep going; matrix may still allow travel by ids even if coordinate empty
            pass

        # --- arrival window ---
        start_iso, end_iso = parse_arrival_window(r.get("Randevu Tarih saat"))
        if (not start_iso or not end_iso):
            if KEEP_IF_NO_WINDOW:
                start_iso = options["planning_horizon"]["start"]
                end_iso   = options["planning_horizon"]["end"]
            else:
                dropped.append((row_id, "no_arrival_window"))
                continue

        # --- duration ---
        call_type = normalize_text(r.get("Çağrı Tipi"))
        product_group = normalize_text(r.get("Ürün grubu adı"))
        duration_min = get_appt_duration_minutes(call_type, product_group, exact_dur, wildcard_dur)

        if duration_min is None:
            if KEEP_IF_NO_DURATION:
                duration_min = DEFAULT_DURATION_MIN
            else:
                dropped.append((row_id, "no_duration_mapping"))
                continue

        # --- build times (be robust) ---
        try:
            start_dt = pd.to_datetime(start_iso)

            # Adjusted job duration: end - start = duration_min / 1.2
            DURATION_DIVISOR = 1
            adjusted_minutes = float(duration_min) / DURATION_DIVISOR

            # safety: avoid non-sense values
            if not (adjusted_minutes > 0 and adjusted_minutes < 24 * 60 * 30):
                raise ValueError(f"adjusted_minutes out of range: {adjusted_minutes}")

            if USE_PH_AS_JOB_TIMES_IF_NO_WINDOW and not parse_arrival_window(r.get("Randevu Tarih saat"))[0]:
                job_start_ref = _to_z_no_ms(pd.to_datetime(options["planning_horizon"]["start"]))
                job_end_ref   = _to_z_no_ms(pd.to_datetime(options["planning_horizon"]["end"]))
            else:
                job_start_ref = _to_z_no_ms(start_dt)
                job_end_ref   = _to_z_no_ms(start_dt + pd.to_timedelta(adjusted_minutes, unit="m"))

            arrival_start_ref = _to_z_ms(start_dt)
            arrival_end_ref   = _to_z_ms(pd.to_datetime(end_iso))
        except Exception as e:
            dropped.append((row_id, f"time_build_error:{e}"))
            continue


        appt_id = normalize_text(r.get("Teyit No")) or row_id
        appt_name = normalize_text(r.get("Müşteri no"))
        bu_id = build_business_unit_id(
            r.get("Çağrı Tipi"),
            r.get("Ürün grubu adı"),
            r.get("Yetkinlik grubu"),
        )

        appointments.append(
            {
                "location": {"coordinate": loc_coord, "zone": zone_id or None},
                "arrival_window": {"start": arrival_start_ref, "end": arrival_end_ref},
                "eligible_technicians": [],
                "id": appt_id,
                "start": job_start_ref,
                "end": job_end_ref,
                "technician_ids": [],
                "priority": 1,
                "name": appt_name,
                "business_unit_id": bu_id,
                "optimize_for": "score",
                "score_type": "average_revenue",
            }
        )

    if dropped:
        print("⚠️ Skipped rows:", len(dropped))
        for rid, reason in dropped[:10]:
            print(f"  - {rid}: {reason}")

    # --- Zones ---
    unique_zones = sorted(
        {
            normalize_text(z)
            for z in rapor.get("Müşteri İlçe", pd.Series(dtype=str)).tolist()
            if isinstance(z, str) and normalize_text(z)
        }
    )
    zones = [{"id": z, "can_go_with": []} for z in unique_zones]

    # --- Technicians ---
    tech_ids = sorted(
        {
            normalize_text(t)
            for t in tech.get("Teknisyen no", pd.Series(dtype=str)).tolist()
            if isinstance(t, str) and normalize_text(t)
        }
    )
    technicians = []
    for tid in tech_ids:
        technicians.append(
            {
                "id": tid,
                "home": {"coordinate": office_coord, "zone": office_zone},
                "work_time": {"start": ph_start, "end": ph_end},
                "non_availabilities": [],
                "name": tid,
            }
        )

    # --- Business Units from RAPOR ---
    bu_records = {}
    for _, r in rapor.iterrows():
        call_type = normalize_text(r.get("Çağrı Tipi"))
        product_group = normalize_text(r.get("Ürün grubu adı"))
        competency = normalize_text(r.get("Yetkinlik grubu"))
        bu_id = build_business_unit_id(call_type, product_group, competency)
        if bu_id not in bu_records:
            bu_records[bu_id] = {
                "id": bu_id,
                "call_type": call_type,
                "product_group": product_group,
                "competency": competency,
                "buffer_slot_count": 0,
                "buffer_slot_length": None,
                "technician_ids": set(),
            }

    # --- buffer_slot_length rules ---
    if not ugcts.empty:
        ct_col = "Çağrı tipi"
        pg_col = "Ürün grubu"
        dur_col = "Süre"

        wildcard_rows = ugcts[ugcts[pg_col].astype(str).str.contains(r"\*", na=False)]
        exact_rows = ugcts[~ugcts[pg_col].astype(str).str.contains(r"\*", na=False)]

        for _, row in wildcard_rows.iterrows():
            a_ct = normalize_text(row.get(ct_col))
            c_len = numeric_or_none(row.get(dur_col))
            if c_len is None:
                continue
            for bu in bu_records.values():
                if bu["call_type"] == a_ct and bu["buffer_slot_length"] is None:
                    bu["buffer_slot_length"] = c_len

        for _, row in exact_rows.iterrows():
            a_ct = normalize_text(row.get(ct_col))
            b_pg = normalize_text(row.get(pg_col))
            c_len = numeric_or_none(row.get(dur_col))
            if c_len is None:
                continue
            for bu in bu_records.values():
                if bu["call_type"] == a_ct and bu["product_group"] == b_pg:
                    bu["buffer_slot_length"] = c_len

    # --- technician_ids rules (respect wildcards, normalize text) ---
    t_id_col = "Teknisyen no"
    t_pg_col = "Çağrı Tanım"        # CALL TYPE name
    t_comp_col = "Yetkinlik tanım"  # competency/product family

    if not tech.empty:
        for _, row in tech.iterrows():
            tid = normalize_text(row.get(t_id_col))
            if not tid:
                continue
            c_calltype = normalize_text(row.get(t_pg_col))
            d_comp = normalize_text(row.get(t_comp_col))

            if "*" in d_comp:
                # wildcard → all BUs with this call type
                for bu in bu_records.values():
                    if bu["call_type"] == c_calltype:
                        bu["technician_ids"].add(tid)
            else:
                # exact match → call type + competency must both match
                for bu in bu_records.values():
                    if bu["call_type"] == c_calltype and bu["competency"] == d_comp:
                        bu["technician_ids"].add(tid)

    # Materialize business_units list
    business_units = []
    for bu in sorted(bu_records.values(), key=lambda x: x["id"]):
        buf_len = bu["buffer_slot_length"]
        try:
            buf_len = int(float(buf_len)) if buf_len is not None else 0
        except Exception:
            buf_len = 0

        business_units.append(
            {
                "id": bu["id"],
                "buffer_slot_count": 0,
                "buffer_slot_length": buf_len,
                "technician_ids": sorted(bu["technician_ids"]),
            }
        )

    # --- Fill eligible_technicians for each appointment from its BU ---
    bu_to_tech = {b["id"]: b["technician_ids"] for b in business_units}
    for appt in appointments:
        techs = bu_to_tech.get(appt["business_unit_id"], [])
        appt["eligible_technicians"] = [{"id": tid, "score": 1} for tid in techs]

    # --- Matrix (convert all values to int) ---
    def convert_matrix_values_to_int(matrix_obj: Dict) -> Dict:
        result = {}
        for from_id, inner in matrix_obj.items():
            result[from_id] = {}
            for to_id, val in inner.items():
                try:
                    result[from_id][to_id] = int(float(val))
                except Exception:
                    result[from_id][to_id] = 0
        return result

    def convert_matrix_values_to_int_duration(matrix_obj: Dict) -> Dict:
        result = {}
        for from_id, inner in matrix_obj.items():
            result[from_id] = {}
            for to_id, val in inner.items():
                try:
                    if (val == 0 or val is None) and from_id != to_id:
                        result[from_id][to_id] = 0
                    result[from_id][to_id] = int(float(val)) if from_id == to_id else max(int(float(val)), 300)
                except Exception:
                    result[from_id][to_id] = 0
        return result

    matrix = {
        "duration": convert_matrix_values_to_int_duration(duration_obj.get("duration", {})),
        "distance": convert_matrix_values_to_int(distance_obj.get("distance", {})),
    }

    # --- Final payload ---
    payload: Dict = {
        "appointments": appointments,
        "zones": zones,
        "technicians": technicians,
        "options": options,
        "matrix": matrix,
        "business_units": business_units,
        "board_id": "",
    }

    Path(OUTPUT_JSON).write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(
        f"✅ Wrote {OUTPUT_JSON} with "
        f"{len(appointments)} appointments, "
        f"{len(technicians)} technicians, "
        f"{len(zones)} zones, "
        f"{len(business_units)} business units."
    )

if __name__ == "__main__":
    main()
