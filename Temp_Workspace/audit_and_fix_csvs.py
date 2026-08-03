import os, glob, csv

datas_dir = r"c:\Users\PC\Projects\TP2\Assets\Datas"
proj_dir = r"c:\Users\PC\Projects\TP2"

csv_files = glob.glob(os.path.join(datas_dir, "*.csv"))
print(f"Found {len(csv_files)} CSV files in {datas_dir}:\n")

report = []

for csv_path in sorted(csv_files):
    file_name = os.path.basename(csv_path)
    meta_path = csv_path + ".meta"
    meta_ok = os.path.exists(meta_path)
    
    with open(csv_path, "r", encoding="utf-8-sig", errors="ignore") as f:
        lines = [line.strip() for line in f if line.strip()]

    if not lines:
        continue

    header_raw = lines[0]
    headers = [h.strip() for h in header_raw.split(",")]

    # Rule 1: SkillData.csv skillid -> idx
    fixed_headers = []
    for h in headers:
        if file_name.lower() == "skilldata.csv" and h.lower() == "skillid":
            fixed_headers.append("idx")
        else:
            fixed_headers.append(h.lower())  # Rule 2: lowercase headers

    new_header_line = ",".join(fixed_headers)

    lines[0] = new_header_line
    with open(csv_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")

    report.append(f"OK: {file_name}: Header=['{new_header_line}'], MetaExists={meta_ok}")

print("\n".join(report))

# Rule 4: ResourceData.csv path verification
print("\n--- ResourceData.csv Path Verification ---")
res_csv_path = os.path.join(datas_dir, "ResourceData.csv")
if os.path.exists(res_csv_path):
    with open(res_csv_path, "r", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            idx = row.get("idx", "")
            path_val = row.get("path", "")
            full_asset_path = os.path.normpath(os.path.join(proj_dir, path_val)) if path_val else ""
            exists = os.path.exists(full_asset_path) if full_asset_path else False
            print(f"  Idx={idx}: Path='{path_val}' -> Exists={exists}")

print("\nCSV Audit and Correction Complete!")
