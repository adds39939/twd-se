#!/usr/bin/env python3
"""
Extract and parse choice.prop from all TWD season archives.
Uses ttarch_decrypt.py for Blowfish v7 decryption and ECTT archive parsing.
"""

import sys
import os
import struct
import zlib
import io
import re

def load_key():
    key_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "key.txt")
    if not os.path.exists(key_path):
        raise FileNotFoundError(
            "Encryption key not found. Create tools/key.txt with the Blowfish key hex string. "
            "The key can be extracted from WDC.exe at offset 0xC3D7A0 (55 bytes)."
        )
    with open(key_path, "r") as f:
        return f.read().strip()


# Force UTF-8 stdout
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

# Import from ttarch_decrypt
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ttarch_decrypt import BlowfishV7, parse_ectt_archive


class SuppressOutput:
    """Suppress stdout output from called functions."""
    def __enter__(self):
        self._stdout = sys.stdout
        sys.stdout = io.TextIOWrapper(io.BytesIO(), encoding='utf-8')
        return self
    def __exit__(self, *args):
        sys.stdout.close()
        sys.stdout = self._stdout


# ---- 4ATT archive parser ----

def parse_4att(data):
    """Parse 4ATT inner archive format and return dict of filename -> bytes.

    Entry layout (28 bytes):
      +0:  u64 name_hash
      +8:  u32 file_offset_lo, +12: u32 file_offset_hi (always 0)
      +16: u32 file_size_lo, +20: u32 file_size_hi (always 0)
      +24: u32 name_byte_offset (shifted left by 16 bits)
    """
    if len(data) < 12:
        return {}

    magic = struct.unpack_from('<I', data, 0)[0]
    if magic != 0x54544134:  # '4ATT'
        pos = data.find(b'4ATT')
        if pos == -1:
            return {}
        data = data[pos:]

    names_size = struct.unpack_from('<I', data, 4)[0]
    file_count = struct.unpack_from('<I', data, 8)[0]

    entries = []
    for i in range(file_count):
        base = 12 + i * 28
        if base + 28 > len(data):
            break
        file_off = struct.unpack_from('<I', data, base + 8)[0]
        file_size = struct.unpack_from('<I', data, base + 16)[0]
        name_off = struct.unpack_from('<I', data, base + 24)[0] >> 16
        entries.append((name_off, file_off, file_size))

    name_table_start = 12 + file_count * 28
    file_data_start = name_table_start + names_size

    files = {}
    for name_off, file_off, file_size in entries:
        ne = data.find(b'\x00', name_table_start + name_off)
        if ne == -1:
            ne = name_table_start + names_size
        name = data[name_table_start + name_off:ne].decode('ascii', errors='replace')
        abs_off = file_data_start + file_off
        if abs_off + file_size <= len(data):
            files[name] = data[abs_off:abs_off + file_size]

    return files


# ---- MetaStream / PropertySet parser ----

TYPE_STRING = 0xCD9C6E605F5AF4B4
TYPE_BOOL = 0x9004C5587575D6C0
TYPE_INT32 = 0x7CACEEBCD26D075C
TYPE_PROPERTYSET = 0x00000000000002AB
TYPE_CONTAINER = 0xCD75DC4F6B9F15D2

KEY_ENGLISH_TEXT = 0x51B8A88CAD6DA2B1
KEY_GUID = 0xA955F65339C75510


def parse_metastream(data):
    """Parse MetaStream header (MSV6), return definition data."""
    if len(data) < 20:
        return None

    off = 0
    off += 4  # magic
    def_size = struct.unpack_from('<I', data, off)[0]; off += 4
    dbg_size = struct.unpack_from('<I', data, off)[0]; off += 4
    async_size = struct.unpack_from('<I', data, off)[0]; off += 4
    ver_count = struct.unpack_from('<I', data, off)[0]; off += 4

    compressed = (def_size & 0x80000000) != 0
    actual_def_size = def_size & 0x7FFFFFFF

    if ver_count > 100:
        return None

    off += ver_count * 12  # skip version entries

    if off + actual_def_size > len(data):
        return None

    def_data = data[off:off + actual_def_size]

    if compressed:
        if len(def_data) >= 4:
            ttcz_magic = struct.unpack_from('<I', def_data, 0)[0]
            if ttcz_magic == 0x5454435A:
                def_data = decompress_ttcz(def_data)
            else:
                for wbits in [15, -15]:
                    try:
                        def_data = zlib.decompress(def_data, wbits)
                        break
                    except:
                        pass

    return def_data


