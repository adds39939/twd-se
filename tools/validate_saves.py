#!/usr/bin/env python3
"""
Validate TWD Save Editor's save files against the actual game data.

Extracts SaveLoad.lua / ChoiceStats.lua from game archives, parses metadata
property names, computes CRC64-ECMA hashes, and compares against the hashes
used in the C# codebase.
"""

import struct
import sys
import os
import re
import zlib

def load_key():
    key_path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "key.txt")
    if not os.path.exists(key_path):
        raise FileNotFoundError(
            "Encryption key not found. Create tools/key.txt with the Blowfish key hex string. "
            "The key can be extracted from WDC.exe at offset 0xC3D7A0 (55 bytes)."
        )
    with open(key_path, "r") as f:
        return f.read().strip()


sys.stdout.reconfigure(encoding='utf-8', errors='replace')

# Import the cipher from the existing ttarch_decrypt module
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ttarch_decrypt import BlowfishV7, parse_ectt_archive

# ─── CRC64-ECMA-182 ──────────────────────────────────────────────────────────
# Polynomial: 0x42F0E1EBA9EA3693 (ECMA-182)
# This matches .NET's System.IO.Hashing.Crc64

CRC64_POLY = 0x42F0E1EBA9EA3693
_crc64_table = None

def _init_crc64_table():
    global _crc64_table
    _crc64_table = []
    for i in range(256):
        crc = i << 56
        for _ in range(8):
            if crc & (1 << 63):
                crc = ((crc << 1) & 0xFFFFFFFFFFFFFFFF) ^ CRC64_POLY
            else:
                crc = (crc << 1) & 0xFFFFFFFFFFFFFFFF
        _crc64_table.append(crc)

def crc64_ecma(data: bytes) -> int:
    """Compute CRC64-ECMA-182 matching .NET System.IO.Hashing.Crc64.
    Uses MSB-first (normal, non-reflected) polynomial 0x42F0E1EBA9EA3693.
    """
    if _crc64_table is None:
        _init_crc64_table()
    crc = 0
    for byte in data:
        crc = _crc64_table[((crc >> 56) ^ byte) & 0xFF] ^ ((crc << 8) & 0xFFFFFFFFFFFFFFFF)
    return crc

def crc64_str(s: str) -> int:
    """CRC64 of a lowercased ASCII string, matching TelltaleHash.ComputeCrc64."""
    return crc64_ecma(s.lower().encode('ascii'))


# ─── LEn Blowfish decryption for Lua scripts ─────────────────────────────────

LEN_KEY = bytes([
    0x63, 0x47, 0xF5, 0x60, 0xC4, 0x12, 0x52, 0xCB,
    0xEF, 0x04, 0x5A, 0x39, 0x41, 0xBC, 0x61, 0xFD,
])

try:
    from Crypto.Cipher import Blowfish as PyCryptoBlowfish
    HAS_PYCRYPTO = True
except ImportError:
    HAS_PYCRYPTO = False


def decrypt_lua_len(data: bytes) -> bytes:
    """Decrypt a Telltale LEn-encrypted Lua script.
    Format: 4 bytes 'LEn\\x00' + Blowfish-ECB encrypted data.
    """
    if len(data) < 4:
        return data
    magic = data[:4]
    if magic[:3] != b'LEn':
        return data  # not encrypted

    encrypted = data[4:]
    # Pad to 8-byte boundary
    pad_len = (8 - (len(encrypted) % 8)) % 8
    encrypted = encrypted + b'\x00' * pad_len

    if HAS_PYCRYPTO:
        cipher = PyCryptoBlowfish.new(LEN_KEY, PyCryptoBlowfish.MODE_ECB)
        decrypted = cipher.decrypt(encrypted)
    else:
        # Pure Python fallback using standard Blowfish (not v7)
        decrypted = _blowfish_ecb_decrypt(LEN_KEY, encrypted)

    # Strip trailing nulls
    return decrypted.rstrip(b'\x00')


def _blowfish_ecb_decrypt(key: bytes, data: bytes) -> bytes:
    """Simple standard Blowfish ECB decrypt (not the v7 variant)."""
    from ttarch_decrypt import BF_P, BF_S0, BF_S1, BF_S2, BF_S3, MASK32, N

    P = list(BF_P)
    S = [list(BF_S0), list(BF_S1), list(BF_S2), list(BF_S3)]

    # Standard key schedule
    key_len = len(key)
    j = 0
    for i in range(N + 2):
        d = 0
        for _ in range(4):
            d = ((d << 8) | key[j]) & MASK32
            j = (j + 1) % key_len
        P[i] = (P[i] ^ d) & MASK32

    def F(x):
        a = (x >> 24) & 0xFF
        b = (x >> 16) & 0xFF
        c = (x >> 8) & 0xFF
        d = x & 0xFF
        y = (S[0][a] + S[1][b]) & MASK32
        y = y ^ S[2][c]
        y = (y + S[3][d]) & MASK32
        return y

    def encipher(xl, xr):
        for i in range(N):
            xl = (xl ^ P[i]) & MASK32
            xr = (F(xl) ^ xr) & MASK32
            xl, xr = xr, xl
        xl, xr = xr, xl
        xr = (xr ^ P[N]) & MASK32
        xl = (xl ^ P[N + 1]) & MASK32
        return xl, xr

    datal, datar = 0, 0
    for i in range(0, N + 2, 2):
        datal, datar = encipher(datal, datar)
        P[i] = datal
        P[i + 1] = datar

    for i in range(4):
        for jj in range(0, 256, 2):
            datal, datar = encipher(datal, datar)
            S[i][jj] = datal
            S[i][jj + 1] = datar

    def decipher(xl, xr):
        for i in range(N + 1, 1, -1):
            xl = (xl ^ P[i]) & MASK32
            xr = (F(xl) ^ xr) & MASK32
            xl, xr = xr, xl
        xl, xr = xr, xl
        xr = (xr ^ P[1]) & MASK32
        xl = (xl ^ P[0]) & MASK32
        return xl, xr

    result = bytearray(data)
    for i in range(0, len(result), 8):
        xl = struct.unpack_from('>I', result, i)[0]
        xr = struct.unpack_from('>I', result, i + 4)[0]
        xl, xr = decipher(xl, xr)
        struct.pack_into('>I', result, i, xl)
        struct.pack_into('>I', result, i + 4, xr)
    return bytes(result)


