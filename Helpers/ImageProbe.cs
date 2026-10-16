namespace TmcTryoutSystem.Helpers;

/// <summary>
/// Reads the pixel dimensions straight from image file headers. Used to reject
/// student-ID photos that are too small to be legible while reviewing them.
/// Supports JPEG, PNG, WEBP and GIF without pulling in an imaging package.
/// </summary>
public static class ImageProbe
{
    public static bool TryGetDimensions(Stream stream, out int width, out int height)
    {
        width = 0;
        height = 0;
        try
        {
            if (!stream.CanSeek) return false;
            stream.Position = 0;

            // Need enough bytes for the largest header we inspect.
            var header = new byte[Math.Min(32, (int)Math.Min(stream.Length, 32))];
            int read = stream.Read(header, 0, header.Length);
            if (read < 12) return false;
            stream.Position = 0;

            if (IsPng(header)) return TryPng(stream, out width, out height);
            if (IsJpeg(header)) return TryJpeg(stream, out width, out height);
            if (IsWebp(header)) return TryWebp(stream, header, out width, out height);
            if (IsGif(header)) return TryGif(header, out width, out height);
        }
        catch (IOException)
        {
            return false;
        }
        return false;
    }

    private static bool IsPng(byte[] h) =>
        h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47;

    private static bool IsJpeg(byte[] h) => h[0] == 0xFF && h[1] == 0xD8;

    private static bool IsGif(byte[] h) => h[0] == 'G' && h[1] == 'I' && h[2] == 'F';

    private static bool IsWebp(byte[] h) =>
        h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' &&
        h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P';

    private static bool TryPng(Stream s, out int width, out int height)
    {
        // Signature (8) + length (4) + "IHDR" (4) => width at 16, height at 20.
        width = height = 0;
        var buf = new byte[24];
        if (s.Read(buf, 0, 24) < 24) return false;
        if (buf[12] != 'I' || buf[13] != 'H' || buf[14] != 'D' || buf[15] != 'R') return false;
        width = BigEndian(buf, 16);
        height = BigEndian(buf, 20);
        return width > 0 && height > 0;
    }

    private static bool TryGif(byte[] h, out int width, out int height)
    {
        // Little-endian 16-bit values at offset 6 and 8.
        width = h[6] | (h[7] << 8);
        height = h[8] | (h[9] << 8);
        return width > 0 && height > 0;
    }

    private static bool TryJpeg(Stream s, out int width, out int height)
    {
        width = height = 0;
        s.Position = 2; // past SOI
        var two = new byte[2];
        while (s.Position < s.Length - 1)
        {
            int marker = s.ReadByte();
            if (marker != 0xFF) continue;

            // Skip fill bytes.
            int code;
            do { code = s.ReadByte(); } while (code == 0xFF);
            if (code < 0) return false;

            // Markers without a payload.
            if (code is 0xD8 or 0x01 || (code >= 0xD0 && code <= 0xD7)) continue;

            if (s.Read(two, 0, 2) < 2) return false;
            int length = (two[0] << 8) | two[1];
            if (length < 2) return false;

            bool isStartOfFrame = code is 0xC0 or 0xC1 or 0xC2 or 0xC3
                or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
            if (isStartOfFrame)
            {
                var sof = new byte[5];
                if (s.Read(sof, 0, 5) < 5) return false;
                height = (sof[1] << 8) | sof[2];
                width = (sof[3] << 8) | sof[4];
                return width > 0 && height > 0;
            }

            // Skip the rest of this segment (length includes the two size bytes).
            s.Position += length - 2;
        }
        return false;
    }

    private static bool TryWebp(Stream s, byte[] header, out int width, out int height)
    {
        width = height = 0;
        s.Position = 12; // past "RIFF" + size + "WEBP"
        var chunk = new byte[4];
        if (s.Read(chunk, 0, 4) < 4) return false;

        string fourCc = System.Text.Encoding.ASCII.GetString(chunk);
        if (fourCc == "VP8X")
        {
            // Canvas size is stored minus one, 24-bit little-endian.
            var rest = new byte[10];
            if (s.Read(rest, 0, 10) < 10) return false;
            width = 1 + (rest[4] | (rest[5] << 8) | (rest[6] << 16));
            height = 1 + (rest[7] | (rest[8] << 8) | (rest[9] << 16));
            return width > 0 && height > 0;
        }
        if (fourCc == "VP8 ")
        {
            // Lossy: 3-byte frame tag, 3-byte start code, then 14-bit width/height.
            var data = new byte[10];
            if (s.Read(data, 0, 10) < 10) return false;
            if (data[3] != 0x9D || data[4] != 0x01 || data[5] != 0x2A) return false;
            width = (data[6] | (data[7] << 8)) & 0x3FFF;
            height = (data[8] | (data[9] << 8)) & 0x3FFF;
            return width > 0 && height > 0;
        }
        if (fourCc == "VP8L")
        {
            var data = new byte[5];
            if (s.Read(data, 0, 5) < 5) return false;
            if (data[0] != 0x2F) return false;
            int bits = data[1] | (data[2] << 8) | (data[3] << 16) | (data[4] << 24);
            width = (bits & 0x3FFF) + 1;
            height = ((bits >> 14) & 0x3FFF) + 1;
            return width > 0 && height > 0;
        }
        return false;
    }

    private static int BigEndian(byte[] b, int offset) =>
        (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];
}
