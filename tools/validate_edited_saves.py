#!/usr/bin/env python3
"""Validate edited save files against game expectations."""
import sys, os, io, re, struct, zlib, json

os.environ['PYTHONIOENCODING'] = 'utf-8'
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ttarch_decrypt import BlowfishV7
from extract_all_choices import parse_4att, load_key, parse_ectt_archive

ARCHIVES_DIR = r'G:\Games\Steam\steamapps\common\The Walking Dead The Telltale Definitive Series\Archives'
SAVE_DIR = r'C:\Users\Adam\Documents\Telltale Games\The Walking Dead Definitive'
BACKUP_DIR = r'C:\Users\Adam\Downloads\backup'

key_bytes = bytes.fromhex(load_key())

NAMES = {
    0xB218E7C003A67CE9: 'Episode in Progress',
    0x7E7BE4FD8F464350: 'Saved Game Episode',
    0x6047826CD4EDC6B4: 'Checkpoint Dialog',
    0x8B8C42FEDDCE350C: 'Checkpoint Dialog Node',
    0x98A7E965982E6E98: 'Saved Game Chapter ID',
    0xF235E9FCE9562E01: 'Latest Save',
    0x7C725227A47FD1BA: 'Latest Serial',
    0x94C245DACB1ADDC3: 'progress',
    0x5C36A605E4E7890F: 'Saved Game Date',
    0x037B536D294D9D9A: 'Saved Game Serial',
}


def parse_bundle_full(path):
    """Parse a bundle and return complete structural info."""
    data = open(path, 'rb').read()
    def_size = struct.unpack_from('<I', data, 4)[0] & 0x7FFFFFFF
    dbg_size_raw = struct.unpack_from('<I', data, 8)[0]
    dbg_size = dbg_size_raw & 0x7FFFFFFF
    async_field = struct.unpack_from('<I', data, 12)[0]
    async_compressed = (async_field & 0x80000000) != 0
    async_raw = async_field & 0x7FFFFFFF
    ver_count = struct.unpack_from('<I', data, 16)[0]
    header_end = 20 + ver_count * 12
    async_start = header_end + def_size + dbg_size

    # Outer debug section
    outer_dbg = data[header_end + def_size:header_end + def_size + dbg_size]

    # Decompress async
    if async_compressed:
        raw = data[async_start:async_start + async_raw]
        if struct.unpack_from('<I', raw, 0)[0] == 0x5454435A:
            p2 = 4
            w = struct.unpack_from('<I', raw, p2)[0]; p2 += 4
            pages = struct.unpack_from('<I', raw, p2)[0]; p2 += 4
            offsets = [struct.unpack_from('<Q', raw, p2 + j * 8)[0] for j in range(pages + 1)]
            p2 += (pages + 1) * 8
            result = bytearray()
            for j in range(pages):
                result.extend(zlib.decompress(raw[int(offsets[j]):int(offsets[j + 1])], -15))
            async_data = bytes(result)
        else:
            async_data = zlib.decompress(raw)
    else:
        async_data = data[async_start:async_start + async_raw]

    # Parse file table
    ft = data[header_end:header_end + def_size]
    file_count = struct.unpack_from('<I', ft, 4)[0]
    p = 8
    entries = []
    for i in range(min(file_count, 500)):
        off = struct.unpack_from('<I', ft, p)[0]; p += 4
        sz = struct.unpack_from('<I', ft, p)[0]; p += 4
        ns = p
        while p < len(ft) and ft[p] != 0: p += 1
        nb = ft[ns:p]
        readable = all(0x20 <= b < 0x7F for b in nb) and len(nb) > 0
        name = nb.decode('ascii') if readable else f'_hash_{i}'
        p += 1; nl = len(nb) + 1; padded = (nl + 3) & ~3; p += padded - nl + 16
        entries.append((name, off, sz))

    info = {
        'size': len(data),
        'outer_dbg_size': dbg_size,
        'outer_dbg_hex': outer_dbg.hex() if dbg_size > 0 else '(none)',
        'async_compressed': async_compressed,
        'async_decompressed_size': len(async_data),
        'file_count': file_count,
        'files': [(n, o, s) for n, o, s in entries[:5]],
    }

    # Parse each named inner file
    for name, off, sz in entries:
        if off + sz > len(async_data):
            continue
        inner = async_data[off:off + sz]
        if len(inner) < 20:
            continue

        i_magic = struct.unpack_from('<I', inner, 0)[0]
        if i_magic not in (0x4D535636, 0x4D535635):
            continue

        i_def = struct.unpack_from('<I', inner, 4)[0] & 0x7FFFFFFF
        i_dbg = struct.unpack_from('<I', inner, 8)[0] & 0x7FFFFFFF
        i_async = struct.unpack_from('<I', inner, 12)[0] & 0x7FFFFFFF
        i_ver = struct.unpack_from('<I', inner, 16)[0]

        file_info = {
            'size': sz,
            'inner_def': i_def,
            'inner_dbg': i_dbg,
            'inner_async': i_async,
            'inner_versions': i_ver,
        }

        # Dump debug section bytes
        dbg_start = 20 + i_ver * 12 + i_def
        if i_dbg > 0 and dbg_start + i_dbg <= len(inner):
            file_info['inner_dbg_hex'] = inner[dbg_start:dbg_start + i_dbg].hex()

        # Parse PropertySet if this is metadata
        if 'metadata' in name:
            ip = 20 + i_ver * 12
            if ip + 12 <= len(inner):
                ps_ver = struct.unpack_from('<I', inner, ip)[0]; ip += 4
                ps_flags = struct.unpack_from('<I', inner, ip)[0]; ip += 4
                ps_size = struct.unpack_from('<I', inner, ip)[0]; ip += 4

                file_info['ps_version'] = ps_ver
                file_info['ps_flags'] = f'0x{ps_flags:X}'
                file_info['ps_data_size'] = ps_size

                try:
                    pc = struct.unpack_from('<I', inner, ip)[0]; ip += 4 + pc * 8
                    gc = struct.unpack_from('<I', inner, ip)[0]; ip += 4
                    props = {}
                    for g in range(gc):
                        ts = struct.unpack_from('<Q', inner, ip)[0]; ip += 8
                        prc = struct.unpack_from('<I', inner, ip)[0]; ip += 4
                        for pr in range(prc):
                            key = struct.unpack_from('<Q', inner, ip)[0]; ip += 8
                            pname = NAMES.get(key, f'0x{key:016X}')
                            if ts == 0x7CACEEBCD26D075C:
                                val = struct.unpack_from('<i', inner, ip)[0]; ip += 4
                                props[pname] = val
                            elif ts == 0xCD9C6E605F5AF4B4:
                                sl = struct.unpack_from('<I', inner, ip)[0]; ip += 4
                                sv = inner[ip:ip + sl].decode('utf-8', errors='replace'); ip += sl
                                props[pname] = sv
                            elif ts == 0x9004C5587575D6C0:
                                bv = inner[ip] == 0x31; ip += 1
                                props[pname] = bv
                    file_info['properties'] = props
                except Exception as e:
                    file_info['parse_error'] = str(e)

        info[name] = file_info

    return info