def decompress_ttcz(data):
    """Decompress TTCZ compressed data."""
    off = 4
    off += 4  # version
    off += 4  # decompressed_size
    off += 4  # compressed_size
    block_count = struct.unpack_from('<I', data, off)[0]; off += 4

    block_sizes = []
    for _ in range(block_count):
        block_sizes.append(struct.unpack_from('<I', data, off)[0]); off += 4

    result = bytearray()
    for bs in block_sizes:
        block = data[off:off + bs]; off += bs
        for wbits in [15, -15]:
            try:
                result.extend(zlib.decompress(block, wbits))
                break
            except:
                pass
        else:
            result.extend(block)
    return bytes(result)


class BinaryReader:
    def __init__(self, data):
        self.data = data
        self.pos = 0

    def u8(self):
        v = self.data[self.pos]; self.pos += 1; return v
    def u32(self):
        v = struct.unpack_from('<I', self.data, self.pos)[0]; self.pos += 4; return v
    def i32(self):
        v = struct.unpack_from('<i', self.data, self.pos)[0]; self.pos += 4; return v
    def u64(self):
        v = struct.unpack_from('<Q', self.data, self.pos)[0]; self.pos += 8; return v
    def read(self, n):
        v = self.data[self.pos:self.pos + n]; self.pos += n; return v
    def remaining(self):
        return len(self.data) - self.pos


def parse_property_value(reader, type_sym):
    """Parse a property value based on its type symbol."""
    if type_sym == TYPE_STRING:
        length = reader.u32()
        raw = reader.read(length)
        try:
            return raw.decode('utf-8')
        except:
            return raw.decode('latin-1', errors='replace')
    elif type_sym == TYPE_BOOL:
        return reader.u8() == 0x31
    elif type_sym == TYPE_INT32:
        return reader.i32()
    elif type_sym in (TYPE_PROPERTYSET, TYPE_CONTAINER):
        return parse_propertyset(reader)
    else:
        return None


def parse_propertyset(reader):
    """Parse a PropertySet recursively."""
    if reader.remaining() < 16:
        return None

    version = reader.u32()
    flags = reader.u32()
    data_size = reader.u32()
    data_start = reader.pos

    parent_count = reader.u32()
    for _ in range(parent_count):
        if reader.remaining() < 8:
            reader.pos = data_start + data_size
            return None
        reader.u64()

    if reader.remaining() < 4:
        reader.pos = data_start + data_size
        return None

    type_group_count = reader.u32()
    groups = []

    for _ in range(type_group_count):
        if reader.remaining() < 12:
            break
        type_sym = reader.u64()
        prop_count = reader.u32()

        props = []
        for _ in range(prop_count):
            if reader.remaining() < 8:
                break
            key_sym = reader.u64()
            value = parse_property_value(reader, type_sym)
            if value is None and type_sym not in (TYPE_STRING, TYPE_BOOL, TYPE_INT32,
                                                   TYPE_PROPERTYSET, TYPE_CONTAINER):
                reader.pos = data_start + data_size
                return {'version': version, 'flags': flags, 'groups': groups}
            props.append((key_sym, value))
        groups.append((type_sym, props))

    return {'version': version, 'flags': flags, 'groups': groups}


# ---- Markup stripping ----

def strip_markup(text):
    """Remove Telltale markup tags like ^font:...^, ^color:...^, ^glyphScale:...^, ^^."""
    if not text:
        return text
    # Remove ^tag:value^ patterns and standalone ^^
    cleaned = re.sub(r'\^[a-zA-Z]+:[^^ ]*\^', '', text)
    cleaned = re.sub(r'\^\^', '', cleaned)
    cleaned = re.sub(r'\^', '', cleaned)
    # Collapse whitespace
    cleaned = re.sub(r'\s+', ' ', cleaned).strip()
    return cleaned


# ---- Choice extraction ----

def extract_choices_structured(pset, depth=0):
    """Extract choice items recursively from a PropertySet tree."""
    results = []
    _walk_choices(pset, results, depth)
    return results


def _walk_choices(pset, results, depth):
    if not isinstance(pset, dict):
        return

    strings = {}
    ints = {}
    nested = []

    for type_sym, props in pset.get('groups', []):
        for key_sym, value in props:
            if type_sym == TYPE_STRING and isinstance(value, str):
                strings[key_sym] = value
            elif type_sym == TYPE_INT32 and isinstance(value, int):
                ints[key_sym] = value
            elif type_sym in (TYPE_PROPERTYSET, TYPE_CONTAINER) and isinstance(value, dict):
                nested.append((key_sym, value))

    eng_text = strings.get(KEY_ENGLISH_TEXT)
    guid = strings.get(KEY_GUID)

    if eng_text:
        expr_id = None
        episode = None
        for ks, vs in strings.items():
            if ks not in (KEY_ENGLISH_TEXT, KEY_GUID):
                if vs.isdigit() and len(vs) > 5:
                    expr_id = vs
        for ks, vs in ints.items():
            if 100 <= vs <= 999:
                episode = vs

        results.append({
            'text': eng_text,
            'text_clean': strip_markup(eng_text),
            'guid': guid,
            'depth': depth,
            'episode': episode,
            'expr_id': expr_id,
        })

    for key_sym, child in nested:
        _walk_choices(child, results, depth + 1)


