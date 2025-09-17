# -*- coding: utf-8 -*-
import subprocess
from pathlib import Path

# -------- CONFIG --------
day = 13
DATA_DIR = Path(rf".\scenarios\capacity_weight=1\technician_capacity_100-driving_speed_dynamic\diff_slots\window_variants-{day}")
# ------------------------

# klasördeki tüm dataloader jsonlarını al
json_files = sorted(DATA_DIR.glob("*.json"))

for input_file in json_files:
    print("a")
    # output dosya adı: result-<same as input>.json
    output_file = DATA_DIR / input_file.name.replace("dataloader", "result")

    # curl komutu
    cmd = [
        "curl",
        "-s",
        "-X", "POST",
        "http://localhost:8080/dispatch_optimizer",
        "-H", "accept: text/plain",
        "-H", "Content-Type: application/json",
        "--data-binary", f"@{input_file}",
        "-o", f"{output_file}"
    ]

    print(f"Çalıştırılıyor: {input_file.name} -> {output_file.name}")
    subprocess.run(cmd, check=True)

print("✅ Tüm dosyalar işlendi.")