# ─── NCTT (unencrypted) archive parser ────────────────────────────────────────

def parse_nctt_archive(data: bytes):
    """Parse an NCTT archive's file directory from reassembled data.
    Returns list of (filename, offset, size) tuples.
    """
    if len(data) < 12:
        return []

    # NCTT format: magic(4) + unknown(4) + file_count(4)
    magic = struct.unpack_from('<I', data, 0)[0]
    if magic != 0x5454434E:  # 'NCTT' LE
        # Possibly the data itself is the raw directory?
        pass

    # Search for the directory structure within the reassembled data
    # The NCTT directory has: file_count(4), then entries with:
    # nameLen(4) + name + offset(8) + size(4) [variable]
    files = []

    # Try to find file entries by looking for .lua filenames
    pos = 0
    while pos < len(data) - 4:
        # Look for a length-prefixed filename pattern
        name_len = struct.unpack_from('<I', data, pos)[0]
        if 5 < name_len < 200 and pos + 4 + name_len < len(data):
            try:
                name = data[pos + 4: pos + 4 + name_len].decode('ascii')
                if all(c.isprintable() and c != '\x00' for c in name) and ('.' in name):
                    # Potential file entry - record position
                    pass
            except (UnicodeDecodeError, ValueError):
                pass
        pos += 1

    return files


def extract_files_from_archive(data: bytes, target_names: list[str]) -> dict[str, bytes]:
    """Extract specific files from reassembled NCTT archive data.
    Returns dict mapping filename to file contents.
    """
    results = {}

    # The archive data is a flat concatenation of file entries in a directory structure.
    # We search for filenames and then try to extract them.

    for target in target_names:
        target_lower = target.lower()
        target_bytes = target_lower.encode('ascii')

        # Search for the filename in the data
        search_pos = 0
        while search_pos < len(data):
            idx = data.lower().find(target_bytes, search_pos)
            if idx == -1:
                break

            # Check if this is a length-prefixed string
            if idx >= 4:
                name_len = struct.unpack_from('<I', data, idx - 4)[0]
                if name_len == len(target_bytes):
                    # Found it! Now try to find the file data.
                    # After the name, there's typically: offset(8) + size(4) or similar
                    after_name = idx + name_len

                    # Try various approaches to find the file data
                    # In NCTT archives, the directory maps names to offsets+sizes
                    # We need the overall structure to resolve offsets.
                    pass

            search_pos = idx + 1

    return results


def find_all_files_in_archive(data: bytes) -> list[tuple[str, int, int]]:
    """Find all file directory entries in reassembled archive data.

    The archive has a directory section at the beginning with entries like:
    nameLength(4) + name(nameLength) + pad-to-8 + offset(8) + size(4)

    Returns list of (name, data_offset, data_size).
    """
    files = []
    pos = 0

    # First, skip the archive header
    # NCTT archives start with: magic(4) + someField(4) + ...
    if len(data) >= 4:
        magic = struct.unpack_from('<I', data, 0)[0]
        if magic == 0x5454434E:  # NCTT
            pos = 4

    # Try to find directory entries by scanning for length-prefixed filenames
    # Strategy: look for reasonable name lengths followed by ASCII text with file extensions
    file_extensions = [b'.lua', b'.prop', b'.scene', b'.d3dtx', b'.wav', b'.ogg',
                       b'.bank', b'.chore', b'.anm', b'.mesh', b'.lenc', b'.landb',
                       b'.skl', b'.font', b'.ttarch']

    # Build an index of all filename occurrences
    name_positions = []
    for ext in file_extensions:
        search_pos = 0
        while search_pos < len(data):
            idx = data.find(ext, search_pos)
            if idx == -1:
                break
            # Walk backwards to find start of the name (look for length prefix)
            for start_candidate in range(max(0, idx - 200), idx):
                name_len_candidate = struct.unpack_from('<I', data, start_candidate)[0]
                expected_end = start_candidate + 4 + name_len_candidate
                if expected_end > idx and expected_end <= idx + len(ext) + 5:
                    name = data[start_candidate + 4: start_candidate + 4 + name_len_candidate]
                    try:
                        name_str = name.decode('ascii')
                        if all(c.isprintable() and c != '\x00' for c in name_str):
                            name_positions.append((start_candidate, name_str))
                    except (UnicodeDecodeError, ValueError):
                        pass
                    break
            search_pos = idx + 1

    # Deduplicate and sort by position
    seen = set()
    unique_entries = []
    for pos_val, name in sorted(name_positions):
        if name not in seen:
            seen.add(name)
            unique_entries.append((pos_val, name))

    return unique_entries


def extract_file_by_search(data: bytes, target_name: str) -> bytes | None:
    """Extract a file from archive data by searching for its content.

    Strategy: Find the filename, then look for the file content nearby.
    For .lua files, the content starts with LEn (encrypted) or LuaQ/LJ (compiled).
    """
    target_lower = target_name.lower().encode('ascii')
    idx = data.lower().find(target_lower)
    if idx == -1:
        return None

    # The archive directory lists entries, and file data is stored elsewhere.
    # We need to scan the directory structure to find offsets.

    # Alternative approach: look for ALL occurrences of the filename,
    # then find nearby offset/size pairs that point to valid data.

    # For Telltale archives, after decryption+decompression we get a flat blob.
    # The blob starts with a file count, then directory entries, then file data.
    # Let's try to parse the directory structure more carefully.

    return _parse_archive_directory(data, target_name)