def organize_choices(choices):
    """Organize flat choice list into question/option groups, deduplicating options."""
    if not choices:
        return []

    depths = sorted(set(c['depth'] for c in choices))
    if len(depths) < 2:
        return [{'text': c['text_clean'], 'guid': c.get('guid'), 'episode': c.get('episode'),
                 'expr_id': c.get('expr_id'), 'options': []} for c in choices]

    question_depth = depths[0]

    organized = []
    current_question = None

    for c in choices:
        text = c['text_clean'].strip()
        if not text:
            continue

        if c['depth'] <= question_depth + 1:
            if current_question:
                organized.append(current_question)
            current_question = {
                'text': text,
                'guid': c.get('guid'),
                'episode': c.get('episode'),
                'expr_id': c.get('expr_id'),
                'options': [],
            }
        else:
            if current_question is None:
                current_question = {
                    'text': '(Unknown question)',
                    'guid': None, 'episode': None, 'expr_id': None,
                    'options': [],
                }
            current_question['options'].append({
                'text': text,
                'guid': c.get('guid'),
                'expr_id': c.get('expr_id'),
            })
            if c.get('episode') and not current_question.get('episode'):
                current_question['episode'] = c['episode']

    if current_question:
        organized.append(current_question)

    # Merge question + theme pairs (S2 pattern: "Shot Kenny?" followed by "SURVIVALISM")
    merged = []
    i = 0
    while i < len(organized):
        q = organized[i]
        if i + 1 < len(organized):
            next_q = organized[i + 1]
            # If current has no options and next has options, and current looks like a question
            # while next looks like a theme/category, merge them
            if not q['options'] and next_q['options'] and len(next_q['text']) < 30 and next_q['text'].isupper():
                # Theme word follows question - merge
                next_q['text'] = f"{q['text']} [{next_q['text']}]"
                merged.append(next_q)
                i += 2
                continue
            elif q['options'] and not next_q['options'] and len(next_q['text']) < 30 and next_q['text'].isupper():
                # Theme word follows question with options
                q['text'] = f"{q['text']} [{next_q['text']}]"
                merged.append(q)
                i += 2
                continue
        merged.append(q)
        i += 1
    organized = merged

    # Deduplicate options: keep only unique text per question
    # Telltale stores same option in multiple markup variants (colored/uncolored/stats-only)
    for q in organized:
        seen_normalized = set()
        unique_opts = []
        for opt in q['options']:
            t = opt['text']
            # Normalize for dedup: strip "You and " prefix and percentages to compare core text
            # e.g. "You and 53% of players killed Conrad." and "53% of players killed Conrad."
            # are the same choice
            norm = re.sub(r'^You and ', '', t)
            norm = re.sub(r'^\d+\.?\d*%\s*of players\s*', '', norm)
            norm = re.sub(r'^\d+\.?\d*%\s*', '', norm)
            norm = norm.strip().lower()

            if norm in seen_normalized:
                continue
            # Skip options that start with "of players" (broken after markup stripping)
            if t.startswith('of players'):
                continue
            # Skip variants without "You and" if a "You and" version exists
            if not t.startswith('You') and not t.startswith('After'):
                has_you_version = any(
                    o['text'].startswith('You') and
                    re.sub(r'^You and \d+\.?\d*%\s*of players\s*', '', o['text']).strip().lower() == norm
                    for o in q['options']
                )
                if has_you_version:
                    continue
            # Skip entries with comma-different variants (e.g. "stopped AJ, and" vs "stopped AJ and")
            norm_no_comma = norm.replace(', ', ' ')
            if norm_no_comma in seen_normalized:
                continue
            seen_normalized.add(norm)
            seen_normalized.add(norm_no_comma)
            unique_opts.append(opt)
        q['options'] = unique_opts

    return organized


# ---- Main ----

ARCHIVES_DIR = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("TWD_ARCHIVES", "Archives")

