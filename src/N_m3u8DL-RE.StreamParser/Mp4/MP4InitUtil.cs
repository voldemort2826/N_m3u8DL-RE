using System.Text;

using N_m3u8DL_RE.Common.Util;

namespace N_m3u8DL_RE.StreamParser.Mp4
{
    public class ParsedMP4Info
    {
        public string? PSSH { get; set; }
        public string? KID { get; set; }
        public string? Scheme { get; set; }
        public bool IsMultiDRM { get; set; }
    }

    public static class MP4InitUtil
    {
        private static readonly byte[] SYSTEM_ID_WIDEVINE = [0xED, 0xEF, 0x8B, 0xA9, 0x79, 0xD6, 0x4A, 0xCE, 0xA3, 0xC8, 0x27, 0xDC, 0xD5, 0x1D, 0x21, 0xED];
#pragma warning disable IDE0052 // Remove unread private members
        private static readonly byte[] SYSTEM_ID_PLAYREADY = [0x9A, 0x04, 0xF0, 0x79, 0x98, 0x40, 0x42, 0x86, 0xAB, 0x92, 0xE6, 0x5B, 0xE0, 0x88, 0x5F, 0x95];
#pragma warning restore IDE0052 // Remove unread private members
        private static readonly byte[] SCHM_FOURCC = [0x73, 0x63, 0x68, 0x6d]; // "schm"
        private static readonly byte[] TENC_FOURCC = [0x74, 0x65, 0x6E, 0x63]; // "tenc"
        private const string ZERO_KID = "00000000000000000000000000000000";

        public static ParsedMP4Info ReadInit(byte[] data)
        {
            ParsedMP4Info info = new();

            // parse init
            new MP4Parser()
                .Box("moov", MP4Parser.Children)
                .Box("trak", MP4Parser.Children)
                .Box("mdia", MP4Parser.Children)
                .Box("minf", MP4Parser.Children)
                .Box("stbl", MP4Parser.Children)
                .FullBox("stsd", MP4Parser.SampleDescription)
                .FullBox("pssh", box =>
                {
                    if (box.Version is not (0 or 1))
                    {
                        throw new InvalidDataException($"PSSH version can only be 0 or 1, but got {box.Version}");
                    }

                    byte[] systemId = box.Reader.ReadBytes(16);
                    if (!SYSTEM_ID_WIDEVINE.SequenceEqual(systemId))
                    {
                        return;
                    }

                    uint dataSize = box.Reader.ReadUInt32();
                    byte[] psshData = box.Reader.ReadBytes((int)dataSize);

                    if (info.KID != ZERO_KID)
                    {
                        return;
                    }

                    info.PSSH = Convert.ToBase64String(psshData);

                    // Guard info.KID before overwriting
                    if (string.IsNullOrEmpty(info.KID) || info.KID == ZERO_KID)
                    {
                        info.KID = HexUtil.BytesToHex(psshData.AsSpan(2, 16)).ToLowerInvariant();
                        info.IsMultiDRM = true;
                    }
                })
                .FullBox("encv", MP4Parser.AllData(data => ReadBox(data, info)))
                .FullBox("enca", MP4Parser.AllData(data => ReadBox(data, info)))
                .FullBox("enct", MP4Parser.AllData(data => ReadBox(data, info)))
                .FullBox("encs", MP4Parser.AllData(data => ReadBox(data, info)))
                .Parse(data, stopOnPartial: true);

            return info;
        }

        private static int FindPattern(ReadOnlySpan<byte> data, ReadOnlySpan<byte> pattern)
        {
            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                if (data.Slice(i, pattern.Length).SequenceEqual(pattern))
                {
                    return i;
                }
            }
            return -1;
        }

        private static void ReadBox(ReadOnlySpan<byte> data, ParsedMP4Info info)
        {
            // find scheme info
            int schmIndex = FindPattern(data, SCHM_FOURCC);
            if (schmIndex != -1 && schmIndex + 12 <= data.Length)
            {
                info.Scheme = Encoding.UTF8.GetString(data.Slice(schmIndex + 8, 4));
            }

            // if (info.Scheme != "cenc") return;

            // Find KID in tenc box
            int tencIndex = FindPattern(data, TENC_FOURCC);
            if (tencIndex != -1 && tencIndex + 28 <= data.Length)
            {
                info.KID = HexUtil.BytesToHex(data.Slice(tencIndex + 12, 16)).ToLowerInvariant();
            }
        }
    }
}
