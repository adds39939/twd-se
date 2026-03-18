#!/usr/bin/env python3
"""
Create a minimal estore file with an "End Episode" event.
Uses the same MetaStream + EventLog format as the game's menu_log estores.
"""
import struct, os, sys

POLY = 0x42F0E1EBA9EA3693
def crc64(s):
    crc = 0
    for byte in s.encode('ascii').lower():
        crc ^= byte << 56
        for _ in range(8):
            if crc & (1 << 63):
                crc = ((crc << 1) ^ POLY) & 0xFFFFFFFFFFFFFFFF
            else:
                crc = (crc << 1) & 0xFFFFFFFFFFFFFFFF
    return crc

def create_end_episode_estore(log_name, num_episodes_to_finish=1):
    """Create an estore with End Episode events.

    Copies the structure from a real menu_log estore but with End Episode tag.
    """
    save_dir = r'C:\Users\Adam\Documents\Telltale Games\The Walking Dead Definitive'

    # Find a real menu_log to use as template
    template = None
    for f in sorted(os.listdir(save_dir)):
        if f.startswith('menu_log_') and f.endswith('.estore'):
            template = os.path.join(save_dir, f)

    if not template:
        print("No menu_log template found!")
        return None

    data = open(template, 'rb').read()

    # Parse template structure
    ver_count = struct.unpack_from('<I', data, 16)[0]
    header_end = 20 + ver_count * 12
    def_size = struct.unpack_from('<I', data, 4)[0] & 0x7FFFFFFF
    dbg_size = struct.unpack_from('<I', data, 8)[0] & 0x7FFFFFFF
    section = bytearray(data[header_end:header_end + def_size])
    dbg = data[header_end + def_size:header_end + def_size + dbg_size]

    # Find a "Button Press" event in the section and replace its tag with "End Episode"
    button_press_hash = struct.pack('<Q', crc64('Button Press'))
    end_episode_hash = struct.pack('<Q', crc64('End Episode'))

    pos = section.find(button_press_hash)
    if pos < 0:
        print("Could not find Button Press event in template!")
        return None

    # Replace the tag hash
    section[pos:pos+8] = end_episode_hash
    print(f"Replaced Button Press with End Episode at offset 0x{pos:X}")

    # Also update the log name in the section header
    # The log name is embedded in the default section
    old_name = os.path.splitext(os.path.basename(template))[0]
    new_name = log_name

    # Find and replace the log name string
    old_name_bytes = old_name.encode('ascii')
    new_name_bytes = new_name.encode('ascii')

    # The name appears with .estore extension in the section
    old_full = (old_name + '.estore').encode('ascii')
    new_full = (new_name + '.estore').encode('ascii')

    name_pos = bytes(section).find(old_full)
    if name_pos >= 0 and len(old_full) == len(new_full):
        section[name_pos:name_pos+len(new_full)] = new_full
        print(f"Updated log name to {new_name}")
    elif name_pos >= 0:
        print(f"WARNING: log name length mismatch ({len(old_full)} vs {len(new_full)}), keeping original name")

    # Rebuild the MetaStream file
    result = bytearray()
    # Copy original header (magic + sizes + version entries)
    result.extend(data[:header_end])

    # Update def size
    struct.pack_into('<I', result, 4, len(section))

    # Write sections
    result.extend(section)
    result.extend(dbg)

    return bytes(result)


if __name__ == '__main__':
    save_dir = r'C:\Users\Adam\Documents\Telltale Games\The Walking Dead Definitive'

    # The EventLog name for wd1_saveslot1 would be _wd1_saveslot1_id
    # But let's also check what the game's EventLog_GetLogName() would return
    # by looking at the naming pattern

    # For the menu_log, the game uses: menu_log_TIMESTAMP
    # For session, it uses: session_TIMESTAMP
    # For save slot EventLog, it might use: _SLOTNAME_id (like S3)

    # The game calls FileSetExtension(EventLog_GetLogName(), "estore")
    # EventLog_GetLogName() returns the log started by EventLog_Start
    # which is called in SaveLoad_SetSlot

    # Let's try creating an estore with the name pattern the game would expect
    log_name = "_wd1_saveslot1_id"
    estore_data = create_end_episode_estore(log_name)

    if estore_data:
        out_path = os.path.join(save_dir, f"{log_name}.estore")
        open(out_path, 'wb').write(estore_data)
        print(f"Wrote {out_path} ({len(estore_data)} bytes)")

        # Verify the End Episode hash is in the file
        end_ep = struct.pack('<Q', crc64('End Episode'))
        if end_ep in estore_data:
            print("Verified: End Episode event present")
        else:
            print("ERROR: End Episode event NOT found in output!")
