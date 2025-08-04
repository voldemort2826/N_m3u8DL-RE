namespace N_m3u8DL_RE.Common.Util
{
    public static class HexUtil
    {
        public static string BytesToHex(byte[] data, string split = "")
        {
            return BytesToHex(data.AsSpan(), split);
        }

        public static string BytesToHex(ReadOnlySpan<byte> data, string split = "")
        {
            if (data.IsEmpty)
            {
                return string.Empty;
            }

            // Convert.ToHexString is the most efficient for spans in .NET 5+
            string hex = Convert.ToHexString(data);

            return string.IsNullOrEmpty(split) ? hex : string.Join(split, hex.AsEnumerable().Select(c => c.ToString()));
        }

        /// <summary>
        /// Determine if it is a HEX string
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool TryParseHexString(string input, out byte[]? bytes)
        {
            bytes = null;
            input = input.ToUpperInvariant();
            if (input.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                input = input[2..];
            }

            if (input.Length % 2 != 0)
            {
                return false;
            }

            if (input.Any(c => !"0123456789ABCDEF".Contains(c)))
            {
                return false;
            }

            bytes = HexToBytes(input);
            return true;
        }

        public static byte[] HexToBytes(string hex)
        {
            ReadOnlySpan<char> hexSpan = hex.AsSpan().Trim();
            if (hexSpan.StartsWith("0x") || hexSpan.StartsWith("0X"))
            {
                hexSpan = hexSpan[2..];
            }

            return Convert.FromHexString(hexSpan);
        }
    }
}