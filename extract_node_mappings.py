#!/usr/bin/env python3
"""
Extract dialog node CRC64 hash mappings from TWD choice.prop files.

For each choice in S3, Michonne, and S4:
- Extracts question text, option text, expression/node IDs
- Converts decimal expression IDs to hex CRC64 hashes
- Verifies hashes against actual S3 epage event records

The expression values like {3263903064056887735} are DECIMAL representations
of CRC64 hashes. Converting to hex gives the dialog node hash stored in
EventLog epage records at bytes 29-36 of each 42-byte record.
"""

import sys
import os
import io
import struct
import json
import re
import glob

# Force UTF-8 stdout
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

# Store reference to our stdout
_our_stdout = sys.stdout

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ttarch_decrypt import BlowfishV7, parse_ectt_archive
from extract_all_choices import (
    parse_4att, parse_metastream, BinaryReader, parse_propertyset,
    strip_markup
)


class SuppressOutput:
    """Suppress stdout output from called functions without closing our stdout."""
    def __enter__(self):
        self._original_stdout = sys.stdout
        sys.stdout = open(os.devnull, 'w', encoding='utf-8')
        return self
    def __exit__(self, *args):
        try:
            sys.stdout.close()
        except:
            pass
        sys.stdout = self._original_stdout

# ---- Constants ----

KEY_HEX = "REDACTED_KEY"

ARCHIVES_BASE = r"G:\Games\Steam\steamapps\common\The Walking Dead The Telltale Definitive Series\Archives"

ARCHIVES = {
    "Michonne": os.path.join(ARCHIVES_BASE, "WDC_pc_ProjectSeasonM_data.ttarch2"),
    "Season 4": os.path.join(ARCHIVES_BASE, "WDC_pc_ProjectSeason4_data.ttarch2"),
}

# Property key constants
KEY_ENGLISH_TEXT = 0x51B8A88CAD6DA2B1
KEY_EXPRESSION = 0x704F239B5CCB1A02
KEY_SECONDARY_EXPR = 0xD9960DF963F20638
KEY_GUID = 0xA955F65339C75510
KEY_QUESTION_TEXT = 0x662E2F86618F19DA  # Localized text container
KEY_OPTIONS_CONTAINER = 0x8B92202445FF971A  # Options container
KEY_TEXTURE = 0xB3DE310781EA1FCA
KEY_PERCENTAGE = 0x7C7B27DCD663E32E

# Type hashes
TYPE_STRING = 0xCD9C6E605F5AF4B4
TYPE_PROPERTYSET = 0x00000000000002AB
TYPE_CONTAINER = 0xCD75DC4F6B9F15D2

# Known event type hashes for epage verification
def _build_crc64_table_normal():
    poly = 0x42F0E1EBA9EA3693
    table = []
    for i in range(256):
        crc = i << 56
        for _ in range(8):
            if crc & (1 << 63):
                crc = ((crc << 1) & 0xFFFFFFFFFFFFFFFF) ^ poly
            else:
                crc = (crc << 1) & 0xFFFFFFFFFFFFFFFF
        table.append(crc)
    return table

CRC64_TABLE = _build_crc64_table_normal()

def telltale_crc64(s):
    crc = 0
    for c in s.lower():
        crc = CRC64_TABLE[(ord(c) ^ (crc >> 56)) & 0xFF] ^ ((crc << 8) & 0xFFFFFFFFFFFFFFFF)
    return crc

HASH_EXECUTING_DIALOG_NODE = telltale_crc64("Executing Dialog Node")
HASH_DIALOG_CHOICE = telltale_crc64("Dialog Choice")


# ---- Choice data extraction from parsed JSON ----

