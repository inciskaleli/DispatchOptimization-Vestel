import pandas as pd
import json

# --- CONFIG ---
INPUT_FILE = "dist_matrix.txt"     # your txt/tsv matrix file
OUTPUT_FILE = "duration.json" # output json
# ---------------

# Load as dataframe (first row = column ids, first col = row ids)
df = pd.read_csv(INPUT_FILE, sep="\t", header=None, dtype=str)
df = df.replace("v", ",", regex=True)

col_ids = df.iloc[0, 1:].tolist()
row_ids = df.iloc[1:, 0].tolist()

# Assign headers
df = df.iloc[1:, 1:]
df.columns = col_ids
df.index = row_ids

# Convert to numbers
df = df.apply(pd.to_numeric, errors="coerce").fillna(0)

# Build nested JSON
distance = {}
for r in df.index:
    inner = {}
    for c, val in df.loc[r].items():   # now val is guaranteed scalar
        try:
            val = float(val) * 0.06 
            # val = 2400
            if val.is_integer():
                val = int(val) 
        except Exception:
            val = 2400
        inner[str(c)] = val  # convert to minutes
    distance[str(r)] = inner

result = {"duration": distance}

# Write JSON
with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

print(f"✅ Wrote {OUTPUT_FILE}")


###################################################################################


# --- CONFIG ---
INPUT_FILE = "dist_matrix.txt"     # your txt/tsv matrix file
OUTPUT_FILE = "distance.json" # output json
# ---------------

# Load as dataframe (first row = column ids, first col = row ids)
df = pd.read_csv(INPUT_FILE, sep="\t", header=None, dtype=str)
df = df.replace("v", ",", regex=True)

col_ids = df.iloc[0, 1:].tolist()
row_ids = df.iloc[1:, 0].tolist()

# Assign headers
df = df.iloc[1:, 1:]
df.columns = col_ids
df.index = row_ids

# Convert to numbers
df = df.apply(pd.to_numeric, errors="coerce").fillna(0)

# Build nested JSON
distance = {}
for r in df.index:
    inner = {}
    for c, val in df.loc[r].items():   # now val is guaranteed scalar
        try:
            val = float(val)
            # val = 2400
            if val.is_integer():
                val = int(val) 
        except Exception:
            val = 2400
        inner[str(c)] = val  # convert to minutes
    distance[str(r)] = inner

result = {"distance": distance}

# Write JSON
with open(OUTPUT_FILE, "w", encoding="utf-8") as f:
    json.dump(result, f, ensure_ascii=False, indent=2)

print(f"✅ Wrote {OUTPUT_FILE}")