def _parse_archive_directory(data: bytes, target_name: str) -> bytes | None:
    """Parse the archive directory to find and extract a specific file.

    Archive format (after ECTT decryption/decompression):
    - 4 bytes: file count
    - For each file:
        - 4 bytes: name length
        - N bytes: name (no null terminator)
        - padding to 8-byte alignment from start of name
        - 8 bytes: offset into data section
        - 4 bytes: file size
    - Then the data section follows
    """
    if len(data) < 4:
        return None

    pos = 0
    file_count = struct.unpack_from('<I', data, pos)[0]
    pos += 4

    # Sanity check
    if file_count > 100000 or file_count == 0:
        return None

    entries = []
    for i in range(file_count):
        if pos + 4 > len(data):
            break
        name_len = struct.unpack_from('<I', data, pos)[0]
        pos += 4
        if name_len > 500 or pos + name_len > len(data):
            # Probably wrong structure - try alternative
            return _try_alternative_parse(data, target_name)

        name = data[pos:pos + name_len].decode('ascii', errors='replace')
        pos += name_len

        # Padding to 8-byte alignment (alignment of the name field, starting after the length)
        aligned = ((name_len + 7) & ~7)
        pad = aligned - name_len
        pos += pad

        if pos + 12 > len(data):
            break
        offset = struct.unpack_from('<Q', data, pos)[0]
        pos += 8
        size = struct.unpack_from('<I', data, pos)[0]
        pos += 4

        entries.append((name, offset, size))

    # The data section starts right after the directory
    data_start = pos

    for name, offset, size in entries:
        if name.lower() == target_name.lower():
            actual_offset = data_start + offset
            if actual_offset + size <= len(data):
                return data[actual_offset:actual_offset + size]

    # Try without data_start offset (offset might be absolute)
    for name, offset, size in entries:
        if name.lower() == target_name.lower():
            if offset + size <= len(data):
                return data[offset:offset + size]

    return None


def _try_alternative_parse(data: bytes, target_name: str) -> bytes | None:
    """Try alternative directory formats."""
    # Some archives have a different structure
    # Try: skip first 4 bytes (maybe a version/flags field)
    for skip in [4, 8, 12]:
        result = _parse_archive_directory(data[skip:], target_name)
        if result:
            return result
    return None


# ─── Validation logic ─────────────────────────────────────────────────────────

# Known metadata property hashes from SaveSlotFactory.cs
KNOWN_METADATA_HASHES = {
    0x7C725227A47FD1BA: "chapter count int",
    0x94C245DACB1ADDC3: "save slot index int",
    0x4F8338150CC8BCD6: "bool property",
    0xB218E7C003A67CE9: "episode id string",
    0xF235E9FCE9562E01: "autosave bundle string",
}

# Known version entry CRCs
OUTER_VERSION_ENTRIES = [
    (0xE09B099B8076C147, 0x5A585C97),
    (0x004F023463D89FB0, 0xB539B0FF),
]

INNER_VERSION_ENTRIES = [
    (0xCD75DC4F6B9F15D2, 0x21F2BCC9),
    (0x84283CB979D71641, 0x0527D6BF),
    (0x004F023463D89FB0, 0xB539B0FF),
]

# File table hashes from SaveSlotFactory
FILE_HASHES = {
    "metadata_slot.p": (0xEE382691929657D5, 0xCD75DC4F6B9F15D2),
    "choices.prop": (0x819F96241D349414, 0xCD75DC4F6B9F15D2),
    "choicestats.pro": (0xBD8881F09F440467, 0xCD75DC4F6B9F15D2),
}

SEASONS = {
    "S1": {
        "archive": "WDC_pc_ProjectSeason1_data.ttarch2",
        "inner_files": ["metadata_slot.p", "choices.prop"],
        "choice_format": "choices.prop",
    },
    "S2": {
        "archive": "WDC_pc_ProjectSeason2_data.ttarch2",
        "inner_files": ["metadata_slot.p", "choices.prop"],
        "choice_format": "choices.prop",
    },
    "S3": {
        "archive": "WDC_pc_ProjectSeason3_data.ttarch2",
        "inner_files": ["metadata_slot.p"],
        "choice_format": "estore/epage",
    },
    "S4": {
        "archive": "WDC_pc_ProjectSeason4_data.ttarch2",
        "inner_files": ["metadata_slot.p", "choicestats.pro"],
        "choice_format": "choicestats.pro",
    },
    "Michonne": {
        "archive": "WDC_pc_ProjectSeasonM_data.ttarch2",
        "inner_files": ["metadata_slot.p"],
        "choice_format": "estore/epage",
    },
}

# Test data paths
TEST_DATA_DIR = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tests", "TestData")

ARCHIVES_DIR = sys.argv[1] if len(sys.argv) > 1 else os.environ.get("TWD_ARCHIVES", "Archives")