def extract_from_json(data):
    """Extract choice mappings from the already-parsed JSON structure.

    The JSON has structure:
    {
      "0xE4AACE2DB31067FF": "success",
      "0xD95633003C31F3AC": {
        "<choice_key>": {
          "0x662E2F86618F19DA": { ... localized question texts ... },
          "0x704F239B5CCB1A02": "{0}",           # question expression
          "0xD9960DF963F20638": "{}",             # question secondary expr
          "0x8B92202445FF971A": {                 # options container
            "<option_key>": {
              "0x704F239B5CCB1A02": "{3263903064056887735}",  # option expression
              "0xD9960DF963F20638": "{26390306405688773}",    # secondary expression
              "0x662E2F86618F19DA": { ... localized option texts ... },
              ...
            },
            ...
          }
        },
        ...
      }
    }
    """
    mappings = []

    # Find the main container (skip the "success" entry)
    main_container = None
    for key, val in data.items():
        if isinstance(val, dict) and len(val) > 2:
            main_container = val
            break

    if not main_container:
        return mappings

    for choice_key, choice_val in main_container.items():
        if not isinstance(choice_val, dict):
            continue

        # Get question text
        question_texts = choice_val.get("0x662E2F86618F19DA", {})
        if isinstance(question_texts, dict):
            question_en = question_texts.get("0x51B8A88CAD6DA2B1", "")
        else:
            question_en = ""

        question_expr = choice_val.get("0x704F239B5CCB1A02", "")
        question_sec_expr = choice_val.get("0xD9960DF963F20638", "")
        episode_code = choice_val.get("0x35DF01C0CB402E67", "")
        target_episode = choice_val.get("0xF355012A92A676CE", "")

        # Get options
        options_container = choice_val.get("0x8B92202445FF971A", {})

        choice_entry = {
            'question': question_en,
            'question_clean': strip_markup(question_en),
            'question_expression': question_expr,
            'question_secondary': question_sec_expr,
            'episode_code': episode_code,
            'target_episode': target_episode,
            'options': [],
        }

        if isinstance(options_container, dict):
            for opt_key, opt_val in options_container.items():
                if not isinstance(opt_val, dict):
                    continue

                opt_texts = opt_val.get("0x662E2F86618F19DA", {})
                if isinstance(opt_texts, dict):
                    opt_en = opt_texts.get("0x51B8A88CAD6DA2B1", "")
                else:
                    opt_en = ""

                opt_expr = opt_val.get("0x704F239B5CCB1A02", "")
                opt_sec_expr = opt_val.get("0xD9960DF963F20638", "")
                opt_guid = opt_val.get("0xA955F65339C75510", "")
                # Michonne: no expression key, GUID is the identifier
                # Wrap GUID in braces to make it parseable as expression if no expression exists
                if not opt_expr and opt_guid and not opt_guid.startswith('{'):
                    opt_expr = "{" + opt_guid + "}"
                opt_texture = opt_val.get("0xB3DE310781EA1FCA", "")
                opt_pct = opt_val.get("0x7C7B27DCD663E32E", "")

                choice_entry['options'].append({
                    'text': opt_en,
                    'text_clean': strip_markup(opt_en),
                    'expression': opt_expr,
                    'secondary_expression': opt_sec_expr,
                    'guid': opt_guid,
                    'texture': opt_texture,
                    'percentage': opt_pct,
                })

        mappings.append(choice_entry)

    return mappings


# ---- Binary prop extraction (for Michonne and S4) ----

def extract_from_binary_prop(prop_data, season_name):
    """Extract choice mappings from raw choice.prop binary data."""
    # Parse MetaStream
    def_data = parse_metastream(prop_data)
    if not def_data:
        print(f"  ERROR: Failed to parse MetaStream for {season_name}")
        return []

    print(f"  MetaStream definition data: {len(def_data)} bytes")

    # Parse PropertySet
    reader = BinaryReader(def_data)
    try:
        pset = parse_propertyset(reader)
    except Exception as e:
        print(f"  ERROR parsing PropertySet for {season_name}: {e}")
        return []

    if not pset:
        print(f"  ERROR: PropertySet parse returned None for {season_name}")
        return []

    # Convert the binary PropertySet to a JSON-like dict for uniform processing
    json_like = pset_to_dict(pset)

    # Save as JSON for debugging
    json_path = os.path.join(os.path.dirname(os.path.abspath(__file__)),
                             f"{season_name.lower().replace(' ', '_')}_choice_prop.json")
    try:
        with open(json_path, 'w', encoding='utf-8') as f:
            json.dump(json_like, f, indent=2, ensure_ascii=False)
        print(f"  Saved parsed JSON to: {json_path}")
    except Exception as e:
        print(f"  Warning: could not save JSON: {e}")

    return extract_from_json(json_like)


