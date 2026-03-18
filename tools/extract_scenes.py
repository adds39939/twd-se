#!/usr/bin/env python3
"""
Extract scene file names from TWD Definitive Series archives to build
a scene-per-episode map for the resume point editor.

Uses the same ECTT decryption + 4ATT parsing as extract_all_choices.py.
"""

import sys
import os
import io
import json

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ttarch_decrypt import BlowfishV7, parse_ectt_archive
from extract_all_choices import parse_4att, load_key

ARCHIVES_DIR = sys.argv[1] if len(sys.argv) > 1 else os.environ.get(
    "TWD_ARCHIVES",
    r"G:\Games\Steam\steamapps\common\The Walking Dead The Telltale Definitive Series\Archives"
)

EPISODE_ARCHIVES = {
    "WalkingDead101": "WDC_pc_WalkingDead101_data.ttarch2",
    "WalkingDead102": "WDC_pc_WalkingDead102_data.ttarch2",
    "WalkingDead103": "WDC_pc_WalkingDead103_data.ttarch2",
    "WalkingDead104": "WDC_pc_WalkingDead104_data.ttarch2",
    "WalkingDead105": "WDC_pc_WalkingDead105_data.ttarch2",
    "WalkingDead106": "WDC_pc_WalkingDead106_data.ttarch2",
    "WalkingDead201": "WDC_pc_WalkingDead201_data.ttarch2",
    "WalkingDead202": "WDC_pc_WalkingDead202_data.ttarch2",
    "WalkingDead203": "WDC_pc_WalkingDead203_data.ttarch2",
    "WalkingDead204": "WDC_pc_WalkingDead204_data.ttarch2",
    "WalkingDead205": "WDC_pc_WalkingDead205_data.ttarch2",
    "WalkingDead301": "WDC_pc_WalkingDead301_data.ttarch2",
    "WalkingDead302": "WDC_pc_WalkingDead302_data.ttarch2",
    "WalkingDead303": "WDC_pc_WalkingDead303_data.ttarch2",
    "WalkingDead304": "WDC_pc_WalkingDead304_data.ttarch2",
    "WalkingDead305": "WDC_pc_WalkingDead305_data.ttarch2",
    "WalkingDead401": "WDC_pc_WalkingDead401_data.ttarch2",
    "WalkingDead402": "WDC_pc_WalkingDead402_data.ttarch2",
    "WalkingDead403": "WDC_pc_WalkingDead403_data.ttarch2",
    "WalkingDead404": "WDC_pc_WalkingDead404_data.ttarch2",
    "Michonne101": "WDC_pc_WalkingDeadM101_data.ttarch2",
    "Michonne102": "WDC_pc_WalkingDeadM102_data.ttarch2",
    "Michonne103": "WDC_pc_WalkingDeadM103_data.ttarch2",
}


class SuppressOutput:
    def __enter__(self):
        self._stdout = sys.stdout
        sys.stdout = io.TextIOWrapper(io.BytesIO(), encoding='utf-8')
        return self
    def __exit__(self, *args):
        sys.stdout.close()
        sys.stdout = self._stdout


def main():
    key_hex = load_key()
    key_bytes = bytes.fromhex(key_hex)
    cipher = BlowfishV7(key_bytes)

    results = {}

    for episode_id, archive_name in sorted(EPISODE_ARCHIVES.items()):
        archive_path = os.path.join(ARCHIVES_DIR, archive_name)
        if not os.path.exists(archive_path):
            print(f"  SKIP {episode_id}: {archive_name} not found")
            continue

        print(f"  Scanning {episode_id}...", end="", flush=True)
        try:
            with SuppressOutput():
                raw_data = parse_ectt_archive(archive_path, cipher)

            if not raw_data:
                print(" no data")
                continue

            files = parse_4att(raw_data)

            scenes = sorted(set(
                os.path.splitext(os.path.basename(name))[0]
                for name in files
                if name.lower().endswith(".scene")
            ))

            dlogs = sorted(set(
                os.path.basename(name)
                for name in files
                if name.lower().endswith(".dlog")
            ))

            results[episode_id] = {
                "scenes": scenes,
                "dlogs": dlogs,
            }
            print(f" {len(scenes)} scenes, {len(dlogs)} dlogs")
        except Exception as e:
            print(f" ERROR: {e}")

    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "data", "episode_scenes.json")
    with open(output_path, "w") as f:
        json.dump(results, f, indent=2)

    print(f"\nWrote {output_path}")
    for ep, info in sorted(results.items()):
        print(f"  {ep}: {info['scenes']}")


if __name__ == "__main__":
    main()
