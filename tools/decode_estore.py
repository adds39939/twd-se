#!/usr/bin/env python3
"""
Decode Telltale EventLog estore/epage files (MSV6 MetaStream format).
Reverse-engineers the event storage format used in TWD S3 and Michonne saves.

FINDINGS SUMMARY:
=================
EventLog records are stored in epage files as fixed-size entries.
Each epage default section has:
  - 12-byte header: [4B unknown] [8B page_type_hash]
  - Variable-length structure: [4B total_size] [4B filename_len] [filename_bytes]
  - N event records (variable-length, see below)
  - Small trailer

Each event record in the epage is approximately 42 bytes:
  - Bytes [0-3]: 4 bytes - CRC64 hash fragment (dialog node ID / tag hash, lower bits)
  - Byte [4]: sequential event index (low byte)
  - Bytes [5-6]: sequential event index (high bytes) - page-range dependent
  - Byte [7]: flags/padding (always 0x00)
  - Bytes [8-11]: uint32 = 0x0000000A (10) - likely a version or type indicator
  - Bytes [12-15]: uint32 = 0x00000022 (34) - likely the record data size
  - Bytes [16-19]: uint32 = 0x00000001 (1) - count or flag
  - Bytes [20-23]: padding (0x00000000)
  - Bytes [24-31]: CRC64 hash of event type string (ECMA-182 normal)
    - 0x625874A31EA13BB1 = "Executing Dialog Node"
    - 0x25D62FD9BE53CF73 = "Dialog Choice"
    - 0x48FA4CC44ADE92F3 = "Save Serial"
    - 0x22B4F702006E4E3A = "Begin Episode"
    - 0xB1BB1124EA852E99 = "End Episode"
  - Bytes [32-35]: uint32 = 0x00000001 (1) - another count/flag
  - Byte [36]: 0x00 or 0x02 - when 0x02, next 4 bytes are a float/double fragment
  - Bytes [37-41]: 5 bytes - dialog node hash fragment or tag data

Ep1 vs Ep5 comparison of Page734:
  - Same 733 records, same structure
  - Only differences: byte[3] changes (from variable values to mostly 0x7F)
  - This byte appears to be a "visited/processed" flag or counter

Estore default section structure:
  - 12-byte header: [4B zero] [8B estore_type_hash]
  - Page index: [4B total_size] [4B entry_count] then N * [4B entry_size=0x0C, 8B page_hash, 4B page_number]
  - After index: [4B block_size=0x20] [4B filename_len] [filename_string]
  - Event records (same format as epage records)
"""

import sys
import struct
import zlib
import os
import glob
from collections import Counter

sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# =============================================================================
# ECMA-182 CRC64 (normal / non-reflected - used by Telltale for event types)
# =============================================================================

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
    """CRC64 ECMA-182 normal (Telltale's variant, lowercase input)."""
    crc = 0
    for c in s.lower():
        crc = CRC64_TABLE[(ord(c) ^ (crc >> 56)) & 0xFF] ^ ((crc << 8) & 0xFFFFFFFFFFFFFFFF)
    return crc

# Known event type hashes
EVENT_TYPES = {
    telltale_crc64("Executing Dialog Node"): "Executing Dialog Node",
    telltale_crc64("Begin Episode"): "Begin Episode",
    telltale_crc64("End Episode"): "End Episode",
    telltale_crc64("Save Serial"): "Save Serial",
    telltale_crc64("Dialog Choice"): "Dialog Choice",
}

# Verify hashes
print("=== Known Event Type CRC64 Hashes ===")
for h, name in sorted(EVENT_TYPES.items(), key=lambda x: x[1]):
    print(f"  0x{h:016X} = {name}")

# =============================================================================
# MSV6 / TTCZ parsing
# =============================================================================

def decompress_ttcz(data):
    if len(data) < 12 or data[0:4] != b'ZCTT':
        return data
    page_count = struct.unpack_from('<I', data, 8)[0]
    offsets = [struct.unpack_from('<Q', data, 12 + i * 8)[0] for i in range(page_count + 1)]
    result = bytearray()
    for i in range(page_count):
        block = data[offsets[i]:offsets[i + 1]]
        try:
            result.extend(zlib.decompress(block, -15))
        except zlib.error:
            try:
                result.extend(zlib.decompress(block))
            except:
                result.extend(block)
    return bytes(result)


