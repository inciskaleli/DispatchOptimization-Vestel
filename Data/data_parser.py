# -*- coding: utf-8 -*-
"""
Builds the requested dataloader JSON from the given Excel and existing
distance/duration matrices.

This version:
- Uses RAPOR sheet column 'Z' as the appointment job start time
- Sets job end = job start + adjusted duration
- Leaves arrival windows unchanged (still parsed from 'Randevu Tarih saat')
- Cleans trailing zeros in coordinate strings
- Normalizes coord-like keys in matrices
- Appends '_fixed_arrivals' ONCE to the output file name
- Parses 'Z' start times with explicit formats to avoid pandas dayfirst warnings
- Reads RAPOR sheet column 'T' as technician(s) for each appointment and sets "technician_ids"
"""

import json
import re
from pathlib import Path
from typing import Dict, Tuple, Optional, List

import pandas as pd
from dateutil import parser as dtparser

sayi = 14
# --------------------- CONFIG ---------------------
INPUT_XLSX = "28002357 Yiğit Klima 10-14 Mart 2025_SON Veri_Düzeltilmiştir.xlsx"
SHEET_RAPOR = f"RAPOR-{sayi}_03_2025"
SHEET_TECH = "Teknisyen Yetkinlikleri"
SHEET_UGCTS = "Ürün grubu çağrı tipi süre"

DISTANCE_JSON = "distance.json"
DURATION_JSON = "duration.json"

OUTPUT_JSON = f"./scenarios/capacity_weight=1/technician_capacity_100-driving_speed_dynamic/dataloader-{sayi}_03_2025_fixed_arrivals.json"

# Which Excel columns (letters) to use in RAPOR sheet:
START_TIME_COLUMN_LETTER = "Z"   # job start time
TECH_COLUMN_LETTER = "T"         # technician of an appointment
# --------------------------------------------------

def normalize_text(s: Optional[str]) -> str:
    if s is None:
        return ""
    return " ".join(str(s).strip().split())

def _to_z_no_ms(dt_like) -> str:
    ts = pd.to_datetime(dt_like)
    return ts.strftime("%Y-%m-%dT%H:%M:%SZ")

def _to_z_ms(dt_like) -> str:
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

def normalize_lonlat_key(s: Optional[str]) -> Optional[str]:
    """
    'lon,lat' ya da 'lon;lat' gelen stringi:
      - boşlukları temizleyip,
      - float'a çevirip,
      - en fazla 6 ondalıkla yazıp,
      - sonda kalan gereksiz 0 ve '.' karakterlerini kırparak
    'lon,lat' formatında geri döndürür.
    """
    if not isinstance(s, str):
        return None
    s = s.strip().replace(";", ",")
    if "," not in s:
        return s
    lon_s, lat_s = [p.strip() for p in s.split(",", 1)]
    try:
        lon = float(lon_s)
        lat = float(lat_s)
        def _trim(x: float) -> str:
            t = f"{x:.6f}".rstrip("0").rstrip(".")
            # Tam sayı ise '27.' gibi kalmasın diye:
            return t if t else "0"
        return f"{_trim(lon)},{_trim(lat)}"
    except Exception:
        return s

# ---------- Coordinate cleaners ----------
_COORD_NUM_RE = re.compile(r"^[\+\-]?\d+(?:\.\d+)?$")

def _strip_trailing_zeros(num_str: str) -> str:
    s = num_str.strip()
    if "." in s:
        s = s.rstrip("0").rstrip(".")
    return s

def _is_coord_string(s: str) -> bool:
    if not isinstance(s, str):
        return False
    t = s.strip()
    if "," in t:
        part1, part2 = [p.strip() for p in t.split(",", 1)]
    elif ";" in t:
        part1, part2 = [p.strip() for p in t.split(";", 1)]
    else:
        return False
    return bool(_COORD_NUM_RE.match(part1)) and bool(_COORD_NUM_RE.match(part2))

