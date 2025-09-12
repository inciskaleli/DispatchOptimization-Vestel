# -*- coding: utf-8 -*-
"""
Creates a single Excel file with:
  - Assignments, Nonavailabilities, Technicians, Meta
  - Suggestions_* (if present)
  - Tech_Load_By_Slot (counts & minutes)
  - appt_eligibility_by_slot  <-- NEW

'eligible_with_free_space' = for each appointment and slot on that day:
  number of ELIGIBLE technicians who would have at least the appointment's
  duration minutes available in that slot IF THIS APPOINTMENT WERE NOT ASSIGNED.
"""

import json
import os
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Tuple

import pandas as pd
from openpyxl.styles import Font

# -------- CONFIG --------
INPUT_RESULT_JSON  = "./scenarios/technician_capacity_100-driving_speed_60kmh/result-10_03_2025.json"
INPUT_DATALOADER   = "./scenarios/technician_capacity_100-driving_speed_60kmh/dataloader-10_03_2025.json"
OUTPUT_XLSX        = "./scenarios/technician_capacity_100-driving_speed_60kmh/result-10_03_2025.xlsx"
SLOT_CAPACITY_MIN  = 120  # 2 hours per slot
# ------------------------

SLOT_DEFS: List[Tuple[str, int, int]] = [
    ("08:00-10:00", 8, 10),
    ("10:00-12:00", 10, 12),
    ("13:00-15:00", 13, 15),
    ("15:00-17:00", 15, 17),
    ("17:00-19:00", 17, 19),
    ("19:00-21:00", 19, 21),
    ("21:00-23:00", 21, 23),
]
SLOT_LABELS = [s[0] for s in SLOT_DEFS]


# ----------------- Common helpers -----------------
def _to_list(v):
    return v if isinstance(v, list) else ([] if v is None else [v])


def parse_iso_z(s: str) -> datetime:
    """Parse '...Z' or '...+00:00' → aware UTC datetime."""
    s = (s or "").strip().replace(".000Z", "Z")
    if s.endswith("Z"):
        s = s[:-1] + "+00:00"
    return datetime.fromisoformat(s)


def slot_bounds_for_date(d: datetime) -> list[tuple[datetime, datetime, str]]:
    base = datetime(d.year, d.month, d.day, tzinfo=timezone.utc)
    out = []
    for label, h1, h2 in SLOT_DEFS:
        sdt = base.replace(hour=h1, minute=0, second=0, microsecond=0)
        edt = base.replace(hour=h2, minute=0, second=0, microsecond=0)
        out.append((sdt, edt, label))
    return out


def label_for_window(start_iso: str, end_iso: str) -> str | None:
    """Return slot label if the arrival window exactly matches a standard slot."""
    try:
        s = parse_iso_z(start_iso)
        e = parse_iso_z(end_iso)
    except Exception:
        return None
    for sdt, edt, label in slot_bounds_for_date(s):
        if s == sdt and e == edt:
            return label
    return None


def overlap_minutes(a_start: datetime, a_end: datetime, b_start: datetime, b_end: datetime) -> int:
    latest_start = max(a_start, b_start)
    earliest_end = min(a_end, b_end)
    delta = (earliest_end - latest_start).total_seconds() / 60.0
    return max(0, int(round(delta)))


# ----------------- Flatten result JSON -----------------
def flatten_assignments(assignments: List[Dict[str, Any]]) -> pd.DataFrame:
    rows = []
    for a in assignments or []:
        route = a.get("route") or {}
        tech_ids = _to_list(a.get("technician_ids"))
        rows.append({
            "appointment_id": str(a.get("id")),
            "status": a.get("status"),
            "start": a.get("start"),
            "end": a.get("end"),
            "technician_ids": tech_ids,
            "route_distance": route.get("distance"),
            "route_duration": route.get("duration"),
        })
    return pd.DataFrame(rows)


def flatten_nonavail(nonavail: List[Dict[str, Any]]) -> pd.DataFrame:
    rows = []
    for n in nonavail or []:
        nas = n.get("non_availabilities") or []
        if not nas:
            rows.append({"technician_id": n.get("technician_id"),
                         "na_start": None, "na_end": None, "reason": None})
        for item in nas:
            rows.append({
                "technician_id": n.get("technician_id"),
                "na_start": item.get("start"),
                "na_end": item.get("end"),
                "reason": item.get("reason") if isinstance(item, dict) else None,
            })
    return pd.DataFrame(rows)


