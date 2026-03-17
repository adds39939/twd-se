import struct, zlib, os

def parse_metastream_header(data):
    def_size = struct.unpack_from('<I', data, 4)[0]
    dbg_size = struct.unpack_from('<I', data, 8)[0]
    async_size = struct.unpack_from('<I', data, 12)[0]
    ver_count = struct.unpack_from('<I', data, 16)[0]
    pos = 20 + ver_count * 12
    sections = []
    for size_field in [def_size, dbg_size, async_size]:
        compressed = (size_field & 0x80000000) != 0
        raw_size = size_field & 0x7FFFFFFF
        if raw_size == 0:
            sections.append(b'')
            continue
        raw = data[pos:pos+raw_size]
        pos += raw_size
        if not compressed:
            sections.append(raw)
        else:
            if len(raw) >= 4 and struct.unpack_from('<I', raw, 0)[0] == 0x5454435A:
                sections.append(decompress_ttcz(raw))
            else:
                try:
                    sections.append(zlib.decompress(raw))
                except:
                    try:
                        sections.append(zlib.decompress(raw, -15))
                    except:
                        sections.append(raw)
    return sections

def decompress_ttcz(data):
    ws = struct.unpack_from('<I', data, 4)[0]
    pc = struct.unpack_from('<I', data, 8)[0]
    offsets = [struct.unpack_from('<Q', data, 12 + i*8)[0] for i in range(pc+1)]
    result = bytearray()
    for i in range(pc):
        block = data[offsets[i]:offsets[i+1]]
        dec = zlib.decompress(block, -15)
        result.extend(dec)
    return bytes(result)

print("analyze_saves.py loaded successfully")