def clean_coord_str(coord: Optional[str]) -> Optional[str]:
    if not isinstance(coord, str):
        return coord
    s = coord.strip()
    if not s:
        return s
    sep = "," if "," in s else (";" if ";" in s else None)
    if not sep:
        return s
    a, b = [p.strip() for p in s.split(sep, 1)]
    if not (_COORD_NUM_RE.match(a) and _COORD_NUM_RE.match(b)):
        return s
    a2 = _strip_trailing_zeros(a)
    b2 = _strip_trailing_zeros(b)
    return f"{a2}, {b2}"

def remap_coord_keys_if_any(d: Dict[str, Dict[str, int]]) -> Dict[str, Dict[str, int]]:
    out: Dict[str, Dict[str, int]] = {}
    for from_k, inner in d.items():
        new_from = clean_coord_str(from_k) if _is_coord_string(from_k) else from_k
        out[new_from] = {}
        for to_k, v in inner.items():
            new_to = clean_coord_str(to_k) if _is_coord_string(to_k) else to_k
            out[new_from][new_to] = v
    return out
# -------------------------------------------------

# ---------- Arrival-window parser (unchanged) ----------
def _normalize_aw(s: str) -> str:
    if not isinstance(s, str):
        return ""
    s = (s.replace("\u2012", "-")
           .replace("\u2013", "-")
           .replace("\u2014", "-")
           .replace("\u2212", "-"))
    s = s.replace("\xa0", " ")
    s = " ".join(s.split())
    return s

def parse_arrival_window(yy: str) -> Tuple[Optional[str], Optional[str]]:
    if not isinstance(yy, str):
        return None, None
    s = _normalize_aw(yy)

    m_date = re.search(r"(\d{1,2}[./-]\d{1,2}[./-]\d{2,4})", s)
    if not m_date:
        return None, None
    date_part = m_date.group(1)
    try:
        d = dtparser.parse(date_part, dayfirst=True).date()
    except Exception:
        return None, None

    times = re.findall(r"\b(\d{1,2}:\d{2}(?::\d{2})?)\b", s)
    if len(times) < 2:
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

# ---------- Helper: Excel letter -> zero-based index ----------
def excel_col_to_index(col_letter: str) -> int:
    s = col_letter.strip().upper()
    if not s or not all('A' <= ch <= 'Z' for ch in s):
        raise ValueError(f"Invalid Excel column letter: {col_letter}")
    idx = 0
    for ch in s:
        idx = idx * 26 + (ord(ch) - ord('A') + 1)
    return idx - 1

# ---------- NEW: robust, warning-free job-start parser ----------
_YMD_HMS = re.compile(r"^\s*(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})(?::(\d{2}))?\s*$")
_DMY_HMS = re.compile(r"^\s*(\d{1,2})[./-](\d{1,2})[./-](\d{4})\s+(\d{1,2}):(\d{2})(?::(\d{2}))?\s*$")
_ONLY_DATE_YMD = re.compile(r"^\s*\d{4}-\d{2}-\d{2}\s*$")
_ONLY_DATE_DMY = re.compile(r"^\s*\d{1,2}[./-]\d{1,2}[./-]\d{4}\s*$")

def parse_job_start(raw) -> Optional[pd.Timestamp]:
    """
    Parse RAPOR 'Z' column value into a Timestamp with explicit formats when possible,
    falling back safely. Handles:
      - 'YYYY-MM-DD HH:MM[:SS]'
      - 'DD.MM.YYYY HH:MM[:SS]'
      - Date-only variants above (assumes 00:00)
      - Excel serial numbers (days since 1899-12-30)
    """
    if raw is None:
        return None
    s = str(raw).strip()
    if not s:
        return None

    n = pd.to_numeric(s, errors="coerce")
    if pd.notna(n):
        try:
            return pd.to_datetime(n, unit="D", origin="1899-12-30")
        except Exception:
            pass

    if _YMD_HMS.match(s):
        fmt = "%Y-%m-%d %H:%M:%S" if s.count(":") == 2 else "%Y-%m-%d %H:%M"
        return pd.to_datetime(s.replace("T", " "), format=fmt, errors="coerce")

    if _DMY_HMS.match(s):
        s2 = re.sub(r"[/-]", ".", s)
        fmt = "%d.%m.%Y %H:%M:%S" if s2.count(":") == 2 else "%d.%m.%Y %H:%M"
        return pd.to_datetime(s2, format=fmt, errors="coerce")

    if _ONLY_DATE_YMD.match(s):
        return pd.to_datetime(s, format="%Y-%m-%d", errors="coerce")

    if _ONLY_DATE_DMY.match(s):
        s2 = re.sub(r"[/-]", ".", s)
        return pd.to_datetime(s2, format="%d.%m.%Y", errors="coerce")

    return pd.to_datetime(s, dayfirst=True, errors="coerce")
