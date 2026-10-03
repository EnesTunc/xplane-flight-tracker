"""Build the airport data used by XPlaneMapReceiver from X-Plane's apt.dat.

Usage:
    python build_airports.py "<path to apt.dat>" [output_dir]

apt.dat locations:
    X-Plane 12: Global Scenery/Global Airports/Earth nav data/apt.dat
    X-Plane 11: Resources/default scenery/default apt dat/Earth nav data/apt.dat

Writes airports.csv and airports.json (ICAO, Name, City, Country, datum_lat, datum_lon)
to output_dir (default: ../XPlaneMapReceiver/XPlaneMapReceiver/Data).
Only land airports (row code 1) that define datum coordinates are included.
"""
import json
import os
import sys

FIELDS = ["ICAO", "Name", "City", "Country", "datum_lat", "datum_lon"]


def clean(value):
    # The receiver splits CSV lines on commas, so keep fields comma-free
    return value.replace(",", " ").strip()


def country_name(value):
    # X-Plane 12 writes "TUR Turkey"; X-Plane 11 writes "Turkey"
    parts = value.split(" ", 1)
    if len(parts) == 2 and parts[0].isupper() and 2 <= len(parts[0]) <= 3:
        return parts[1]
    return value


def parse(apt_dat_path):
    airports = []
    current = None

    def flush():
        if current and current.get("datum_lat") and current.get("datum_lon"):
            airports.append({field: current.get(field, "") for field in FIELDS})

    with open(apt_dat_path, encoding="utf-8", errors="replace") as f:
        for line in f:
            if line.startswith("1 ") or line.startswith("16 ") or line.startswith("17 "):
                flush()
                tokens = line.split(None, 5)
                # Seaplane bases (16) and heliports (17) are skipped
                current = None
                if tokens[0] == "1" and len(tokens) >= 5:
                    current = {"ICAO": tokens[4], "Name": clean(tokens[5]) if len(tokens) > 5 else ""}
            elif current is not None and line.startswith("1302 "):
                tokens = line.split(None, 2)
                if len(tokens) < 3:
                    continue
                key, value = tokens[1], tokens[2].strip()
                if key == "city":
                    current["City"] = clean(value)
                elif key == "country":
                    current["Country"] = clean(country_name(value))
                elif key in ("datum_lat", "datum_lon"):
                    current[key] = value
    flush()
    return airports


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
        here, "..", "XPlaneMapReceiver", "XPlaneMapReceiver", "Data")
    os.makedirs(out_dir, exist_ok=True)

    airports = parse(sys.argv[1])

    with open(os.path.join(out_dir, "airports.csv"), "w", encoding="utf-8", newline="\n") as f:
        f.write(",".join(FIELDS) + "\n")
        for ap in airports:
            f.write(",".join(ap[field] for field in FIELDS) + "\n")

    with open(os.path.join(out_dir, "airports.json"), "w", encoding="utf-8") as f:
        json.dump(airports, f, ensure_ascii=False, indent=2)

    print(f"{len(airports)} airports written to {os.path.abspath(out_dir)}")


if __name__ == "__main__":
    main()