def flatten_technicians(techs: List[Dict[str, Any]]) -> pd.DataFrame:
    rows = []
    for t in techs or []:
        lb = t.get("lunch_break") or {}
        rows.append({
            "technician_id": t.get("id"),
            "lunch_start": lb.get("start"),
            "lunch_end": lb.get("end"),
        })
    return pd.DataFrame(rows)


def flatten_suggestions(sugg: Dict[str, Any]) -> Dict[str, pd.DataFrame]:
    dfs = {}
    if not sugg:
        return dfs
    overtime = sugg.get("overtime") or []
    rows_ot = []
    for o in overtime:
        appts = _to_list(o.get("appointment_ids"))
        rows_ot.append({
            "technician_id": o.get("technician_id"),
            "appointment_ids": ",".join(appts)
        })
    if rows_ot:
        dfs["Suggestions_Overtime"] = pd.DataFrame(rows_ot)
    buffer_slots = sugg.get("buffer_slots") or []
    rows_bs = []
    for b in buffer_slots:
        rows_bs.append(b if isinstance(b, dict) else {"value": b})
    if rows_bs:
        dfs["Suggestions_BufferSlots"] = pd.DataFrame(rows_bs)
    return dfs


# --------- Tech load by slot (counts & minutes) ----------
def _slot_label_for_hour(h: int) -> str | None:
    for label, start_h, end_h in SLOT_DEFS:
        if start_h <= h < end_h:
            return label
    return None


def _prep_slot_base(assignments_df: pd.DataFrame) -> pd.DataFrame:
    df = assignments_df.copy()
    if df.empty:
        return df
    df = df.explode("technician_ids").rename(columns={"technician_ids": "tech_id"})
    df = df[df["tech_id"].notna() & (df["tech_id"].astype(str) != "")]
    starts = pd.to_datetime(df["start"], errors="coerce", utc=True)
    ends   = pd.to_datetime(df["end"], errors="coerce", utc=True)
    df["start_dt"] = starts
    df["end_dt"]   = ends
    df["duration_min"] = ((ends - starts).dt.total_seconds() / 60.0).fillna(0).clip(lower=0)
    df["slot"] = starts.dt.hour.apply(lambda h: _slot_label_for_hour(int(h)) if pd.notna(h) else None)
    return df[df["slot"].isin(SLOT_LABELS)]


def build_tech_slot_counts(assignments_df: pd.DataFrame) -> pd.DataFrame:
    if assignments_df.empty:
        return pd.DataFrame(columns=["Tech Id"] + SLOT_LABELS)
    df = _prep_slot_base(assignments_df)
    pivot = (df.groupby(["tech_id", "slot"])
               .size()
               .unstack(fill_value=0)
               .reindex(columns=SLOT_LABELS, fill_value=0)
               .reset_index()
               .rename(columns={"tech_id": "Tech Id"}))
    for col in SLOT_LABELS:
        if col not in pivot.columns:
            pivot[col] = 0
    return pivot[["Tech Id"] + SLOT_LABELS]


def build_tech_slot_minutes(assignments_df: pd.DataFrame) -> pd.DataFrame:
    if assignments_df.empty:
        return pd.DataFrame(columns=["Tech Id"] + SLOT_LABELS)
    df = _prep_slot_base(assignments_df)
    pivot = (df.groupby(["tech_id", "slot"])["duration_min"]
               .sum()
               .unstack(fill_value=0.0)
               .reindex(columns=SLOT_LABELS, fill_value=0.0)
               .reset_index()
               .rename(columns={"tech_id": "Tech Id"}))
    for col in SLOT_LABELS:
        pivot[col] = pivot.get(col, 0).round(0).astype(int)
    return pivot[["Tech Id"] + SLOT_LABELS]