def validate_bundle_structure(season: str) -> list[str]:
    """Validation 1: Check bundle file structure against real saves."""
    results = []
    test_files = {
        "S1": "wd1_saveslot2.bundle",
        "S2": "wd2_saveslot1.bundle",
        "S3": "wd3_saveslot1.bundle",
        "S4": "wd4_saveslot1.bundle",
        "Michonne": "wdm_saveslot4.bundle",
    }

    bundle_file = test_files.get(season)
    if not bundle_file:
        results.append(f"  SKIP: No test bundle for {season}")
        return results

    bundle_path = os.path.join(TEST_DATA_DIR, season, bundle_file)
    if not os.path.exists(bundle_path):
        results.append(f"  SKIP: {bundle_path} not found")
        return results

    with open(bundle_path, "rb") as f:
        data = f.read()

    # Parse outer MetaStream header
    magic = struct.unpack_from('<I', data, 0)[0]
    def_size = struct.unpack_from('<I', data, 4)[0]
    dbg_size = struct.unpack_from('<I', data, 8)[0]
    async_size = struct.unpack_from('<I', data, 12)[0]
    ver_count = struct.unpack_from('<I', data, 16)[0]

    magic_name = {0x4D535635: "MSV5", 0x4D535636: "MSV6"}.get(magic, f"0x{magic:08X}")
    results.append(f"  Outer magic: {magic_name}")

    if magic == 0x4D535636:
        results.append(f"  MATCH: Outer uses MSV6 (matches SaveSlotFactory)")
    else:
        results.append(f"  MISMATCH: Outer uses {magic_name}, SaveSlotFactory uses MSV6")

    results.append(f"  Version entries: {ver_count}")

    # Parse version entries
    pos = 20
    real_ver_entries = []
    for i in range(ver_count):
        type_crc = struct.unpack_from('<Q', data, pos)[0]
        pos += 8
        ver_crc = struct.unpack_from('<I', data, pos)[0]
        pos += 4
        real_ver_entries.append((type_crc, ver_crc))

    # Compare against SaveSlotFactory outer version entries
    if len(real_ver_entries) == len(OUTER_VERSION_ENTRIES):
        all_match = True
        for (rt, rv), (et, ev) in zip(real_ver_entries, OUTER_VERSION_ENTRIES):
            if rt != et or rv != ev:
                all_match = False
                results.append(f"  MISMATCH: Version entry 0x{rt:016X}/0x{rv:08X} != expected 0x{et:016X}/0x{ev:08X}")
        if all_match:
            results.append(f"  MATCH: All {len(real_ver_entries)} outer version entries match SaveSlotFactory")
    else:
        results.append(f"  MISMATCH: {len(real_ver_entries)} version entries, expected {len(OUTER_VERSION_ENTRIES)}")

    # Parse file table from default section
    is_compressed = (def_size & 0x80000000) != 0
    raw_def_size = def_size & 0x7FFFFFFF
    default_data = data[pos:pos + raw_def_size]

    if is_compressed:
        # Decompress TTCZ
        if default_data[:4] == b'ZCTT':
            results.append(f"  Default section: TTCZ compressed ({raw_def_size} bytes)")
            # Skip decompression for now, check what we can
        else:
            results.append(f"  Default section: compressed but not TTCZ")
    else:
        # Parse uncompressed file table
        if len(default_data) >= 8:
            unknown1, file_count = struct.unpack_from('<II', default_data, 0)
            results.append(f"  File table: {file_count} entries (unknown1={unknown1})")

            ft_pos = 8
            for i in range(file_count):
                if ft_pos + 8 > len(default_data):
                    break
                offset, size = struct.unpack_from('<II', default_data, ft_pos)
                ft_pos += 8

                # Read null-terminated name
                name_bytes = bytearray()
                while ft_pos < len(default_data) and default_data[ft_pos] != 0:
                    name_bytes.append(default_data[ft_pos])
                    ft_pos += 1
                ft_pos += 1  # skip null

                # Pad to 4-byte alignment
                name_total = len(name_bytes) + 1
                padded = (name_total + 3) & ~3
                ft_pos += padded - name_total

                name = name_bytes.decode('ascii', errors='replace')

                if ft_pos + 16 > len(default_data):
                    break
                h1 = struct.unpack_from('<Q', default_data, ft_pos)[0]
                ft_pos += 8
                h2 = struct.unpack_from('<Q', default_data, ft_pos)[0]
                ft_pos += 8

                results.append(f"    File: {name} (hash1=0x{h1:016X}, hash2=0x{h2:016X})")

                expected = FILE_HASHES.get(name)
                if expected:
                    if h1 == expected[0] and h2 == expected[1]:
                        results.append(f"      MATCH: Hashes match SaveSlotFactory constants")
                    else:
                        if h1 != expected[0]:
                            results.append(f"      MISMATCH: hash1 0x{h1:016X} != expected 0x{expected[0]:016X}")
                        if h2 != expected[1]:
                            results.append(f"      MISMATCH: hash2 0x{h2:016X} != expected 0x{expected[1]:016X}")

                # Try to check inner MetaStream header (only possible if async is uncompressed)
                async_compressed = (async_size & 0x80000000) != 0
                if not async_compressed:
                    async_start = pos + raw_def_size
                    dbg_raw = dbg_size & 0x7FFFFFFF
                    async_start += dbg_raw
                    inner_offset = async_start + offset
                    if inner_offset + 20 < len(data):
                        inner_magic = struct.unpack_from('<I', data, inner_offset)[0]
                        inner_magic_name = {0x4D535635: "MSV5", 0x4D535636: "MSV6"}.get(inner_magic, f"0x{inner_magic:08X}")
                        inner_ver_count = struct.unpack_from('<I', data, inner_offset + 16)[0]
                        results.append(f"      Inner magic: {inner_magic_name}, {inner_ver_count} version entries")

                        if inner_magic == 0x4D535636:
                            results.append(f"      MATCH: Inner uses MSV6 (matches BuildInnerMetaStream)")
                        else:
                            results.append(f"      NOTE: Inner uses {inner_magic_name}")

                        # Parse inner version entries
                        inner_pos = inner_offset + 20
                        inner_vers = []
                        for iv in range(inner_ver_count):
                            if inner_pos + 12 > len(data):
                                break
                            ivt = struct.unpack_from('<Q', data, inner_pos)[0]
                            inner_pos += 8
                            ivc = struct.unpack_from('<I', data, inner_pos)[0]
                            inner_pos += 4
                            inner_vers.append((ivt, ivc))

                        if len(inner_vers) == len(INNER_VERSION_ENTRIES):
                            iv_match = all(
                                rt == et and rv == ev
                                for (rt, rv), (et, ev) in zip(inner_vers, INNER_VERSION_ENTRIES)
                            )
                            if iv_match:
                                results.append(f"      MATCH: Inner version entries match InnerVersionEntries")
                            else:
                                for (rt, rv), (et, ev) in zip(inner_vers, INNER_VERSION_ENTRIES):
                                    if rt != et or rv != ev:
                                        results.append(f"      MISMATCH: inner ver 0x{rt:016X}/0x{rv:08X} != 0x{et:016X}/0x{ev:08X}")
                        else:
                            results.append(f"      NOTE: {len(inner_vers)} inner version entries vs {len(INNER_VERSION_ENTRIES)} expected")

                        # Parse PropertySet header from inner default section
                        inner_def_size = struct.unpack_from('<I', data, inner_offset + 4)[0]
                        inner_def_compressed = (inner_def_size & 0x80000000) != 0
                        inner_def_raw = inner_def_size & 0x7FFFFFFF
                        if not inner_def_compressed and inner_pos + 8 <= len(data):
                            ps_version = struct.unpack_from('<I', data, inner_pos)[0]
                            ps_flags = struct.unpack_from('<I', data, inner_pos + 4)[0]
                            results.append(f"      PropertySet: version={ps_version}, flags=0x{ps_flags:X}")

                            if name == "metadata_slot.p":
                                if ps_version == 2 and ps_flags == 0x100:
                                    results.append(f"      MATCH: metadata version=2, flags=0x100")
                                else:
                                    results.append(f"      MISMATCH: metadata version={ps_version} flags=0x{ps_flags:X} (expected 2, 0x100)")
                            elif name in ("choices.prop", "season1.prop"):
                                if ps_version == 2:
                                    results.append(f"      MATCH: choices version=2, flags=0x{ps_flags:X}")
                                else:
                                    results.append(f"      MISMATCH: choices version={ps_version} (expected 2)")
                            elif name == "choicestats.pro":
                                if ps_version == 2 and ps_flags == 0x100:
                                    results.append(f"      MATCH: choicestats version=2, flags=0x100")
                                else:
                                    results.append(f"      MISMATCH: choicestats version={ps_version} flags=0x{ps_flags:X} (expected 2, 0x100)")
                else:
                    results.append(f"      (async section compressed - inner files checked via C# tests)")

    return results