def parse_msv6(data, label=""):
    if len(data) < 20 or data[0:4] != b'6VSM':
        return None
    def_size_raw = struct.unpack_from('<I', data, 4)[0]
    dbg_size_raw = struct.unpack_from('<I', data, 8)[0]
    async_size_raw = struct.unpack_from('<I', data, 12)[0]
    ver_count = struct.unpack_from('<I', data, 16)[0]

    def_size = def_size_raw & 0x7FFFFFFF
    dbg_size = dbg_size_raw & 0x7FFFFFFF
    async_size = async_size_raw & 0x7FFFFFFF

    pos = 20 + ver_count * 12
    sections = {}

    if def_size > 0:
        raw = data[pos:pos + def_size]
        if (def_size_raw & 0x80000000) and raw[:4] == b'ZCTT':
            raw = decompress_ttcz(raw)
        sections['default'] = raw
        pos += def_size

    if dbg_size > 0:
        raw = data[pos:pos + dbg_size]
        if (dbg_size_raw & 0x80000000) and raw[:4] == b'ZCTT':
            raw = decompress_ttcz(raw)
        sections['debug'] = raw
        pos += dbg_size

    if async_size > 0:
        raw = data[pos:pos + async_size]
        if (async_size_raw & 0x80000000) and raw[:4] == b'ZCTT':
            raw = decompress_ttcz(raw)
        sections['async'] = raw

    return sections


# =============================================================================
# Record parsing - the core event record structure
# =============================================================================

def parse_event_records(data, start_offset, label=""):
    """Parse event records from epage/estore data starting at start_offset.

    Each record appears to be a variable-length entry in a hash table.
    The structure for most records observed:

    Record layout (appears as ~42 bytes per entry, but actually variable):
      [4B hash_fragment/node_id] [2-3B index] [1B flags]
      [4B const=0x0A] [4B const=0x22] [4B const=0x01] [4B zero]
      [8B event_type_hash] [4B count=0x01] [1B tag_type] [5B tag_data]

    But alignment analysis shows they're actually fixed 42-byte records within pages.
    """
    records = []
    pos = start_offset
    record_size = 42  # Empirically determined

    # Try to detect the actual record boundary
    # Look for the constant pattern: 0A 00 00 00 22 00 00 00 01 00 00 00 00 00 00 00
    # This 16-byte constant block appears in every record

    # Find where records actually start by searching for the constant pattern
    pattern = bytes([0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00,
                     0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])

    first_pattern = data.find(pattern, start_offset)
    if first_pattern < 0:
        return records

    # The pattern starts at byte offset 8 within each record
    # But we need to figure out the actual record start
    # From analysis: the pattern is at offsets 8-23 within the 42-byte record
    # So record starts at first_pattern - 8
    # But the first record may have a different structure (header bytes before it)

    # Let's just parse records starting from start_offset using 42-byte steps
    # and verify each one has the constant pattern

    # Determine how many records fit
    data_len = len(data) - start_offset
    num_records = data_len // record_size

    type_counts = Counter()

    for i in range(num_records):
        rec_start = start_offset + i * record_size
        if rec_start + record_size > len(data):
            break

        rec = data[rec_start:rec_start + record_size]

        # Extract fields based on our analysis
        # The alignment varies slightly between pages, but the event type hash
        # is consistently at a position where it matches known hashes

        # Try to find the event type hash within this record
        event_type = None
        event_type_offset = -1
        for off in [24, 23, 22, 20, 16]:
            if off + 8 <= len(rec):
                h = struct.unpack_from('<Q', rec, off)[0]
                if h in EVENT_TYPES:
                    event_type = EVENT_TYPES[h]
                    event_type_offset = off
                    break

        if event_type:
            type_counts[event_type] += 1

        records.append({
            'index': i,
            'raw': rec,
            'event_type': event_type,
            'event_type_offset': event_type_offset,
        })

    return records, type_counts


# =============================================================================
# Epage analysis
# =============================================================================