# ------------- Build per-tech-per-date-per-slot used minutes -----------------
def tech_slot_used_minutes_map(assignments_df: pd.DataFrame) -> Dict[str, Dict[Tuple[str, str], int]]:
    """
    Return {tech_id: {(date_str, slot_label): minutes_used}}
    Minutes are computed by precise overlap with the slot on that date.
    """
    out: Dict[str, Dict[Tuple[str, str], int]] = {}
    if assignments_df.empty:
        return out

    df = assignments_df.copy()
    df = df.explode("technician_ids").rename(columns={"technician_ids": "tech_id"})
    df = df[df["tech_id"].notna() & (df["tech_id"].astype(str) != "")]
    df["start_dt"] = pd.to_datetime(df["start"], errors="coerce", utc=True)
    df["end_dt"]   = pd.to_datetime(df["end"], errors="coerce", utc=True)

    for _, row in df.iterrows():
        tech = str(row["tech_id"])
        s = row["start_dt"]; e = row["end_dt"]
        if pd.isna(s) or pd.isna(e):
            continue
        date_key = s.date().isoformat()
        for sdt, edt, label in slot_bounds_for_date(s):
            mins = overlap_minutes(s, e, sdt, edt)
            if mins > 0:
                out.setdefault(tech, {})
                key = (date_key, label)
                out[tech][key] = out[tech].get(key, 0) + mins
    return out


def appt_assigned_overlap_by_tech(assignments_df: pd.DataFrame) -> Dict[Tuple[str, str, str, str], int]:
    """
    (appt_id, tech_id, date_str, slot_label) -> minutes of THIS appt that fall in that slot for that tech.
    Used to "remove" the appt when simulating not-assigned.
    """
    out: Dict[Tuple[str, str, str, str], int] = {}
    if assignments_df.empty:
        return out

    df = assignments_df.copy()
    df = df.explode("technician_ids").rename(columns={"technician_ids": "tech_id"})
    df = df[df["tech_id"].notna() & (df["tech_id"].astype(str) != "")]
    df["start_dt"] = pd.to_datetime(df["start"], errors="coerce", utc=True)
    df["end_dt"]   = pd.to_datetime(df["end"], errors="coerce", utc=True)

    for _, row in df.iterrows():
        apid = str(row.get("appointment_id"))
        tech = str(row["tech_id"])
        s = row["start_dt"]; e = row["end_dt"]
        if pd.isna(s) or pd.isna(e):
            continue
        date_key = s.date().isoformat()
        for sdt, edt, label in slot_bounds_for_date(s):
            mins = overlap_minutes(s, e, sdt, edt)
            if mins > 0:
                out[(apid, tech, date_key, label)] = mins
    return out


# ------------- appt_eligibility_by_slot sheet -----------------
def build_appt_eligibility_by_slot(appts: List[dict],
                                   tech_used_map: Dict[str, Dict[Tuple[str, str], int]],
                                   appt_overlap_map: Dict[Tuple[str, str, str, str], int]) -> pd.DataFrame:
    rows = []
    # precompute appointment durations & arrival labels
    appt_duration: Dict[str, int] = {}
    appt_arrival_label: Dict[str, str | None] = {}
    appt_date_key: Dict[str, str] = {}

    for ap in appts:
        apid = str(ap.get("id", ""))
        try:
            js = parse_iso_z(ap.get("start"))
            je = parse_iso_z(ap.get("end"))
            dur = max(0, int(round((je - js).total_seconds() / 60.0)))
        except Exception:
            dur = 0
        appt_duration[apid] = dur
        aw = ap.get("arrival_window") or {}
        appt_arrival_label[apid] = label_for_window(aw.get("start", ""), aw.get("end", "")) if aw else None
        try:
            aw_s = parse_iso_z(aw.get("start", "")) if aw else parse_iso_z(ap.get("start"))
            appt_date_key[apid] = aw_s.date().isoformat()
        except Exception:
            appt_date_key[apid] = None

    for ap in appts:
        apid = str(ap.get("id", ""))
        bu   = ap.get("business_unit_id") or ""
        elig = [str(x.get("id")) for x in (ap.get("eligible_technicians") or []) if x.get("id") is not None]
        total_eligible = len(elig)
        dur_t = appt_duration.get(apid, 0)
        date_key = appt_date_key.get(apid)
        if not date_key:
            continue

        # find the UTC date to iterate slot bounds
        try:
            aw_s = parse_iso_z((ap.get("arrival_window") or {}).get("start", "")) if ap.get("arrival_window") else parse_iso_z(ap.get("start"))
        except Exception:
            continue

        arrival_label = appt_arrival_label.get(apid)

        for sdt, edt, label in slot_bounds_for_date(aw_s):
            free_count = 0
            for tid in elig:
                used = tech_used_map.get(tid, {}).get((date_key, label), 0)
                # subtract this appointment's own overlap minutes for this tech in that slot (simulate "not assigned")
                used_minus_this = used - appt_overlap_map.get((apid, tid, date_key, label), 0)
                # available minutes
                avail = SLOT_CAPACITY_MIN - max(0, used_minus_this)
                if avail >= dur_t and dur_t > 0:
                    free_count += 1

            rows.append({
                "slot": label,
                "appointment_no": apid,
                "business_unit_id": bu,
                "total_eligible_technicians": total_eligible,
                "eligible_with_free_space": free_count,
                "occupancy_rate": f"{(total_eligible - free_count) / total_eligible:.1f}" if total_eligible > 0 else None,
                "is_arrival_window_row": (label == arrival_label),
            })

    df = pd.DataFrame(rows, columns=[
        "slot",
        "appointment_no",
        "business_unit_id",
        "total_eligible_technicians",
        "eligible_with_free_space",
        "occupancy_rate",
        "is_arrival_window_row",
    ])
    return df