print("=" * 70)
print("SAVE FILE VALIDATION")
print("=" * 70)

for label, path in [
    ('BACKUP SLOT', os.path.join(BACKUP_DIR, 'wd1_saveslot1.bundle')),
    ('CURRENT SLOT', os.path.join(SAVE_DIR, 'wd1_saveslot1.bundle')),
    ('BACKUP AUTO', os.path.join(BACKUP_DIR, '_wd1_saveslot1_autosave.bundle')),
    ('CURRENT AUTO', os.path.join(SAVE_DIR, '_wd1_saveslot1_autosave.bundle')),
]:
    if not os.path.exists(path):
        print(f'\n{label}: FILE NOT FOUND')
        continue
    info = parse_bundle_full(path)
    print(f'\n{label} ({os.path.basename(path)}):')
    print(f'  Bundle size: {info["size"]}')
    print(f'  Outer debug: {info["outer_dbg_size"]} bytes = {info["outer_dbg_hex"][:80]}')
    print(f'  Async compressed: {info["async_compressed"]}')
    print(f'  Files ({info["file_count"]}):')
    for name, off, sz in info['files']:
        print(f'    {name}: offset={off}, size={sz}')
        if name in info:
            fi = info[name]
            print(f'      inner: def={fi["inner_def"]}, dbg={fi["inner_dbg"]}, async={fi["inner_async"]}, versions={fi["inner_versions"]}')
            if 'inner_dbg_hex' in fi:
                print(f'      dbg bytes: {fi["inner_dbg_hex"]}')
            if 'properties' in fi:
                for k, v in fi['properties'].items():
                    print(f'      {k} = {repr(v)}')
            if 'ps_version' in fi:
                print(f'      PS: ver={fi["ps_version"]}, flags={fi["ps_flags"]}, data_size={fi["ps_data_size"]}')

# Compare backup vs current
print("\n" + "=" * 70)
print("DIFFERENCES")
print("=" * 70)
for fname in ['wd1_saveslot1.bundle', '_wd1_saveslot1_autosave.bundle']:
    bp = os.path.join(BACKUP_DIR, fname)
    cp = os.path.join(SAVE_DIR, fname)
    if os.path.exists(bp) and os.path.exists(cp):
        bd = open(bp, 'rb').read()
        cd = open(cp, 'rb').read()
        if bd == cd:
            print(f'\n{fname}: IDENTICAL (not edited yet)')
        else:
            print(f'\n{fname}: DIFFERENT (backup={len(bd)}, current={len(cd)})')