def pset_to_dict(pset):
    """Convert a parsed PropertySet into a JSON-friendly dict keyed by hex symbol."""
    if not isinstance(pset, dict) or 'groups' not in pset:
        return pset

    result = {}
    for type_sym, props in pset.get('groups', []):
        for key_sym, value in props:
            key_hex = f"0x{key_sym:016X}"
            if isinstance(value, dict) and 'groups' in value:
                result[key_hex] = pset_to_dict(value)
            else:
                result[key_hex] = value
    return result


# ---- Expression parsing ----

def parse_expression(expr_str):
    """Parse an expression string and classify it.

    Returns dict with:
      - type: 'decimal', 'guid', 'empty', 'zero', 'compound', 'unknown'
      - value: the raw value
      - hex_hash: hex CRC64 hash (for decimal type)
      - decimal: the decimal value (for decimal type)
    """
    if not expr_str or expr_str == '{}' or expr_str == '{0}':
        return {'type': 'empty' if expr_str in ('{}', '') else 'zero', 'value': expr_str}

    # Strip outer braces
    inner = expr_str.strip()
    if inner.startswith('{') and inner.endswith('}'):
        inner = inner[1:-1].strip()

    # Check for GUID pattern
    guid_pattern = re.compile(r'^[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}$')
    if guid_pattern.match(inner):
        return {'type': 'guid', 'value': expr_str, 'guid': inner}

    # Check for compound GUID expression (S4 uses "GUID | GUID")
    if '|' in inner:
        parts = [p.strip() for p in inner.split('|')]
        if all(guid_pattern.match(p.strip('{ }')) for p in parts):
            return {'type': 'compound_guid', 'value': expr_str, 'guids': [p.strip('{ }') for p in parts]}

    # Check for pure decimal number
    if inner.isdigit():
        decimal_val = int(inner)
        hex_hash = f"0x{decimal_val:016X}"
        return {
            'type': 'decimal',
            'value': expr_str,
            'decimal': decimal_val,
            'hex_hash': hex_hash,
        }

    # Check for compound expression with operators (e.g., "expr1 || expr2")
    if '||' in inner or '&&' in inner:
        return {'type': 'compound_logic', 'value': expr_str}

    return {'type': 'unknown', 'value': expr_str}


# ---- Epage verification ----

def parse_msv6(data):
    """Parse MSV6 file header."""
    import zlib
    if len(data) < 20 or data[0:4] != b'6VSM':
        return None
    def_size_raw = struct.unpack_from('<I', data, 4)[0]
    dbg_size_raw = struct.unpack_from('<I', data, 8)[0]
    async_size_raw = struct.unpack_from('<I', data, 12)[0]
    ver_count = struct.unpack_from('<I', data, 16)[0]

    def_size = def_size_raw & 0x7FFFFFFF
    pos = 20 + ver_count * 12
    sections = {}

    if def_size > 0:
        raw = data[pos:pos + def_size]
        if (def_size_raw & 0x80000000) and raw[:4] == b'ZCTT':
            raw = decompress_ttcz_epage(raw)
        sections['default'] = raw
    return sections


def decompress_ttcz_epage(data):
    """Decompress TTCZ format used in epage files."""
    import zlib
    if len(data) < 12 or data[0:4] != b'ZCTT':
        return data
    page_count = struct.unpack_from('<I', data, 8)[0]
    offsets = [struct.unpack_from('<Q', data, 12 + i * 8)[0] for i in range(page_count + 1)]
    result = bytearray()
    for i in range(page_count):
        block = data[offsets[i]:offsets[i + 1]]
        try:
            result.extend(zlib.decompress(block, -15))
        except:
            try:
                result.extend(zlib.decompress(block))
            except:
                result.extend(block)
    return bytes(result)