# -------------------------------------------------------------

def ensure_suffix_once(path_str: str, suffix: str = "_fixed_arrivals") -> str:
    p = Path(path_str)
    stem = p.stem
    if re.search(r"(?:_fixed_arrival|_fixed_arrivals)$", stem, flags=re.IGNORECASE):
        new_stem = re.sub(r"(?:_fixed_arrival|_fixed_arrivals)$", suffix, stem, flags=re.IGNORECASE)
    else:
        new_stem = stem + suffix
    return str(p.with_name(new_stem + p.suffix))

# ---------- NEW: parse tech ids from RAPOR column T ----------
def parse_technician_ids(val) -> List[str]:
    """
    Accept 'T' values like 'T001', 'T001,T002', 'T001; T002', 'T001|T002', 'T001 T002' etc.
    Returns a list of normalized non-empty strings.
    """
    if val is None:
        return []
    s = str(val).strip()
    if not s:
        return []
    # unify separators to comma
    s = re.sub(r"[;|/]", ",", s)
    # also split on whitespace
    parts = []
    for chunk in s.split(","):
        parts.extend(chunk.strip().split())
    return [normalize_text(p) for p in parts if normalize_text(p)]
# -------------------------------------------------------------

def main():
    # --- Load Excel ---
    xls = pd.ExcelFile(INPUT_XLSX, engine="openpyxl")
    rapor = pd.read_excel(xls, sheet_name=SHEET_RAPOR, dtype=str)
    tech = pd.read_excel(xls, sheet_name=SHEET_TECH, dtype=str)
    ugcts = pd.read_excel(xls, sheet_name=SHEET_UGCTS, dtype=str)

    # Determine RAPOR start-time & technician column names by Excel letters
    try:
        start_col_idx = excel_col_to_index(START_TIME_COLUMN_LETTER)
        START_TIME_COLNAME = rapor.columns[start_col_idx]
    except Exception:
        START_TIME_COLNAME = None

    try:
        tech_col_idx = excel_col_to_index(TECH_COLUMN_LETTER)
        TECH_COLNAME = rapor.columns[tech_col_idx]
    except Exception:
        TECH_COLNAME = None

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
        "planning_horizon": {"start": f"2025-03-{sayi}T08:00:00", "end": f"2025-03-{sayi}T23:00:00"},
        "run_time_limit": 120,
        "enable_buffer_slot": False,
        "distance_limit_between_jobs": 400000,
        "start_day_at_office": True,
        "start_point_after_unavailability": "office",
        "respect_scheduled_times": True,
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
    options["office"]["coordinate"] = clean_coord_str(options["office"]["coordinate"])
    ph_start = options["planning_horizon"]["start"]
    ph_end = options["planning_horizon"]["end"]
    office_coord = options["office"]["coordinate"]
    office_zone = options["office"]["zone"]

    # --- Technicians (list for known IDs) ---
    known_tech_ids = sorted(
        {
            normalize_text(t)
            for t in tech.get("Teknisyen no", pd.Series(dtype=str)).tolist()
            if isinstance(t, str) and normalize_text(t)
        }
    )
    technicians = []
    for tid in known_tech_ids:
        technicians.append(
            {
                "id": tid,
                "home": {"coordinate": office_coord, "zone": office_zone},
                "work_time": {"start": ph_start, "end": ph_end},
                "non_availabilities": [],
                "name": tid,
            }
        )

    # --- Appointments ---
    appointments: List[Dict] = []
    dropped = []

    KEEP_IF_NO_DURATION = False
    DEFAULT_DURATION_MIN = 60
    KEEP_IF_NO_WINDOW = False

    for idx, r in rapor.iterrows():
        row_id = str(r.get("Teyit No") or f"row{idx}")

        # Coordinate / zone
        loc_coord = parse_coord_latlon_to_lonlat_str(r.get("Müşteri Koordinat"))
        zone_id = normalize_text(r.get("Müşteri İlçe"))
        if loc_coord:
            loc_coord = clean_coord_str(loc_coord)

        # Arrival window (unchanged)
        start_iso, end_iso = parse_arrival_window(r.get("Randevu Tarih saat"))
        if (not start_iso or not end_iso):
            if KEEP_IF_NO_WINDOW:
                start_iso = options["planning_horizon"]["start"]
                end_iso   = options["planning_horizon"]["end"]
            else:
                dropped.append((row_id, "no_arrival_window"))
                continue

        # Duration
        call_type = normalize_text(r.get("Çağrı Tipi"))
        product_group = normalize_text(r.get("Ürün grubu adı"))
        duration_min = get_appt_duration_minutes(call_type, product_group, exact_dur, wildcard_dur)
        if duration_min is None:
            if KEEP_IF_NO_DURATION:
                duration_min = DEFAULT_DURATION_MIN
            else:
                dropped.append((row_id, "no_duration_mapping"))
                continue

        # Times
        try:
            arrival_start_ref = _to_z_ms(pd.to_datetime(start_iso))
            arrival_end_ref   = _to_z_ms(pd.to_datetime(end_iso))

            DURATION_DIVISOR = 1
            adjusted_minutes = float(duration_min) / DURATION_DIVISOR
            if not (adjusted_minutes > 0 and adjusted_minutes < 24 * 60 * 30):
                raise ValueError(f"adjusted_minutes out of range: {adjusted_minutes}")

            # Preferred start from RAPOR column Z
            job_start_dt: Optional[pd.Timestamp] = None
            if START_TIME_COLNAME is not None:
                raw_start_val = r.get(START_TIME_COLNAME)
                if raw_start_val is not None and str(raw_start_val).strip():
                    job_start_dt = parse_job_start(raw_start_val)
            if job_start_dt is None or pd.isna(job_start_dt):
                job_start_dt = pd.to_datetime(start_iso)

            job_end_dt = job_start_dt 
            job_start_dt = job_end_dt - pd.to_timedelta(adjusted_minutes, unit="m")
            job_start_ref = _to_z_no_ms(job_start_dt)
            job_end_ref   = _to_z_no_ms(job_end_dt)

        except Exception as e:
            dropped.append((row_id, f"time_build_error:{e}"))
            continue

        # Technician IDs from RAPOR column T
        tech_ids_for_row: List[str] = []
        if TECH_COLNAME is not None:
            tech_ids_for_row = parse_technician_ids(r.get(TECH_COLNAME))

        # Optionally, warn if any are unknown
        unknown = [t for t in tech_ids_for_row if t not in known_tech_ids]
        if unknown:
            print(f"⚠️ Row {row_id}: technician(s) not found in TECH sheet -> {unknown}")

        # Build BU
        bu_id = build_business_unit_id(
            r.get("Çağrı Tipi"),
            r.get("Ürün grubu adı"),
            r.get("Yetkinlik grubu"),
        )

        appt_id = normalize_text(r.get("Teyit No")) or row_id
        appt_name = normalize_text(r.get("Müşteri no"))

        appointments.append(
            {
                "location": {"coordinate": loc_coord, "zone": zone_id or None},
                "arrival_window": {"start": arrival_start_ref, "end": arrival_end_ref},
                "eligible_technicians": [],            # will be filled from BU below
                "id": appt_id,
                "start": job_start_ref,
                "end": job_end_ref,
                "technician_ids": tech_ids_for_row,    # <-- SET FROM COLUMN T
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

    # Zones
    unique_zones = sorted(
        {
            normalize_text(z)
            for z in rapor.get("Müşteri İlçe", pd.Series(dtype=str)).tolist()
            if isinstance(z, str) and normalize_text(z)
        }
    )
    zones = [{"id": z, "can_go_with": []} for z in unique_zones]

    # Business Units from RAPOR
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

    # Buffer slot rules
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

    # Technician eligibility rules from TECH sheet
    t_id_col = "Teknisyen no"
    t_pg_col = "Çağrı Tanım"
    t_comp_col = "Yetkinlik tanım"

    if not tech.empty:
        for _, row in tech.iterrows():
            tid = normalize_text(row.get(t_id_col))
            if not tid:
                continue
            c_calltype = normalize_text(row.get(t_pg_col))
            d_comp = normalize_text(row.get(t_comp_col))

            if "*" in d_comp:
                for bu in bu_records.values():
                    if bu["call_type"] == c_calltype:
                        bu["technician_ids"].add(tid)
            else:
                for bu in bu_records.values():
                    if bu["call_type"] == c_calltype and bu["competency"] == d_comp:
                        bu["technician_ids"].add(tid)

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

    # Fill eligible_technicians from BU
    bu_to_tech = {b["id"]: b["technician_ids"] for b in business_units}
    for appt in appointments:
        techs = bu_to_tech.get(appt["business_unit_id"], [])
        appt["eligible_technicians"] = [{"id": tid, "score": 1} for tid in techs]

    # Matrices
    def convert_matrix_values_to_int(matrix_obj: Dict) -> Dict:
        """
        Distance matrisi: değerleri int'e çevirir, from/to anahtarlarını normalize eder.
        """
        result: Dict[str, Dict[str, int]] = {}
        for from_id, inner in (matrix_obj or {}).items():
            nf = normalize_lonlat_key(from_id) or from_id
            if nf not in result:
                result[nf] = {}
            for to_id, val in (inner or {}).items():
                nt = normalize_lonlat_key(to_id) or to_id
                try:
                    iv = int(float(val))
                except Exception:
                    iv = 0
                # Çakışma olursa son değer kazanır (istersen max/min alacak şekilde değiştirilebilir)
                result[nf][nt] = iv
        return result


    def convert_matrix_values_to_int_duration(matrix_obj: Dict) -> Dict:
        """
        Duration matrisi: değerleri int'e çevirir, from/to anahtarlarını normalize eder.
        Diagonal dışı geçişlere alt sınır uygular (>= 300 sn).
        """
        result: Dict[str, Dict[str, int]] = {}
        for from_id, inner in (matrix_obj or {}).items():
            nf = normalize_lonlat_key(from_id) or from_id
            if nf not in result:
                result[nf] = {}
            for to_id, val in (inner or {}).items():
                nt = normalize_lonlat_key(to_id) or to_id
                try:
                    iv = int(float(val))
                except Exception:
                    iv = 0
                if nf != nt:
                    iv = max(iv, 300)  # diagonal olmayanlar için alt sınır
                result[nf][nt] = iv
        return result


    raw_duration = remap_coord_keys_if_any(duration_obj.get("duration", {}))
    raw_distance = remap_coord_keys_if_any(distance_obj.get("distance", {}))

    matrix = {
        "duration": convert_matrix_values_to_int_duration(raw_duration),
        "distance": convert_matrix_values_to_int(raw_distance),
    }

    # Final payload
    payload: Dict = {
        "appointments": appointments,
        "zones": zones,
        "technicians": technicians,
        "options": options,
        "matrix": matrix,
        "business_units": business_units,
        "board_id": "",
    }

    # Write with '_fixed_arrivals' once
    output_path = ensure_suffix_once(OUTPUT_JSON, "_fixed_arrivals")
    Path(output_path).parent.mkdir(parents=True, exist_ok=True)
    Path(output_path).write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(
        f"✅ Wrote {output_path} with "
        f"{len(appointments)} appointments, "
        f"{len(technicians)} technicians, "
        f"{len(zones)} zones, "
        f"{len(business_units)} business units."
    )

if __name__ == "__main__":
    main()