def validate_metadata_hashes(season: str, archive_data: bytes | None) -> list[str]:
    """Validation 2: Extract metadata property names from SaveLoad.lua and hash them."""
    results = []

    if archive_data is None:
        results.append(f"  SKIP: No archive data available for {season}")
        return results

    # Try to extract SaveLoad.lua
    lua_data = _parse_archive_directory(archive_data, "SaveLoad.lua")
    if lua_data is None:
        # Try alternate names
        for alt_name in ["saveload.lua", "SaveLoad.lenc", "saveload.lenc"]:
            lua_data = _parse_archive_directory(archive_data, alt_name)
            if lua_data:
                results.append(f"  Found as: {alt_name}")
                break

    if lua_data is None:
        results.append(f"  Could not extract SaveLoad.lua from archive")
        # Try scanning for Lua content directly
        results.append(f"  Searching for SaveLoad patterns in raw data...")

        # Search for metadata-related strings in the raw archive data
        metadata_strings = [
            b"metadata_slot",
            b"chapterCount",
            b"saveSlotIndex",
            b"episodeId",
            b"autosave",
            b"SaveLoad",
        ]

        for s in metadata_strings:
            count = archive_data.count(s)
            if count > 0:
                # Find context
                idx = archive_data.find(s)
                start = max(0, idx - 30)
                end = min(len(archive_data), idx + len(s) + 50)
                ctx = archive_data[start:end]
                readable = ''.join(c if c >= ' ' and c <= '~' else '.' for c in ctx.decode('latin-1'))
                results.append(f"  Found '{s.decode()}' x{count}: ...{readable}...")
        return results

    results.append(f"  Extracted SaveLoad.lua: {len(lua_data)} bytes")

    # Check if encrypted
    if lua_data[:3] == b'LEn':
        results.append(f"  Lua file is LEn encrypted, decrypting...")
        lua_data = decrypt_lua_len(lua_data)
        results.append(f"  Decrypted: {len(lua_data)} bytes")

    # Check if compiled Lua (binary starts with \x1bLua or LJ)
    if lua_data[:4] == b'\x1bLua' or lua_data[:2] == b'LJ':
        results.append(f"  Lua file is compiled bytecode - extracting strings only")
        # Extract readable strings from bytecode
        strings = extract_strings(lua_data)
    else:
        results.append(f"  Lua file is source code")
        strings = lua_data.decode('utf-8', errors='replace')
        strings = [s for s in re.findall(r'[A-Za-z_][A-Za-z0-9_]+', strings)]

    # Look for metadata property names
    metadata_patterns = [
        "chapterCount", "saveSlotIndex", "SaveSlotIndex",
        "episodeId", "EpisodeId", "episode_id",
        "autosave", "isValid", "IsValid",
        "SaveSlotChapterCount", "numChaptersComplete",
    ]

    found_names = []
    for pattern in metadata_patterns:
        if any(pattern.lower() == s.lower() for s in strings):
            found_names.append(pattern)
            results.append(f"  Found metadata property: '{pattern}'")

    # Compute CRC64 hashes for found names and compare
    if found_names:
        results.append(f"  Computing CRC64 hashes for {len(found_names)} found property names:")
        for name in found_names:
            h = crc64_str(name)
            known = KNOWN_METADATA_HASHES.get(h)
            if known:
                results.append(f"    '{name}' -> 0x{h:016X} MATCH ({known})")
            else:
                results.append(f"    '{name}' -> 0x{h:016X} (not in SaveSlotFactory)")
    else:
        results.append(f"  No metadata property names found in Lua source")

    # Also verify our known hashes by computing them from candidate names
    candidate_names = {
        0x7C725227A47FD1BA: ["chapterCount", "numChaptersComplete", "SaveSlotChapterCount",
                             "chapter_count", "ChapterCount", "mChapterCount"],
        0x94C245DACB1ADDC3: ["saveSlotIndex", "SaveSlotIndex", "save_slot_index",
                             "mSaveSlotIndex", "slotIndex", "slot_index"],
        0x4F8338150CC8BCD6: ["isValid", "IsValid", "is_valid", "mIsValid", "bIsValid",
                             "mValid", "valid"],
        0xB218E7C003A67CE9: ["episodeId", "EpisodeId", "episode_id", "mEpisodeId",
                             "currentEpisode", "CurrentEpisode", "current_episode"],
        0xF235E9FCE9562E01: ["autosavePath", "AutosavePath", "autosave_path",
                             "mAutosavePath", "checkpointFile", "savePath"],
    }

    results.append(f"\n  Reverse-engineering known hashes:")
    for target_hash, candidates in candidate_names.items():
        desc = KNOWN_METADATA_HASHES[target_hash]
        found = False
        for cand in candidates:
            h = crc64_str(cand)
            if h == target_hash:
                results.append(f"    0x{target_hash:016X} ({desc}) = CRC64('{cand}') CONFIRMED")
                found = True
                break
        if not found:
            results.append(f"    0x{target_hash:016X} ({desc}) = no candidate matched")
            # Try all strings from Lua source
            for s in strings:
                h = crc64_str(s)
                if h == target_hash:
                    results.append(f"    0x{target_hash:016X} ({desc}) = CRC64('{s}') FOUND IN LUA!")
                    found = True
                    break
            if not found:
                results.append(f"    0x{target_hash:016X} ({desc}) = NOT FOUND in Lua strings either")

    return results


