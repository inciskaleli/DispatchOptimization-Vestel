# -*- coding: utf-8 -*-
import json
from pathlib import Path
from copy import deepcopy

day = 14
# -------- CONFIG --------
INPUT_RESULT_JSON  = Path(__file__).parent /f"dataloader-{day}_03_2025_pruned.json"   # input JSON dosyası
OUTPUT_RESULT_JSON_DIR = Path(__file__).parent /f"window_variants-{day}/" # output JSON dosyalarının kaydedileceği klasör
APPOINTMENT_ID = "9098841329"        # değiştirmek istediğin appointment ID
# ------------------------

# Yeni arrival windowlar
NEW_WINDOWS = [
    ("08:00:00", "10:00:00"),
    ("10:00:00", "12:00:00"),
    ("13:00:00", "15:00:00"),
    ("15:00:00", "17:00:00"),
    ("17:00:00", "19:00:00"),
    ("19:00:00", "21:00:00"),
]

# Ensure output directory exists
Path(OUTPUT_RESULT_JSON_DIR).mkdir(parents=True, exist_ok=True)

# Load input JSON
data = json.loads(Path(INPUT_RESULT_JSON).read_text(encoding="utf-8"))


# Find the appointment to modify
appt_index = None
for i, appt in enumerate(data.get("appointments", [])):
    if str(appt.get("id")) == str(APPOINTMENT_ID):
        appt_index = i
        break

if appt_index is None:
    raise ValueError(f"Appointment ID {APPOINTMENT_ID} not found in input JSON.")

# Create 6 new JSONs with updated arrival windows
for start_time, end_time in NEW_WINDOWS:
    new_data = deepcopy(data)
    appt = new_data["appointments"][appt_index]

    # Extract date from original start (assume ISO format)
    orig_start = appt["arrival_window"]["start"]
    date_str = orig_start.split("T")[0]

    # Set new arrival window
    appt["arrival_window"]["start"] = f"{date_str}T{start_time}.000Z"
    appt["arrival_window"]["end"] = f"{date_str}T{end_time}.000Z"
    start_hh = start_time.split(":")[0]
    end_hh   = end_time.split(":")[0]

    # Create output filename
    output_file = Path(OUTPUT_RESULT_JSON_DIR) / f"dataloader-{day}_03_2025_pruned_{APPOINTMENT_ID}_{start_hh}-{end_hh}.json"

    # Write new JSON
    with open(output_file, "w", encoding="utf-8") as f_out:
        json.dump(new_data, f_out, indent=2, ensure_ascii=False)

    print(f"✅ Created: {output_file}")
