using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class DocumentService
{
    static DocumentService()
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }
        catch { }
    }

    public async Task<(string Content, Encoding Encoding, LineEnding LineEnding)> LoadAsync(string filePath)
    {
        byte[] bytes = await File.ReadAllBytesAsync(filePath);
        Encoding encoding = DetectEncoding(bytes);
        string content = encoding.GetString(StripBom(bytes, encoding));
        LineEnding lineEnding = DetectLineEnding(content);
        return (content, encoding, lineEnding);
    }

    public async Task SaveAsync(string filePath, string content, Encoding encoding, LineEnding lineEnding)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string normalized = NormalizeLineEndings(content, lineEnding);
        Encoding utf8NoBom = encoding is UTF8Encoding ? new UTF8Encoding(false) : encoding;
        await File.WriteAllTextAsync(filePath, normalized, utf8NoBom);
    }

    public static string NormalizeLineEndings(string text, LineEnding lineEnding)
    {
        string unified = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return lineEnding == LineEnding.CRLF ? unified.Replace("\n", "\r\n") : unified;
    }

    public static LineEnding DetectLineEnding(string text)
    {
        int crlf = 0;
        int lf = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                if (i > 0 && text[i - 1] == '\r') crlf++; else lf++;
            }
        }
        return crlf >= lf ? LineEnding.CRLF : LineEnding.LF;
    }

    public static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return new UTF8Encoding(true);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode;
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return Encoding.BigEndianUnicode;
        if (LooksLikeUtf8(bytes))
            return new UTF8Encoding(false);
        return Encoding.GetEncoding(1252);
    }

    private static byte[] StripBom(byte[] bytes, Encoding encoding)
    {
        byte[] preamble = encoding.GetPreamble();
        if (preamble.Length == 0 || bytes.Length < preamble.Length) return bytes;
        for (int i = 0; i < preamble.Length; i++)
        {
            if (bytes[i] != preamble[i]) return bytes;
        }
        byte[] stripped = new byte[bytes.Length - preamble.Length];
        Array.Copy(bytes, preamble.Length, stripped, 0, stripped.Length);
        return stripped;
    }

    private static bool LooksLikeUtf8(byte[] bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];
            int extra;
            if (b < 0x80) extra = 0;
            else if ((b & 0xE0) == 0xC0) extra = 1;
            else if ((b & 0xF0) == 0xE0) extra = 2;
            else if ((b & 0xF8) == 0xF0) extra = 3;
            else return false;

            if (i + extra >= bytes.Length) return false;
            for (int j = 1; j <= extra; j++)
            {
                if ((bytes[i + j] & 0xC0) != 0x80) return false;
            }
            i += extra + 1;
        }
        return true;
    }
}