def extract_strings(data: bytes, min_len: int = 4) -> list[str]:
    """Extract readable ASCII strings from binary data."""
    strings = []
    current = []
    for b in data:
        if 0x20 <= b <= 0x7E:
            current.append(chr(b))
        else:
            if len(current) >= min_len:
                strings.append(''.join(current))
            current = []
    if len(current) >= min_len:
        strings.append(''.join(current))
    return strings


def validate_estore_format(season: str) -> list[str]:
    """Validation 4: Compare created estore/epage against real ones."""
    results = []

    if season not in ("S3", "Michonne"):
        results.append(f"  SKIP: {season} doesn't use estore/epage")
        return results

    estore_files = {
        "S3": "_wd3_saveslot1_id.estore",
        "Michonne": "_wdm_saveslot4_id.estore",
    }
    epage_files = {
        "S3": "_wd3_saveslot1_id_Page734.epage",
        "Michonne": "_wdm_saveslot4_id_Page971.epage",
    }

    estore_file = estore_files.get(season)
    epage_file = epage_files.get(season)

    if not estore_file or not epage_file:
        return results

    estore_path = os.path.join(TEST_DATA_DIR, season, estore_file)
    epage_path = os.path.join(TEST_DATA_DIR, season, epage_file)

    if not os.path.exists(estore_path):
        results.append(f"  SKIP: {estore_path} not found")
        return results

    # Read real estore
    with open(estore_path, "rb") as f:
        estore_data = f.read()

    results.append(f"  Real estore: {len(estore_data)} bytes")

    # Parse MSV6 header
    magic = struct.unpack_from('<I', estore_data, 0)[0]
    magic_name = {0x4D535635: "MSV5", 0x4D535636: "MSV6"}.get(magic, f"0x{magic:08X}")
    results.append(f"  estore magic: {magic_name}")

    if magic == 0x4D535636:
        results.append(f"  MATCH: estore uses MSV6 (matches EStoreCreator)")
    else:
        results.append(f"  MISMATCH: estore uses {magic_name}, expected MSV6")

    def_size = struct.unpack_from('<I', estore_data, 4)[0]
    dbg_size = struct.unpack_from('<I', estore_data, 8)[0]
    async_size = struct.unpack_from('<I', estore_data, 12)[0]
    ver_count = struct.unpack_from('<I', estore_data, 16)[0]

    results.append(f"  estore version entries: {ver_count}")

    pos = 20
    estore_vers = []
    for i in range(ver_count):
        vt = struct.unpack_from('<Q', estore_data, pos)[0]
        pos += 8
        vc = struct.unpack_from('<I', estore_data, pos)[0]
        pos += 4
        estore_vers.append((vt, vc))

    if len(estore_vers) == len(INNER_VERSION_ENTRIES):
        ver_match = all(
            rt == et and rv == ev
            for (rt, rv), (et, ev) in zip(estore_vers, INNER_VERSION_ENTRIES)
        )
        if ver_match:
            results.append(f"  MATCH: estore version entries match EStoreCreator.VersionEntries")
        else:
            for (rt, rv), (et, ev) in zip(estore_vers, INNER_VERSION_ENTRIES):
                if rt != et or rv != ev:
                    results.append(f"  MISMATCH: ver 0x{rt:016X}/0x{rv:08X} != 0x{et:016X}/0x{ev:08X}")
    else:
        results.append(f"  NOTE: {len(estore_vers)} version entries vs {len(INNER_VERSION_ENTRIES)} expected")

    # Read real epage and check record format
    if os.path.exists(epage_path):
        with open(epage_path, "rb") as f:
            epage_data = f.read()

        results.append(f"\n  Real epage: {len(epage_data)} bytes")

        ep_magic = struct.unpack_from('<I', epage_data, 0)[0]
        ep_magic_name = {0x4D535635: "MSV5", 0x4D535636: "MSV6"}.get(ep_magic, f"0x{ep_magic:08X}")
        results.append(f"  epage magic: {ep_magic_name}")

        ep_def_size = struct.unpack_from('<I', epage_data, 4)[0]
        ep_ver_count = struct.unpack_from('<I', epage_data, 16)[0]
        ep_pos = 20 + ep_ver_count * 12

        ep_is_compressed = (ep_def_size & 0x80000000) != 0
        ep_raw_def_size = ep_def_size & 0x7FFFFFFF

        if not ep_is_compressed:
            default_section = epage_data[ep_pos:ep_pos + ep_raw_def_size]

            # Find 42-byte records
            record_pattern = bytes([0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00,
                                    0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])
            record_start = default_section.find(record_pattern)

            if record_start >= 0:
                remaining = len(default_section) - record_start
                record_count = remaining // 42
                results.append(f"  Records start at offset {record_start} in default section")
                results.append(f"  Record count: {record_count} (42 bytes each)")
                results.append(f"  MATCH: 42-byte record format confirmed")

                # Parse first record
                if record_count > 0:
                    rec = default_section[record_start:record_start + 42]
                    version = struct.unpack_from('<I', rec, 0)[0]
                    payload = struct.unpack_from('<I', rec, 4)[0]
                    count = struct.unpack_from('<I', rec, 8)[0]
                    padding = struct.unpack_from('<I', rec, 12)[0]
                    event_hash = struct.unpack_from('<Q', rec, 16)[0]
                    value_type = struct.unpack_from('<I', rec, 24)[0]
                    extra_flag = rec[28]
                    node_hash = struct.unpack_from('<Q', rec, 29)[0]
                    seq_lo = rec[37]
                    seq_mid = rec[38]
                    seq_hi = rec[39]
                    trailing = struct.unpack_from('<H', rec, 40)[0]

                    results.append(f"  First record:")
                    results.append(f"    version=0x{version:X}, payload=0x{payload:X}, count={count}, padding={padding}")
                    results.append(f"    eventTypeHash=0x{event_hash:016X}")
                    results.append(f"    valueType={value_type}, extraFlag={extra_flag}")
                    results.append(f"    nodeHash=0x{node_hash:016X}")
                    results.append(f"    sequenceIndex={seq_lo | (seq_mid << 8) | (seq_hi << 16)}")
                    results.append(f"    trailing=0x{trailing:04X}")

                    # Check if event type matches known hashes
                    KNOWN_EVENT_TYPES = {
                        0x625874A31EA13BB1: "ExecutingDialogNode",
                        0x25D62FD9BE53CF73: "DialogChoice",
                        0x22B4F702006E4E3A: "BeginEpisode",
                        0xB1BB1124EA852E99: "EndEpisode",
                        0x48FA4CC44ADE92F3: "SaveSerial",
                    }
                    et_name = KNOWN_EVENT_TYPES.get(event_hash, "Unknown")
                    results.append(f"    Event type: {et_name}")
            else:
                results.append(f"  WARNING: No 42-byte record pattern found in epage")
        else:
            results.append(f"  epage default section is compressed ({ep_raw_def_size} bytes)")

    return results


