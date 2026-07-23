using System.IO;
using System.Text;
using System.Threading.Tasks;
using MarkLocal.Core;
using MarkLocal.Models;
using Xunit;

namespace MarkLocal.Tests;

public class DocumentServiceTests
{
    [Fact]
    public void DetectEncoding_Utf8WithBom()
    {
        byte[] bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x48, 0x6F, 0x6C, 0x61 };
        var enc = DocumentService.DetectEncoding(bytes);
        Assert.IsType<UTF8Encoding>(enc);
        Assert.NotEmpty(enc.GetPreamble());
    }

    [Fact]
    public void DetectEncoding_Utf16LE_FromBom()
    {
        byte[] bytes = new byte[] { 0xFF, 0xFE, 0x48, 0x00, 0x6F, 0x00 };
        var enc = DocumentService.DetectEncoding(bytes);
        Assert.Equal(Encoding.Unicode, enc);
    }

    [Fact]
    public void DetectEncoding_Utf16BE_FromBom()
    {
        byte[] bytes = new byte[] { 0xFE, 0xFF, 0x00, 0x48, 0x00, 0x6F };
        var enc = DocumentService.DetectEncoding(bytes);
        Assert.Equal(Encoding.BigEndianUnicode, enc);
    }

    [Fact]
    public void DetectEncoding_PlainAscii_TreatedAsUtf8()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("Hello World");
        var enc = DocumentService.DetectEncoding(bytes);
        Assert.IsType<UTF8Encoding>(enc);
    }

    [Fact]
    public void DetectEncoding_Utf8MultibyteWithoutBom_IsUtf8()
    {
        byte[] bytes = new UTF8Encoding(false).GetBytes("Año señor");
        var enc = DocumentService.DetectEncoding(bytes);
        Assert.IsType<UTF8Encoding>(enc);
    }

    [Fact]
    public void DetectEncoding_InvalidUtf8Sequence_FallsBackTo1252()
    {
        // 0xC3 sin segundo byte de continuación válido y luego un byte alto suelto
        byte[] bytes = new byte[] { 0xC3, 0x28, 0xE9 };
        var enc = DocumentService.DetectEncoding(bytes);
        Assert.Equal(1252, enc.CodePage);
    }

    [Fact]
    public void DetectLineEnding_PrefersCrlfWhenMajority()
    {
        var le = DocumentService.DetectLineEnding("a\r\nb\r\nc\n");
        Assert.Equal(LineEnding.CRLF, le);
    }

    [Fact]
    public void DetectLineEnding_PrefersLfWhenMajority()
    {
        var le = DocumentService.DetectLineEnding("a\nb\nc\r\n");
        Assert.Equal(LineEnding.LF, le);
    }

    [Theory]
    [InlineData("a\r\nb\nc\r", LineEnding.CRLF, "a\r\nb\r\nc\r\n")]
    [InlineData("a\r\nb\nc\r", LineEnding.LF, "a\nb\nc\n")]
    [InlineData("sin saltos", LineEnding.LF, "sin saltos")]
    public void NormalizeLineEndings_ConvertsConsistently(string input, LineEnding target, string expected)
    {
        Assert.Equal(expected, DocumentService.NormalizeLineEndings(input, target));
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripUtf8AndCrlf()
    {
        var svc = new DocumentService();
        string path = Path.Combine(TestFactory.CreateTempDir(), "doc.md");
        string content = "# Título\n\nLínea con acentos á é í ó ú.\nOtra línea.\n";

        await svc.SaveAsync(path, content, new UTF8Encoding(false), LineEnding.CRLF);
        var (loaded, encoding, lineEnding) = await svc.LoadAsync(path);

        Assert.Equal("# Título\r\n\r\nLínea con acentos á é í ó ú.\r\nOtra línea.\r\n", loaded);
        Assert.IsType<UTF8Encoding>(encoding);
        Assert.Equal(LineEnding.CRLF, lineEnding);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripLf()
    {
        var svc = new DocumentService();
        string path = Path.Combine(TestFactory.CreateTempDir(), "doc.md");
        string content = "uno\r\ndos\r\ntres";

        await svc.SaveAsync(path, content, new UTF8Encoding(false), LineEnding.LF);
        var (loaded, _, lineEnding) = await svc.LoadAsync(path);

        Assert.Equal("uno\ndos\ntres", loaded);
        Assert.Equal(LineEnding.LF, lineEnding);
    }

    [Fact]
    public async Task SaveAsync_DoesNotEmitUtf8Bom()
    {
        var svc = new DocumentService();
        string path = Path.Combine(TestFactory.CreateTempDir(), "doc.md");
        await svc.SaveAsync(path, "hola", new UTF8Encoding(false), LineEnding.LF);

        byte[] bytes = await File.ReadAllBytesAsync(path);
        Assert.NotEqual(0xEF, bytes[0]);
    }

    [Fact]
    public async Task LoadAsync_StripsBomFromContent()
    {
        var svc = new DocumentService();
        string path = Path.Combine(TestFactory.CreateTempDir(), "doc.md");
        File.WriteAllBytes(path, new byte[] { 0xEF, 0xBB, 0xBF, (byte)'h', (byte)'i' });

        var (loaded, encoding, _) = await svc.LoadAsync(path);
        Assert.Equal("hi", loaded);
        Assert.IsType<UTF8Encoding>(encoding);
    }
}