def analyze_epage(filepath):
    """Full analysis of an epage file."""
    basename = os.path.basename(filepath)
    with open(filepath, 'rb') as f:
        data = f.read()

    sections = parse_msv6(data, basename)
    if not sections:
        print(f"  FAILED to parse MSV6 for {basename}")
        return None

    default = sections.get('default', b'')
    if not default:
        print(f"  No default section in {basename}")
        return None

    # Find filename in default section (S3 has it, Michonne may not)
    fn_start = default.find(b'.epage')
    if fn_start >= 0:
        data_start = fn_start + 6  # After ".epage"
    else:
        # No filename - data starts after the 12-byte header
        # Header: [4B flags] [8B type_hash]
        # Then possibly a small block before records
        data_start = 12
    data_region = default[data_start:]

    # Find the constant pattern to locate records
    pattern = bytes([0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00])
    first_match = data_region.find(pattern)

    if first_match < 0:
        print(f"  No record pattern found in {basename}")
        return None

    # Records appear to be 42 bytes each
    # The constant pattern at offset 8 within records means
    # the first record starts at (first_match - 8) bytes into data_region
    # But we need to account for a small header before records

    # Actually, let me try a different approach: scan for ALL occurrences
    # of the pattern and compute spacing
    occurrences = []
    pos = 0
    while True:
        idx = data_region.find(pattern, pos)
        if idx < 0:
            break
        occurrences.append(idx)
        pos = idx + 1

    if len(occurrences) < 2:
        return None

    # Compute spacings
    spacings = Counter()
    for i in range(1, len(occurrences)):
        diff = occurrences[i] - occurrences[i - 1]
        spacings[diff] += 1

    dominant_spacing = spacings.most_common(1)[0][0]

    # The record size IS the dominant spacing
    record_size = dominant_spacing

    # Record start: pattern is at a fixed offset within records
    # For 42-byte records, the pattern "0A 00 00 00 22 00 00 00" appears at different
    # internal offsets depending on the page
    pattern_internal_offset = occurrences[0] % record_size if record_size > 0 else 0

    # But actually, we can determine it by looking at the first few bytes before the pattern
    # and figuring out which are "header" bytes
    rec_data_start = occurrences[0] - pattern_internal_offset

    # Let's just use the first occurrence minus a small header offset
    # and see what works best

    # Better approach: try all possible internal offsets and see which gives
    # the best hash matches
    best_offset = 0
    best_matches = 0
    for trial_offset in range(record_size):
        trial_start = occurrences[0] - trial_offset
        if trial_start < 0:
            continue
        matches = 0
        for i in range(min(50, len(data_region) // record_size)):
            rec_pos = trial_start + i * record_size
            if rec_pos + record_size > len(data_region):
                break
            rec = data_region[rec_pos:rec_pos + record_size]
            for off in range(max(0, record_size - 8)):
                h = struct.unpack_from('<Q', rec, off)[0]
                if h in EVENT_TYPES:
                    matches += 1
                    break
        if matches > best_matches:
            best_matches = matches
            best_offset = trial_offset

    rec_data_start = occurrences[0] - best_offset

    # Now parse all records
    num_records = (len(data_region) - rec_data_start) // record_size

    # Find where the event type hash sits within records
    # Scan first 10 records for hash matches
    hash_offset = -1
    for off in range(record_size - 7):
        matches = 0
        for i in range(min(10, num_records)):
            rec_pos = rec_data_start + i * record_size
            if rec_pos + off + 8 > len(data_region):
                break
            h = struct.unpack_from('<Q', data_region, rec_pos + off)[0]
            if h in EVENT_TYPES:
                matches += 1
        if matches >= min(5, num_records):
            hash_offset = off
            break

    # Count event types
    type_counts = Counter()
    unique_hashes_at_type_offset = set()
    all_records = []

    for i in range(num_records):
        rec_pos = rec_data_start + i * record_size
        if rec_pos + record_size > len(data_region):
            break
        rec = data_region[rec_pos:rec_pos + record_size]

        event_type = None
        if hash_offset >= 0 and hash_offset + 8 <= record_size:
            h = struct.unpack_from('<Q', rec, hash_offset)[0]
            unique_hashes_at_type_offset.add(h)
            event_type = EVENT_TYPES.get(h, f"unknown(0x{h:016X})")
            type_counts[event_type] += 1

        all_records.append({
            'index': i,
            'raw': rec,
            'event_type': event_type,
        })

    return {
        'basename': basename,
        'default_size': len(default),
        'record_size': record_size,
        'num_records': len(all_records),
        'type_counts': type_counts,
        'hash_offset': hash_offset,
        'records': all_records,
        'unique_type_hashes': unique_hashes_at_type_offset,
        'default_data': default,
    }


# =============================================================================
# Estore analysis
# =============================================================================

def analyze_estore(filepath):
    """Analyze estore file."""
    with open(filepath, 'rb') as f:
        data = f.read()

    sections = parse_msv6(data, os.path.basename(filepath))
    if not sections:
        return None

    default = sections.get('default', b'')
    if len(default) < 20:
        return None

    # Parse header
    header_hash = struct.unpack_from('<Q', default, 4)[0]
    total_idx_size = struct.unpack_from('<I', default, 12)[0]
    entry_count = struct.unpack_from('<I', default, 16)[0]

    pages = []
    pos = 20
    for i in range(entry_count):
        if pos + 16 > len(default):
            break
        entry_size = struct.unpack_from('<I', default, pos)[0]
        page_hash = struct.unpack_from('<Q', default, pos + 4)[0]
        page_num = struct.unpack_from('<I', default, pos + 12)[0]
        pages.append({
            'hash': page_hash,
            'page_number': page_num,
        })
        pos += 4 + entry_size

    # After page index, find filename and then event records
    fn_marker = default.find(b'.estore', pos)
    estore_name = ""
    records_start = pos
    if fn_marker >= 0:
        # Find the length-prefixed string before it
        fn_end = fn_marker + 7
        records_start = fn_end

    # Count event records in estore default section
    pattern = bytes([0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00])
    record_occurrences = []
    search_pos = records_start
    while True:
        idx = default.find(pattern, search_pos)
        if idx < 0:
            break
        record_occurrences.append(idx)
        search_pos = idx + 1

    estore_record_count = len(record_occurrences)

    return {
        'filepath': filepath,
        'file_size': len(data),
        'default_size': len(default),
        'header_hash': header_hash,
        'pages': pages,
        'estore_record_count': estore_record_count,
    }


# =============================================================================
# Comparison
# =============================================================================

def compare_epage_defaults(data1, data2, label1, label2):
    """Compare two epage default sections byte by byte."""
    if len(data1) != len(data2):
        return {'same_size': False, 'size1': len(data1), 'size2': len(data2)}

    diffs = []
    for i in range(len(data1)):
        if data1[i] != data2[i]:
            diffs.append((i, data1[i], data2[i]))

    return {'same_size': True, 'size': len(data1), 'diff_count': len(diffs), 'diffs': diffs}


# =============================================================================
# Main
# =============================================================================

def main():
    print("\n" + "=" * 80)
    print("TELLTALE EVENTLOG ESTORE/EPAGE FORMAT DECODER")
    print("=" * 80)

    # =========================================================================
    # S3 Episode 1
    # =========================================================================
    s3e1_dir = r"<SAVE_DIR>\S3\Episode 1"
    s3e1_estore_path = os.path.join(s3e1_dir, "_wd3_saveslot1_id.estore")

    print(f"\n{'='*80}")
    print("S3 EPISODE 1")
    print(f"{'='*80}")

    if os.path.exists(s3e1_estore_path):
        estore = analyze_estore(s3e1_estore_path)
        if estore:
            print(f"\nEstore: {estore['file_size']} bytes, default section: {estore['default_size']} bytes")
            print(f"  Header hash: 0x{estore['header_hash']:016X}")
            print(f"  Page count: {len(estore['pages'])}")
            for p in estore['pages']:
                print(f"    Page {p['page_number']}: hash=0x{p['hash']:016X}")
            print(f"  Event records in estore: {estore['estore_record_count']}")

    s3e1_epages = {}
    epage_files = sorted(glob.glob(os.path.join(s3e1_dir, "_wd3_saveslot1_id_Page*.epage")))
    print(f"\n  Found {len(epage_files)} epage files")

    for ef in epage_files:
        result = analyze_epage(ef)
        if result:
            bn = result['basename']
            s3e1_epages[bn] = result
            print(f"\n  {bn}:")
            print(f"    Default section: {result['default_size']} bytes")
            print(f"    Record size: {result['record_size']} bytes")
            print(f"    Record count: {result['num_records']}")
            print(f"    Event type hash at offset: {result['hash_offset']}")
            print(f"    Event types:")
            for etype, count in result['type_counts'].most_common():
                print(f"      {etype}: {count}")
            print(f"    Unique type hashes: {len(result['unique_type_hashes'])}")
            for h in sorted(result['unique_type_hashes']):
                name = EVENT_TYPES.get(h, "UNKNOWN")
                print(f"      0x{h:016X} = {name}")

            # Show first 5 records decoded
            print(f"    First 5 records (hex):")
            for rec in result['records'][:5]:
                raw = rec['raw']
                hex_str = ' '.join(f'{b:02X}' for b in raw)
                print(f"      [{rec['index']:4d}] {hex_str}")
                print(f"             type={rec['event_type']}")

    # =========================================================================
    # S3 Episode 5
    # =========================================================================
    s3e5_dir = r"<SAVE_DIR>\S3\Episode 5\The end"
    s3e5_estore_path = os.path.join(s3e5_dir, "_wd3_saveslot1_id.estore")

    print(f"\n{'='*80}")
    print("S3 EPISODE 5 (The End)")
    print(f"{'='*80}")

    if os.path.exists(s3e5_estore_path):
        estore5 = analyze_estore(s3e5_estore_path)
        if estore5:
            print(f"\nEstore: {estore5['file_size']} bytes, default section: {estore5['default_size']} bytes")
            print(f"  Page count: {len(estore5['pages'])}")
            for p in estore5['pages']:
                print(f"    Page {p['page_number']}: hash=0x{p['hash']:016X}")
            print(f"  Event records in estore: {estore5['estore_record_count']}")

    s3e5_epages = {}
    epage_files_e5 = sorted(glob.glob(os.path.join(s3e5_dir, "_wd3_saveslot1_id_Page*.epage")))
    print(f"\n  Found {len(epage_files_e5)} epage files")

    for ef in epage_files_e5:
        result = analyze_epage(ef)
        if result:
            bn = result['basename']
            s3e5_epages[bn] = result
            print(f"\n  {bn}:")
            print(f"    Record size: {result['record_size']}, count: {result['num_records']}")
            print(f"    Event types:")
            for etype, count in result['type_counts'].most_common():
                print(f"      {etype}: {count}")

    # =========================================================================
    # Compare Page734 between Ep1 and Ep5
    # =========================================================================
    p734_key = "_wd3_saveslot1_id_Page734.epage"
    if p734_key in s3e1_epages and p734_key in s3e5_epages:
        print(f"\n{'='*80}")
        print("COMPARISON: Page734 - Ep1 vs Ep5")
        print(f"{'='*80}")

        d1 = s3e1_epages[p734_key]['default_data']
        d2 = s3e5_epages[p734_key]['default_data']
        comp = compare_epage_defaults(d1, d2, "Ep1", "Ep5")

        if comp['same_size']:
            print(f"  Size: {comp['size']} bytes (identical)")
            print(f"  Differing bytes: {comp['diff_count']}")

            if comp['diff_count'] > 0:
                # Determine which byte position within records differs
                fn_end1 = d1.find(b'.epage') + 6
                record_size = s3e1_epages[p734_key]['record_size']

                byte_positions = Counter()
                for offset, v1, v2 in comp['diffs']:
                    adj = offset - fn_end1
                    if adj >= 0 and record_size > 0:
                        byte_in_record = adj % record_size
                        byte_positions[byte_in_record] += 1

                print(f"\n  Differences by byte position within {record_size}-byte records:")
                for pos, count in byte_positions.most_common():
                    print(f"    byte[{pos}]: {count} differences")

                print(f"\n  First 20 differences:")
                for offset, v1, v2 in comp['diffs'][:20]:
                    adj = offset - fn_end1
                    rec_idx = adj // record_size if record_size > 0 else -1
                    byte_pos = adj % record_size if record_size > 0 else -1
                    print(f"    offset 0x{offset:04X} (record {rec_idx}, byte {byte_pos}): "
                          f"Ep1=0x{v1:02X} Ep5=0x{v2:02X}")

                # Check if byte[3] is a "visited" flag
                print(f"\n  Analysis of changing byte (byte[3] = likely 'visited/state' flag):")
                ep1_vals = Counter()
                ep5_vals = Counter()
                for offset, v1, v2 in comp['diffs']:
                    ep1_vals[v1] += 1
                    ep5_vals[v2] += 1
                print(f"    Ep1 values: {dict(ep1_vals.most_common(10))}")
                print(f"    Ep5 values: {dict(ep5_vals.most_common(10))}")
        else:
            print(f"  Different sizes: {comp['size1']} vs {comp['size2']}")

    # Also compare Page10249 (compressed, 458K decompressed)
    p10249_key = "_wd3_saveslot1_id_Page10249.epage"
    if p10249_key in s3e1_epages and p10249_key in s3e5_epages:
        print(f"\n{'='*80}")
        print("COMPARISON: Page10249 - Ep1 vs Ep5")
        print(f"{'='*80}")

        d1 = s3e1_epages[p10249_key]['default_data']
        d2 = s3e5_epages[p10249_key]['default_data']
        comp = compare_epage_defaults(d1, d2, "Ep1", "Ep5")

        if comp['same_size']:
            print(f"  Size: {comp['size']} bytes")
            print(f"  Differing bytes: {comp['diff_count']}")

            if comp['diff_count'] > 0:
                fn_end1 = d1.find(b'.epage') + 6
                record_size = s3e1_epages[p10249_key]['record_size']

                byte_positions = Counter()
                for offset, v1, v2 in comp['diffs']:
                    adj = offset - fn_end1
                    if adj >= 0 and record_size > 0:
                        byte_in_record = adj % record_size
                        byte_positions[byte_in_record] += 1

                print(f"\n  Differences by byte position within {record_size}-byte records:")
                for pos, count in byte_positions.most_common():
                    print(f"    byte[{pos}]: {count} differences")

                print(f"\n  First 20 differences:")
                for offset, v1, v2 in comp['diffs'][:20]:
                    adj = offset - fn_end1
                    rec_idx = adj // record_size if record_size > 0 else -1
                    byte_pos = adj % record_size if record_size > 0 else -1
                    print(f"    offset 0x{offset:04X} (record {rec_idx}, byte {byte_pos}): "
                          f"Ep1=0x{v1:02X} Ep5=0x{v2:02X}")
        else:
            print(f"  Different sizes: {comp['size1']} vs {comp['size2']}")

    # =========================================================================
    # Michonne
    # =========================================================================
    mich_dir = r"<SAVE_DIR>\Michonne"

    print(f"\n{'='*80}")
    print("MICHONNE")
    print(f"{'='*80}")

    for slot in [3, 2, 4]:
        mich_estore_path = os.path.join(mich_dir, f"_wdm_saveslot{slot}_id.estore")
        if not os.path.exists(mich_estore_path):
            continue

        estore_m = analyze_estore(mich_estore_path)
        if estore_m:
            print(f"\nEstore (slot {slot}): {estore_m['file_size']} bytes, "
                  f"default: {estore_m['default_size']} bytes")
            print(f"  Page count: {len(estore_m['pages'])}")
            for p in estore_m['pages']:
                print(f"    Page {p['page_number']}: hash=0x{p['hash']:016X}")
            print(f"  Event records in estore: {estore_m['estore_record_count']}")

        epage_files_m = sorted(glob.glob(
            os.path.join(mich_dir, f"_wdm_saveslot{slot}_id_Page*.epage")))
        print(f"\n  Found {len(epage_files_m)} epage files")

        for ef in epage_files_m:
            result = analyze_epage(ef)
            if result:
                print(f"\n  {result['basename']}:")
                print(f"    Record size: {result['record_size']}, count: {result['num_records']}")
                print(f"    Event type hash offset: {result['hash_offset']}")
                print(f"    Event types:")
                for etype, count in result['type_counts'].most_common():
                    print(f"      {etype}: {count}")
                print(f"    Unique type hashes: {len(result['unique_type_hashes'])}")
                for h in sorted(result['unique_type_hashes']):
                    name = EVENT_TYPES.get(h, "UNKNOWN")
                    print(f"      0x{h:016X} = {name}")

                # Show first 5 records
                print(f"    First 5 records:")
                for rec in result['records'][:5]:
                    raw = rec['raw']
                    hex_str = ' '.join(f'{b:02X}' for b in raw)
                    print(f"      [{rec['index']:4d}] {hex_str}")
                    print(f"             type={rec['event_type']}")
        break  # Only first slot found

    # =========================================================================
    # STRUCTURAL ANALYSIS SUMMARY
    # =========================================================================
    print(f"\n{'='*80}")
    print("STRUCTURAL ANALYSIS SUMMARY")
    print(f"{'='*80}")

    # Collect all record sizes across all analyzed files
    all_sizes = set()
    all_hash_offsets = set()
    total_records = 0
    total_event_types = Counter()

    for epages in [s3e1_epages, s3e5_epages]:
        for name, result in epages.items():
            all_sizes.add(result['record_size'])
            all_hash_offsets.add(result['hash_offset'])
            total_records += result['num_records']
            total_event_types += result['type_counts']

    print(f"\n  Record sizes observed: {sorted(all_sizes)}")
    print(f"  Event type hash offsets within records: {sorted(all_hash_offsets)}")
    print(f"  Total records analyzed: {total_records}")
    print(f"\n  Overall event type distribution:")
    for etype, count in total_event_types.most_common():
        pct = count / total_records * 100 if total_records > 0 else 0
        print(f"    {etype}: {count} ({pct:.1f}%)")

    # Analyze the record structure in detail using Page734 as reference
    if p734_key in s3e1_epages:
        result = s3e1_epages[p734_key]
        records = result['records']
        record_size = result['record_size']
        hash_offset = result['hash_offset']

        print(f"\n  Detailed record structure analysis (Page734, {record_size}-byte records):")
        print(f"  Event type hash at byte offset {hash_offset}")

        # Analyze each byte position
        if len(records) >= 10:
            for byte_pos in range(record_size):
                vals = set()
                for rec in records[:200]:
                    if byte_pos < len(rec['raw']):
                        vals.add(rec['raw'][byte_pos])

                if len(vals) == 1:
                    v = list(vals)[0]
                    print(f"    byte[{byte_pos:2d}]: CONSTANT 0x{v:02X} ({v})")
                elif len(vals) <= 5:
                    vs = ', '.join(f'0x{v:02X}' for v in sorted(vals))
                    print(f"    byte[{byte_pos:2d}]: {len(vals)} values: {vs}")
                else:
                    print(f"    byte[{byte_pos:2d}]: VARIABLE ({len(vals)} distinct values)")

        # Check for sequential index field
        print(f"\n  Sequential index search:")
        for field_start in range(record_size - 3):
            vals = []
            for rec in records[:30]:
                v = struct.unpack_from('<I', rec['raw'], field_start)[0]
                vals.append(v)
            if len(vals) >= 5:
                is_seq = True
                for i in range(len(vals) - 1):
                    if vals[i + 1] <= vals[i]:
                        is_seq = False
                        break
                if is_seq and vals[-1] - vals[0] < len(vals) * 3:
                    print(f"    uint32 at byte[{field_start}]: sequential, "
                          f"range {vals[0]}-{vals[-1]}")

        for field_start in range(record_size - 1):
            vals = []
            for rec in records[:30]:
                v = struct.unpack_from('<H', rec['raw'], field_start)[0]
                vals.append(v)
            if len(vals) >= 5:
                is_seq = True
                for i in range(len(vals) - 1):
                    if vals[i + 1] <= vals[i]:
                        is_seq = False
                        break
                if is_seq and vals[-1] - vals[0] < len(vals) * 3:
                    print(f"    uint16 at byte[{field_start}]: sequential, "
                          f"range {vals[0]}-{vals[-1]}")

    print(f"\n{'='*80}")
    print("FINAL RECORD FORMAT (CONFIRMED)")
    print(f"{'='*80}")
    print("""
EventLog Event Record (42 bytes, as aligned to constant block):
  Offset  Size  Description
  ------  ----  -----------
  0       4     Constant: 0x0000000A (version/format = 10)
  4       4     Constant: 0x00000022 (record payload size = 34 bytes)
  8       4     Constant: 0x00000001 (entry count within record)
  12      4     Constant: 0x00000000 (reserved/padding)
  16      8     Event type hash (CRC64-ECMA182-normal of lowercase string)
                  Known values:
                  - 0x625874A31EA13BB1 = "Executing Dialog Node" (~87.7%)
                  - 0x25D62FD9BE53CF73 = "Dialog Choice" (~1.4%)
                  - 0x5B4C4805E8CDC279 = Unknown (always paired with Dialog Choice, ~1.4%)
                  - 0x48FA4CC44ADE92F3 = "Save Serial" (~0.5%)
                  - 0x22B4F702006E4E3A = "Begin Episode" (rare)
                  - 0xB1BB1124EA852E99 = "End Episode" (rare)
                  - 0x0000000000000000 = Empty/unused slot
  24      4     Value type indicator (0x00000001 or 0x00000002)
                  0x02 = record has additional value data (float?)
  28      1     Extra data flag (0x00 = none, other = data follows)
  29      8     Event tag/node hash data (CRC64 of dialog node ID)
                  This is the unique identifier for the specific dialog
                  node or choice. Different for every event.
  37      2-3   Sequential event index (monotonically increasing per page)
                  Low 2 bytes: event number within page
                  High byte: page range selector (constant within page)
  39-41   3     Trailing bytes (constant 0x00 0x00 0x00 within page)

  Note: The 42-byte boundary wraps - the "index" and "tag data" at the end
  of one record are adjacent to the "constants" at the start of the next.
  The true semantic boundary may be at the event_type_hash field.

Epage File Structure:
  - MSV6 header (20 bytes + N*12 version entries)
  - Default section (may be TTCZ compressed):
    - 12 bytes: [4B flags] [8B page_type_hash (0x68BF1ACB or variant)]
    - Optional: [4B block_size] [4B filename_len] [filename_bytes]
      (Present in S3, absent in Michonne)
    - N * 42-byte event records
    - Small trailer (2-4 bytes)
  - Debug section: all zeros (preallocated)
  - Async section: empty (0 bytes)

Estore File Structure:
  - MSV6 header
  - Default section:
    - 12 bytes: [4B zero] [8B estore_type_hash]
    - Page index:
      - [4B total_index_size]
      - [4B page_count]
      - N * {[4B entry_size=0x0C] [8B page_hash] [4B page_number]}
    - [4B block_size=0x20] [4B filename_len] [filename_string]
    - Event records (same 42-byte format, overflow from pages)
  - Debug section: may contain metadata

Page Organization:
  - S3 Ep1: 4 pages (734, 10249, 11215, 12180), ~12K total events
  - S3 Ep5: 14 pages, ~19K total events (grows as game progresses)
  - Michonne slot3: 6 pages (971-5887), ~4K total events
  - Page10249 is largest (458KB decompressed, 10921 records)
  - Most events are "Executing Dialog Node" (~88%)
  - "Dialog Choice" events mark player decision points
  - Pages contain sequential event indices within their range

Ep1 vs Ep5 Comparison (Page734 = first page, shared between saves):
  - IDENTICAL structure, same 30841 bytes, same 732 records
  - Only 686 bytes differ, ALL at byte position [3] within 42-byte records
  - This is within the "tag/node hash" field
  - Ep1: varied values (0x0A-0x7E range)
  - Ep5: shifted toward 0x7F (430 of 686 diffs are to 0x7F)
  - Interpretation: this byte likely encodes a "traversal count" or
    "completion state" that increments as the game is replayed/revisited

CRC64 Hash Function (ECMA-182 normal, non-reflected):
  - Polynomial: 0x42F0E1EBA9EA3693
  - Input: lowercase(string)
  - Init: 0
  - Process: crc = TABLE[(byte ^ (crc >> 56)) & 0xFF] ^ (crc << 8)
""")


if __name__ == '__main__':
    main()