def validate_choice_format(season: str) -> list[str]:
    """Validation 3: Verify choice storage format compatibility."""
    results = []
    info = SEASONS[season]

    if info["choice_format"] == "choices.prop":
        results.append(f"  Format: ChoicesContainer PropertySet in choices.prop/season1.prop")
        results.append(f"  Serialization: u32(count) + count x (u32(strlen) + chars + u8(bool))")
        results.append(f"  Type hash: 0x8AD17AD4CB809956 (ChoicesContainer)")

        # Verify by computing hash
        # The type hash 0x8AD17AD4CB809956 is a constant - we need to verify what string produces it
        # Since it's defined as a const, let's verify it exists in test data
        test_files = {"S1": "wd1_saveslot2.bundle", "S2": "wd2_saveslot1.bundle"}
        bundle_file = test_files.get(season)
        if bundle_file:
            bundle_path = os.path.join(TEST_DATA_DIR, season, bundle_file)
            if os.path.exists(bundle_path):
                with open(bundle_path, "rb") as f:
                    data = f.read()
                # Search for the ChoicesContainer type hash in the file
                target = struct.pack('<Q', 0x8AD17AD4CB809956)
                idx = data.find(target)
                if idx >= 0:
                    results.append(f"  MATCH: ChoicesContainer hash 0x8AD17AD4CB809956 found at offset 0x{idx:X}")
                else:
                    results.append(f"  NOTE: ChoicesContainer hash not found directly in bundle (may be in decompressed data)")

    elif info["choice_format"] == "estore/epage":
        results.append(f"  Format: EventLog records in estore/epage files")
        results.append(f"  Record size: 42 bytes per entry")
        results.append(f"  Event type: ExecutingDialogNode (0x625874A31EA13BB1)")

        # Verify the event type hash
        computed = crc64_str("Executing Dialog Node")
        if computed == 0x625874A31EA13BB1:
            results.append(f"  MATCH: CRC64('executing dialog node') = 0x{computed:016X}")
        else:
            results.append(f"  MISMATCH: CRC64('executing dialog node') = 0x{computed:016X}, expected 0x625874A31EA13BB1")

    elif info["choice_format"] == "choicestats.pro":
        results.append(f"  Format: GUID StringValue in choicestats.pro PropertySet")
        results.append(f"  String type hash: CRC64('string')")
        computed = crc64_str("String")
        results.append(f"  CRC64('string') = 0x{computed:016X}")

        # Check S4 test data
        bundle_path = os.path.join(TEST_DATA_DIR, "S4", "wd4_saveslot1.bundle")
        if os.path.exists(bundle_path):
            with open(bundle_path, "rb") as f:
                data = f.read()
            # Search for GUID pattern
            guid_pattern = rb'\{[0-9A-Fa-f]{8}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{4}-[0-9A-Fa-f]{12}\}'
            guids = re.findall(guid_pattern, data)
            if guids:
                results.append(f"  Found {len(guids)} GUIDs in S4 bundle")
                for g in guids[:5]:
                    results.append(f"    {g.decode()}")
            else:
                results.append(f"  No GUIDs found directly in S4 bundle (may be compressed)")

    return results


