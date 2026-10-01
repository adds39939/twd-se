namespace TwdSaveEditor.Tools.DecodeEstore.Reporting;

public static class RecordFormatDescription
{
    public const string Text = """

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

    """;
}