# ----------------- MAIN -----------------
def main():
    # Load JSONs
    result_obj = json.loads(Path(INPUT_RESULT_JSON).read_text(encoding="utf-8"))
    dl_obj     = json.loads(Path(INPUT_DATALOADER).read_text(encoding="utf-8"))

    assignments_df = flatten_assignments(result_obj.get("assignments"))
    nonavail_df    = flatten_nonavail(result_obj.get("nonavailibilities"))
    techs_df       = flatten_technicians(result_obj.get("technicians"))
    sugg_dfs       = flatten_suggestions(result_obj.get("suggestions"))

    # Meta
    meta_df = pd.DataFrame({
        "assignments_count": [len(assignments_df)],
        "technicians_count": [techs_df["technician_id"].nunique() if not techs_df.empty else 0],
        "nonavail_rows": [len(nonavail_df)],
    })

    # Tech load by slot
    tech_slot_counts_df    = build_tech_slot_counts(assignments_df)
    tech_slot_minutes_df   = build_tech_slot_minutes(assignments_df)

    # Maps for appt_eligibility_by_slot
    tech_used_map   = tech_slot_used_minutes_map(assignments_df)
    appt_overlap_map = appt_assigned_overlap_by_tech(assignments_df)
    appts = dl_obj.get("appointments", [])
    appt_elig_df = build_appt_eligibility_by_slot(appts, tech_used_map, appt_overlap_map)

    # Write all sheets in ONE file
    with pd.ExcelWriter(OUTPUT_XLSX, engine="openpyxl") as writer:
        assignments_df.to_excel(writer, index=False, sheet_name="Assignments")
        nonavail_df.to_excel(writer, index=False, sheet_name="Nonavailabilities")
        techs_df.to_excel(writer, index=False, sheet_name="Technicians")
        meta_df.to_excel(writer, index=False, sheet_name="Meta")

        for name, df in sugg_dfs.items():
            df.to_excel(writer, index=False, sheet_name=name[:31])

        # Tech_Load_By_Slot (two tables one below another)
        sheet = "Tech_Load_By_Slot"
        start_row = 0
        pd.DataFrame({"Tech Load (Counts by Slot)": [""]}).to_excel(
            writer, index=False, header=False, sheet_name=sheet, startrow=start_row
        )
        start_row += 2
        tech_slot_counts_df.to_excel(writer, index=False, sheet_name=sheet, startrow=start_row)
        start_row += len(tech_slot_counts_df) + 3
        pd.DataFrame({"Tech Load (Duration minutes by Slot)": [""]}).to_excel(
            writer, index=False, header=False, sheet_name=sheet, startrow=start_row
        )
        start_row += 2
        tech_slot_minutes_df.to_excel(writer, index=False, sheet_name=sheet, startrow=start_row)

        # appt_eligibility_by_slot
        out_df = appt_elig_df.drop(columns=["is_arrival_window_row"])
        sheet2 = "appt_eligibility_by_slot"
        out_df.to_excel(writer, index=False, sheet_name=sheet2)

        # Bold arrival window rows
        try:
            wb = writer.book
            ws = writer.sheets[sheet2]
            bold_font = Font(bold=True)
            mask = appt_elig_df["is_arrival_window_row"].tolist()
            for i, is_bold in enumerate(mask, start=2):  # +1 header, +1 1-based
                if is_bold:
                    for col in range(1, ws.max_column + 1):
                        ws.cell(row=i, column=col).font = bold_font
        except Exception:
            pass

    print(f"✅ Wrote Excel with all sheets to: {OUTPUT_XLSX}")


if __name__ == "__main__":
    main()