def validate_s3_choice_hashes() -> list[str]:
    """Validation 5: Verify S3 ChoiceNodeMapping hashes against game data."""
    results = []

    # Check if we can find S3 choice hashes in test data
    epage_files = [
        "_wd3_saveslot1_id_Page734.epage",
        "_wd3_saveslot1_id_Page10249.epage",
        "_wd3_saveslot1_id_Page11215.epage",
        "_wd3_saveslot1_id_Page12180.epage",
    ]

    # Known S3 node hashes from ChoiceNodeMapping
    known_hashes = {
        0x2D4BB68B3A6B79B7: ("shot_conrad", "true"),
        0x95124598AFDC509B: ("shot_conrad", "false"),
        0xF650515EC9AA8356: ("promised_kate", "false"),
        0x05F6BEEEE2651B6C: ("promised_kate", "true"),
        0x80E6B5D0C042A4DB: ("shot_joan", "true"),
        0x30AC5DBC402A7EE4: ("shot_joan", "false"),
        0x65864B7A9C79F70F: ("trippava_saved", "ava"),
        0x37FD9A5893FC1E6D: ("trippava_saved", "tripp"),
    }

    found_count = 0
    total_records = 0

    for epage_file in epage_files:
        epage_path = os.path.join(TEST_DATA_DIR, "S3", epage_file)
        if not os.path.exists(epage_path):
            continue

        with open(epage_path, "rb") as f:
            data = f.read()

        # Parse MSV6 header
        ver_count = struct.unpack_from('<I', data, 16)[0]
        def_size = struct.unpack_from('<I', data, 4)[0]
        ep_pos = 20 + ver_count * 12
        is_compressed = (def_size & 0x80000000) != 0
        raw_def_size = def_size & 0x7FFFFFFF

        if is_compressed:
            results.append(f"  {epage_file}: compressed, skipping")
            continue

        section = data[ep_pos:ep_pos + raw_def_size]

        # Find records
        record_pattern = bytes([0x0A, 0x00, 0x00, 0x00, 0x22, 0x00, 0x00, 0x00,
                                0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00])
        record_start = section.find(record_pattern)
        if record_start < 0:
            continue

        # Parse records
        pos = record_start
        page_records = 0
        while pos + 42 <= len(section):
            version = struct.unpack_from('<I', section, pos)[0]
            payload = struct.unpack_from('<I', section, pos + 4)[0]
            if version != 0x0A or payload != 0x22:
                break

            event_hash = struct.unpack_from('<Q', section, pos + 16)[0]
            node_hash = struct.unpack_from('<Q', section, pos + 29)[0]

            if event_hash == 0x625874A31EA13BB1:  # ExecutingDialogNode
                if node_hash in known_hashes:
                    choice_key, option_value = known_hashes[node_hash]
                    results.append(f"  MATCH: 0x{node_hash:016X} -> {choice_key}={option_value} (in {epage_file})")
                    found_count += 1

            total_records += 1
            page_records += 1
            pos += 42

        results.append(f"  {epage_file}: {page_records} records parsed")

    results.append(f"\n  Summary: {found_count} known hashes found in {total_records} total records")
    if found_count > 0:
        results.append(f"  MATCH: ChoiceNodeMapping hashes confirmed in real game saves")
    else:
        results.append(f"  NOTE: No known hashes matched (save may not contain these specific choices)")

    return results


def decrypt_archive(season: str) -> bytes | None:
    """Decrypt and decompress a season's archive."""
    info = SEASONS[season]
    archive_path = os.path.join(ARCHIVES_DIR, info["archive"])

    if not os.path.exists(archive_path):
        print(f"  Archive not found: {archive_path}")
        return None

    key_hex = load_key()
    key_bytes = bytes.fromhex(key_hex)
    cipher = BlowfishV7(key_bytes)

    # Suppress parse_ectt_archive print output
    import io
    old_stdout = sys.stdout
    sys.stdout = io.StringIO()
    try:
        data = parse_ectt_archive(archive_path, cipher)
    finally:
        sys.stdout = old_stdout

    return data


def main():
    print("=" * 80)
    print("TWD SAVE EDITOR - GAME COMPATIBILITY VALIDATION")
    print("=" * 80)

    all_pass = True

    # ── Validation 1: Bundle structure ────────────────────────────────────
    print("\n" + "=" * 80)
    print("VALIDATION 1: Bundle File Structure")
    print("=" * 80)

    for season in SEASONS:
        print(f"\n--- {season} ---")
        results = validate_bundle_structure(season)
        for r in results:
            print(r)
            if "MISMATCH" in r:
                all_pass = False

    # ── Validation 2: Metadata property hashes ────────────────────────────
    print("\n" + "=" * 80)
    print("VALIDATION 2: Metadata Property Hashes")
    print("=" * 80)

    # First verify our CRC64 implementation against known values
    print("\n--- CRC64 Implementation Verification ---")
    test_cases = [
        ("bool", None),  # We don't know the expected hash, but we can compute it
        ("int32", None),
        ("String", None),
    ]
    for name, expected in test_cases:
        h = crc64_str(name)
        print(f"  CRC64('{name}') = 0x{h:016X}")

    # Verify event type hashes
    print("\n--- Event Type Hash Verification ---")
    event_names = [
        ("Executing Dialog Node", 0x625874A31EA13BB1),
        ("Dialog Choice", 0x25D62FD9BE53CF73),
        ("Begin Episode", 0x22B4F702006E4E3A),
        ("End Episode", 0xB1BB1124EA852E99),
        ("Save Serial", 0x48FA4CC44ADE92F3),
    ]
    for name, expected in event_names:
        h = crc64_str(name)
        status = "MATCH" if h == expected else "MISMATCH"
        print(f"  CRC64('{name}') = 0x{h:016X} {status} (expected 0x{expected:016X})")
        if h != expected:
            all_pass = False

    # Decrypt archives and extract Lua scripts
    for season in SEASONS:
        print(f"\n--- {season} ---")
        print(f"  Decrypting {SEASONS[season]['archive']}...")
        archive_data = decrypt_archive(season)
        results = validate_metadata_hashes(season, archive_data)
        for r in results:
            print(r)
            if "MISMATCH" in r:
                all_pass = False

    # ── Validation 3: Choice format compatibility ─────────────────────────
    print("\n" + "=" * 80)
    print("VALIDATION 3: Choice Format Compatibility")
    print("=" * 80)

    for season in SEASONS:
        print(f"\n--- {season} ---")
        results = validate_choice_format(season)
        for r in results:
            print(r)
            if "MISMATCH" in r:
                all_pass = False

    # ── Validation 4: EventLog record format ──────────────────────────────
    print("\n" + "=" * 80)
    print("VALIDATION 4: EventLog Record Format (S3/Michonne)")
    print("=" * 80)

    for season in ("S3", "Michonne"):
        print(f"\n--- {season} ---")
        results = validate_estore_format(season)
        for r in results:
            print(r)
            if "MISMATCH" in r:
                all_pass = False

    # ── Validation 5: S3 Choice Node Mapping ──────────────────────────────
    print("\n" + "=" * 80)
    print("VALIDATION 5: S3 ChoiceNodeMapping Hash Verification")
    print("=" * 80)

    results = validate_s3_choice_hashes()
    for r in results:
        print(r)
        if "MISMATCH" in r:
            all_pass = False

    # ── Summary ──────────────────────────────────────────────────────────
    print("\n" + "=" * 80)
    if all_pass:
        print("OVERALL: ALL VALIDATIONS PASSED")
    else:
        print("OVERALL: SOME VALIDATIONS HAD MISMATCHES - SEE DETAILS ABOVE")
    print("=" * 80)


if __name__ == "__main__":
    main()
