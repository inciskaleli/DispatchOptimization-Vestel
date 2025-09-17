# -*- coding: utf-8 -*-
"""
Batch process: Run json_to_excel.py for all result/dataloader JSON pairs in a folder.
Only files where the suffix (after 'result-'/'dataloader-') matches are processed.
"""

from pathlib import Path
import subprocess

# ---------------- CONFIG ----------------
JSON_TO_EXCEL_SCRIPT = Path(r"json_to_excel.py")
INPUT_JSON_DIR = Path(r".\scenarios\capacity_weight=1\technician_capacity_100-driving_speed_dynamic\diff_slots\window_variants-13")
RESULT_JSON_PATTERN = "result-*_pruned_*.json"
DATALOADER_JSON_PATTERN = "dataloader-*_pruned_*.json"
# ----------------------------------------

def main():
    result_files = sorted(INPUT_JSON_DIR.glob(RESULT_JSON_PATTERN))
    dataloader_files = sorted(INPUT_JSON_DIR.glob(DATALOADER_JSON_PATTERN))

    print(f"Found {len(result_files)} result JSONs and {len(dataloader_files)} dataloader JSONs.")

    # result/dataloader eşleştirmesi
    for result_file in result_files:
        result_suffix = result_file.name.replace("result-", "")
        matching_dataloader = [f for f in dataloader_files if f.name.replace("dataloader-", "") == result_suffix]
        if not matching_dataloader:
            print(f"⚠️ No matching dataloader for {result_file.name}, skipping.")
            continue
        dataloader_file = matching_dataloader[0]

        print(f"Processing:\n  Result: {result_file.name}\n  Dataloader: {dataloader_file.name}")

        subprocess.run([
            "python", str(JSON_TO_EXCEL_SCRIPT),
            "--input_result", str(result_file),
            "--input_dataloader", str(dataloader_file)
        ], check=True)

    print("✅ All JSONs processed.")

if __name__ == "__main__":
    main()
