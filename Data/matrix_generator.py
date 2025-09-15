import pandas as pd

# --- CONFIG ---
input_excel = "28002357 Yiğit Klima 10-14 Mart 2025_SON Veri_Düzeltilmiştir.xlsx"   # your Excel file
output_txt = "appointments.txt"     # desired output file
sheet_name = "RAPOR"                # sheet to read

# Load the Excel file
df = pd.read_excel(input_excel, sheet_name=sheet_name, header=None)

# Collect unique coordinates
unique_coords = set()

for _, row in df.iterrows():
    identifier = str(row[3]).strip()   # Column D (index 3)
    coords = str(row[11]).strip()      # Column L (index 11)

    if ";" in coords or "," in coords:
        try:
            lat, lon = coords.split(";")
        except Exception:
            lat, lon = coords.split(",")
        lat = lat.strip()
        lon = lon.strip()
        unique_coords.add((lon, lat))  # store as tuple (lon, lat)

# Write unique coordinates to file
with open(output_txt, "w") as f:
    for lon, lat in sorted(unique_coords):  # sorted for stable output
        f.write(f"\"{lon}v{lat}\"\t{lon}\t{lat}\n")

    # Add office coordinate once
    f.write("\"27.436587v38.626512\"\t27.436587\t38.626512")