ARCHIVES = [
    ("Season 1", os.path.join(ARCHIVES_DIR, "WDC_pc_ProjectSeason1_data.ttarch2")),
    ("Season 2", os.path.join(ARCHIVES_DIR, "WDC_pc_ProjectSeason2_data.ttarch2")),
    ("Season 3", os.path.join(ARCHIVES_DIR, "WDC_pc_ProjectSeason3_data.ttarch2")),
    ("Michonne", os.path.join(ARCHIVES_DIR, "WDC_pc_ProjectSeasonM_data.ttarch2")),
    ("Season 4", os.path.join(ARCHIVES_DIR, "WDC_pc_ProjectSeason4_data.ttarch2")),
]

KEY_HEX = load_key()

# Season 1 choice files to try (in order of preference)
S1_CHOICE_FILES = ['choice.prop', 'cmsWorldChoicesParsingData.prop', 'dialog_choices.prop']


def process_archive(name, path, cipher):
    """Process a single archive and return choice data."""
    print(f"\n{'='*60}")
    print(f"Processing: {name}")

    if not os.path.exists(path):
        print(f"  ERROR: File not found!")
        return None

    with SuppressOutput():
        data = parse_ectt_archive(path, cipher)

    if not data:
        print(f"  ERROR: Failed to decrypt archive")
        return None

    print(f"  Decrypted size: {len(data)} bytes")

    files = parse_4att(data)
    print(f"  Files found: {len(files)}")

    # Find choice.prop (or alternatives for S1)
    choice_data = None
    choice_name = None

    # First try exact match
    if 'choice.prop' in files:
        choice_data = files['choice.prop']
        choice_name = 'choice.prop'
    else:
        # Try alternatives
        for candidate in S1_CHOICE_FILES:
            if candidate in files:
                choice_data = files[candidate]
                choice_name = candidate
                break

    if not choice_data:
        # Search for any file with 'choice' and '.prop'
        for fn in files:
            if 'choice' in fn.lower() and fn.lower().endswith('.prop'):
                choice_data = files[fn]
                choice_name = fn
                break

    if not choice_data:
        print(f"  WARNING: No choice.prop found!")
        return None

    print(f"  Found: {choice_name} ({len(choice_data)} bytes)")

    # Check if it contains English text key
    eng_key = struct.pack('<Q', KEY_ENGLISH_TEXT)
    if eng_key not in choice_data:
        print(f"  NOTE: {choice_name} does not contain English text key")
        print(f"  This season may store choices in a different format.")
        return None

    # Parse MetaStream
    def_data = parse_metastream(choice_data)
    if not def_data:
        print(f"  ERROR: Failed to parse MetaStream")
        return None

    print(f"  MetaStream definition data: {len(def_data)} bytes")

    # Parse PropertySet
    reader = BinaryReader(def_data)
    try:
        pset = parse_propertyset(reader)
    except Exception as e:
        print(f"  ERROR parsing PropertySet: {e}")
        return None

    if not pset:
        print(f"  ERROR: PropertySet parse returned None")
        return None

    # Extract choices
    choices = extract_choices_structured(pset)
    print(f"  Choices extracted: {len(choices)}")

    return choices


def main():
    key_bytes = bytes.fromhex(KEY_HEX)

    print("Initializing Blowfish v7 cipher...")
    cipher = BlowfishV7(key_bytes)
    print("Cipher ready.\n")

    all_results = {}
    for name, path in ARCHIVES:
        choices = process_archive(name, path, cipher)
        all_results[name] = choices

    # Generate summary
    print("\n\n" + "=" * 70)
    print("GENERATING SUMMARY")
    print("=" * 70)

    summary_lines = []

    for name, path in ARCHIVES:
        choices = all_results.get(name)
        summary_lines.append(f"\n=== {name} ===")

        if not choices:
            summary_lines.append("  (No choice data extracted - season may use a different format)")
            continue

        organized = organize_choices(choices)

        for q in organized:
            ep_str = f"Episode {q['episode']}" if q.get('episode') else "Episode ?"
            expr_str = f" Expr={{{q['expr_id']}}}" if q.get('expr_id') else ""
            summary_lines.append(f'{ep_str}: "{q["text"]}"{expr_str}')
            if q.get('guid'):
                summary_lines.append(f"  GUID={{{q['guid']}}}")
            for opt in q.get('options', []):
                opt_expr = f" Expr={{{opt['expr_id']}}}" if opt.get('expr_id') else ""
                opt_guid = f" GUID={{{opt['guid']}}}" if opt.get('guid') else ""
                summary_lines.append(f'  Option: "{opt["text"]}"{opt_guid}{opt_expr}')

    summary = "\n".join(summary_lines)

    output_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "all_choices_summary.txt")
    with open(output_path, 'w', encoding='utf-8') as f:
        f.write(summary)

    print(f"\nSummary saved to: {output_path}")
    print("\n--- SUMMARY ---")
    print(summary)


if __name__ == '__main__':
    main()