def extract_dialog_node_hashes_from_epage(filepath):
    """Extract all dialog node hashes from an epage file.

    Uses the 42-byte record format:
    - Bytes 16-23: event type hash (CRC64)
    - Bytes 29-36: dialog node hash (8 bytes, little-endian u64)

    Returns set of dialog node hashes (as integers) for "Executing Dialog Node" events.
    """
    with open(filepath, 'rb') as f:
        data = f.read()

    sections = parse_msv6(data)
    if not sections:
        return set()

    default = sections.get('default', b'')
    if not default:
        return set()

    # Find where records start (after any filename)
    fn_end = default.find(b'.epage')
    if fn_end >= 0:
        data_start = fn_end + 6
    else:
        data_start = 12

    data_region = default[data_start:]

    # Find the constant pattern to locate records
    pattern = bytes([0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00])
    occurrences = []
    pos = 0
    while True:
        idx = data_region.find(pattern, pos)
        if idx < 0:
            break
        occurrences.append(idx)
        pos = idx + 1

    if len(occurrences) < 2:
        return set()

    # Verify 42-byte spacing
    from collections import Counter
    spacings = Counter()
    for i in range(1, min(100, len(occurrences))):
        spacings[occurrences[i] - occurrences[i-1]] += 1

    record_size = spacings.most_common(1)[0][0]
    if record_size != 42:
        print(f"  WARNING: Record size {record_size} != 42 in {filepath}")

    # The constant pattern (0x0A...) starts at offset 8 within the canonical layout,
    # but in the 42-byte record frame it could be at different positions.
    # We need to find event_type_hash and node_hash positions.

    # From decode_estore.py analysis:
    # In the 42-byte record, the constant block 0A 00 00 00 22 00 00 00 appears
    # and event_type_hash is at byte offset 16 (relative to record start).
    # Dialog node hash is at byte offset 29.

    # The constant pattern is at offset 8 within the record.
    # So records start at: first_occurrence - 8
    # But we need to verify by finding the best alignment.

    # Try alignment: record_start = occurrence[0] - 8
    # If that doesn't give valid hashes, try other offsets.

    node_hashes = set()
    dialog_choice_hashes = set()

    # Try multiple alignments
    best_alignment = -1
    best_match_count = 0

    for try_off in range(record_size):
        trial_start = occurrences[0] - try_off
        if trial_start < 0:
            continue

        matches = 0
        for i in range(min(50, (len(data_region) - trial_start) // record_size)):
            rec_pos = trial_start + i * record_size
            if rec_pos + record_size > len(data_region):
                break
            # Try reading event type hash at various internal offsets
            for hash_off in [16, 24]:
                if rec_pos + hash_off + 8 <= len(data_region):
                    h = struct.unpack_from('<Q', data_region, rec_pos + hash_off)[0]
                    if h == HASH_EXECUTING_DIALOG_NODE or h == HASH_DIALOG_CHOICE:
                        matches += 1
                        break

        if matches > best_match_count:
            best_match_count = matches
            best_alignment = try_off

    if best_alignment < 0:
        return set()

    rec_start = occurrences[0] - best_alignment
    num_records = (len(data_region) - rec_start) // record_size

    # Now find exact event_type and node_hash offsets
    event_type_offset = -1
    for off in range(record_size - 7):
        matches = 0
        for i in range(min(50, num_records)):
            rec_pos = rec_start + i * record_size
            if rec_pos + off + 8 > len(data_region):
                break
            h = struct.unpack_from('<Q', data_region, rec_pos + off)[0]
            if h == HASH_EXECUTING_DIALOG_NODE or h == HASH_DIALOG_CHOICE:
                matches += 1
        if matches >= min(5, num_records // 2):
            event_type_offset = off
            break

    if event_type_offset < 0:
        return set()

    # Dialog node hash is 13 bytes after event_type_offset (offset 29 vs 16, diff = 13)
    node_hash_offset = event_type_offset + 13

    for i in range(num_records):
        rec_pos = rec_start + i * record_size
        if rec_pos + node_hash_offset + 8 > len(data_region):
            break

        event_type_hash = struct.unpack_from('<Q', data_region, rec_pos + event_type_offset)[0]

        if event_type_hash == HASH_EXECUTING_DIALOG_NODE:
            node_hash = struct.unpack_from('<Q', data_region, rec_pos + node_hash_offset)[0]
            node_hashes.add(node_hash)
        elif event_type_hash == HASH_DIALOG_CHOICE:
            node_hash = struct.unpack_from('<Q', data_region, rec_pos + node_hash_offset)[0]
            dialog_choice_hashes.add(node_hash)

    return node_hashes, dialog_choice_hashes


# ---- Main ----

def format_mapping(mapping, season_name, all_epage_hashes=None, all_choice_hashes=None):
    """Format a single choice mapping for output."""
    lines = []
    q = mapping['question_clean'] or mapping['question']
    ep = mapping['episode_code']
    tgt = mapping['target_episode']

    q_expr = parse_expression(mapping['question_expression'])
    q_sec = parse_expression(mapping['question_secondary'])

    lines.append(f"  Question: {q}")
    lines.append(f"    Episode: {ep} -> {tgt}")
    lines.append(f"    Question Expression: {mapping['question_expression']} ({q_expr['type']})")
    lines.append(f"    Question Secondary:  {mapping['question_secondary']} ({q_sec['type']})")

    for i, opt in enumerate(mapping['options']):
        opt_text = opt['text_clean'] or opt['text']
        opt_expr = parse_expression(opt['expression'])
        opt_sec = parse_expression(opt['secondary_expression'])

        lines.append(f"    Option {i+1}: {opt_text}")
        lines.append(f"      Expression: {opt['expression']}")

        if opt_expr['type'] == 'decimal':
            hex_hash = opt_expr['hex_hash']
            dec_val = opt_expr['decimal']
            lines.append(f"      -> CRC64 Hash: {hex_hash} (decimal: {dec_val})")

            # Verify against epage data
            if all_epage_hashes is not None:
                found = dec_val in all_epage_hashes
                lines.append(f"      -> In epage 'Executing Dialog Node': {'YES' if found else 'NO'}")
            if all_choice_hashes is not None:
                found = dec_val in all_choice_hashes
                if found:
                    lines.append(f"      -> In epage 'Dialog Choice': YES")
        elif opt_expr['type'] == 'guid':
            lines.append(f"      -> GUID: {opt_expr['guid']}")
        elif opt_expr['type'] == 'compound_guid':
            lines.append(f"      -> Compound GUIDs: {opt_expr.get('guids', [])}")

        lines.append(f"      Secondary Expr: {opt['secondary_expression']}")
        if opt_sec['type'] == 'decimal':
            lines.append(f"      -> Secondary CRC64: {opt_sec['hex_hash']}")

        if opt.get('guid'):
            lines.append(f"      GUID: {opt['guid']}")
        if opt.get('percentage'):
            lines.append(f"      Percentage: {opt['percentage']}%")

    return '\n'.join(lines)


def main():
    print("=" * 80)
    print("DIALOG NODE CRC64 HASH MAPPING EXTRACTOR")
    print("=" * 80)

    # =========================================================================
    # 1. Load S3 from pre-parsed JSON
    # =========================================================================
    print("\n" + "=" * 60)
    print("SEASON 3 (from s3_choice_prop.json)")
    print("=" * 60)

    s3_json_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "s3_choice_prop.json")
    with open(s3_json_path, 'r', encoding='utf-8') as f:
        s3_data = json.load(f)

    s3_mappings = extract_from_json(s3_data)
    print(f"  Extracted {len(s3_mappings)} choice entries from S3")

    # =========================================================================
    # 2. Load S3 epage files for verification (Episode 5 has all episodes)
    # =========================================================================
    print("\n" + "-" * 40)
    print("Loading S3 epage files for verification...")
    print("(Using Episode 5 save which contains events from all episodes)")

    all_epage_node_hashes = set()
    all_epage_choice_hashes = set()

    # Search across Episode 1 and Episode 5 saves for maximum coverage
    epage_dirs = [
        r"C:\Users\Adam\Downloads\twd-saves\S3\Episode 1",
        r"C:\Users\Adam\Downloads\twd-saves\S3\Episode 5\The end",
    ]
    epage_files = []
    for d in epage_dirs:
        epage_files.extend(sorted(glob.glob(os.path.join(d, "_wd3_saveslot1_id_Page*.epage"))))

    for ef in epage_files:
        result = extract_dialog_node_hashes_from_epage(ef)
        if result:
            node_hashes, choice_hashes = result
            print(f"  {os.path.basename(ef)}: {len(node_hashes)} node hashes, {len(choice_hashes)} choice hashes")
            all_epage_node_hashes.update(node_hashes)
            all_epage_choice_hashes.update(choice_hashes)

    print(f"  Total unique 'Executing Dialog Node' hashes: {len(all_epage_node_hashes)}")
    print(f"  Total unique 'Dialog Choice' hashes: {len(all_epage_choice_hashes)}")

    # =========================================================================
    # 3. Decrypt and parse Michonne and S4
    # =========================================================================
    print("\nInitializing cipher...")
    key_bytes = bytes.fromhex(KEY_HEX)
    cipher = BlowfishV7(key_bytes)
    print("Cipher ready.")

    season_mappings = {"Season 3": s3_mappings}

    for season_name, archive_path in ARCHIVES.items():
        print(f"\n{'=' * 60}")
        print(f"{season_name}")
        print(f"{'=' * 60}")

        if not os.path.exists(archive_path):
            print(f"  ERROR: Archive not found: {archive_path}")
            continue

        print(f"  Decrypting {archive_path}...")
        with SuppressOutput():
            data = parse_ectt_archive(archive_path, cipher)

        if not data:
            print(f"  ERROR: Failed to decrypt archive")
            continue

        print(f"  Decrypted size: {len(data)} bytes")

        files = parse_4att(data)
        print(f"  Files in archive: {len(files)}")

        # Find choice.prop
        choice_data = None
        for fn in files:
            if fn.lower() == 'choice.prop':
                choice_data = files[fn]
                print(f"  Found choice.prop: {len(choice_data)} bytes")
                break

        if not choice_data:
            # Search for any choice-related prop
            for fn in sorted(files.keys()):
                if 'choice' in fn.lower() and fn.lower().endswith('.prop'):
                    choice_data = files[fn]
                    print(f"  Found {fn}: {len(choice_data)} bytes")
                    break

        if not choice_data:
            print(f"  WARNING: No choice.prop found in {season_name}")
            # List .prop files for debugging
            prop_files = [fn for fn in files if fn.lower().endswith('.prop')]
            print(f"  Available .prop files: {prop_files[:20]}")
            continue

        mappings = extract_from_binary_prop(choice_data, season_name)
        season_mappings[season_name] = mappings
        print(f"  Extracted {len(mappings)} choice entries")

    # =========================================================================
    # 4. Output all mappings
    # =========================================================================
    print("\n\n" + "=" * 80)
    print("ALL CHOICE -> DIALOG NODE HASH MAPPINGS")
    print("=" * 80)

    output_lines = []

    for season_name in ["Season 3", "Michonne", "Season 4"]:
        mappings = season_mappings.get(season_name, [])
        if not mappings:
            continue

        output_lines.append(f"\n{'=' * 70}")
        output_lines.append(f"  {season_name}: {len(mappings)} choices")
        output_lines.append(f"{'=' * 70}")

        # Collect stats
        total_options = 0
        decimal_options = 0
        guid_options = 0
        empty_options = 0
        verified_options = 0

        for mapping in mappings:
            use_epage = (season_name == "Season 3")
            formatted = format_mapping(
                mapping, season_name,
                all_epage_node_hashes if use_epage else None,
                all_epage_choice_hashes if use_epage else None
            )
            output_lines.append(formatted)
            output_lines.append("")

            for opt in mapping['options']:
                total_options += 1
                expr = parse_expression(opt['expression'])
                if expr['type'] == 'decimal':
                    decimal_options += 1
                    if use_epage and expr['decimal'] in all_epage_node_hashes:
                        verified_options += 1
                elif expr['type'] == 'guid':
                    guid_options += 1
                elif expr['type'] in ('empty', 'zero'):
                    empty_options += 1

        output_lines.append(f"\n  --- {season_name} Statistics ---")
        output_lines.append(f"  Total options: {total_options}")
        output_lines.append(f"  Decimal (CRC64 hash) options: {decimal_options}")
        output_lines.append(f"  GUID options: {guid_options}")
        output_lines.append(f"  Empty/zero options: {empty_options}")
        if season_name == "Season 3":
            output_lines.append(f"  Verified in epage files: {verified_options}/{decimal_options}")

    # =========================================================================
    # 5. Compact mapping table (for programmatic use)
    # =========================================================================
    output_lines.append(f"\n\n{'=' * 80}")
    output_lines.append("COMPACT HASH MAPPING TABLE")
    output_lines.append("=" * 80)
    output_lines.append("Format: CRC64_Hex | Decimal | Question | Option")
    output_lines.append("-" * 80)

    for season_name in ["Season 3", "Michonne", "Season 4"]:
        mappings = season_mappings.get(season_name, [])
        if not mappings:
            continue

        output_lines.append(f"\n--- {season_name} ---")

        for mapping in mappings:
            q = mapping['question_clean'] or "(no question)"
            # Truncate long questions
            if len(q) > 60:
                q = q[:57] + "..."

            for opt in mapping['options']:
                expr = parse_expression(opt['expression'])
                opt_text = opt['text_clean'] or "(no text)"
                # Remove "You and X% of players" prefix for cleaner display
                opt_text = re.sub(r'^You and \d+\.?\d*%\s+of players\s+', '', opt_text)
                if len(opt_text) > 50:
                    opt_text = opt_text[:47] + "..."

                if expr['type'] == 'decimal':
                    hex_h = expr['hex_hash']
                    dec_v = expr['decimal']
                    in_epage = ""
                    if season_name == "Season 3":
                        in_epage = " [VERIFIED in save]" if dec_v in all_epage_node_hashes else " [not in save - different choice or later episode]"
                    output_lines.append(f"  {hex_h} | {dec_v} | {q} | {opt_text}{in_epage}")
                elif expr['type'] == 'guid':
                    output_lines.append(f"  GUID:{expr['guid']} | - | {q} | {opt_text}")

    result_text = '\n'.join(output_lines)
    print(result_text)

    # Save to file
    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "node_hash_mappings.txt")
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(result_text)
    print(f"\n\nMappings saved to: {output_path}")

    # Also save as JSON for programmatic use
    json_output = {}
    for season_name in ["Season 3", "Michonne", "Season 4"]:
        mappings = season_mappings.get(season_name, [])
        if not mappings:
            continue

        season_entries = []
        for mapping in mappings:
            entry = {
                'question': mapping['question_clean'] or mapping['question'],
                'episode_code': mapping['episode_code'],
                'target_episode': mapping['target_episode'],
                'options': [],
            }
            for opt in mapping['options']:
                expr = parse_expression(opt['expression'])
                sec_expr = parse_expression(opt['secondary_expression'])
                opt_entry = {
                    'text': opt['text_clean'] or opt['text'],
                    'expression_raw': opt['expression'],
                    'expression_type': expr['type'],
                    'secondary_raw': opt['secondary_expression'],
                    'guid': opt.get('guid', ''),
                }
                if expr['type'] == 'decimal':
                    opt_entry['crc64_hex'] = expr['hex_hash']
                    opt_entry['crc64_decimal'] = expr['decimal']
                    if season_name == "Season 3":
                        opt_entry['verified_in_epage'] = expr['decimal'] in all_epage_node_hashes
                if sec_expr['type'] == 'decimal':
                    opt_entry['secondary_crc64_hex'] = sec_expr['hex_hash']
                    opt_entry['secondary_crc64_decimal'] = sec_expr['decimal']
                entry['options'].append(opt_entry)
            season_entries.append(entry)
        json_output[season_name] = season_entries

    json_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "node_hash_mappings.json")
    with open(json_path, 'w', encoding='utf-8') as f:
        json.dump(json_output, f, indent=2, ensure_ascii=False)
    print(f"JSON mappings saved to: {json_path}")


if __name__ == '__main__':
    main()
