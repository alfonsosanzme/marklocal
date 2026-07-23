using MarkLocal.Core;
using Xunit;

namespace MarkLocal.Tests;

public class CliRunnerTests
{
    [Fact]
    public void Parse_NoArgs_ReturnsShowUi()
    {
        var parsed = CliRunner.Parse(System.Array.Empty<string>());
        Assert.Equal(CliRunner.Action.ShowUi, parsed.Action);
        Assert.Null(parsed.InputPath);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void Parse_HelpFlag_ReturnsShowHelp(string flag)
    {
        var parsed = CliRunner.Parse(new[] { flag });
        Assert.Equal(CliRunner.Action.ShowHelp, parsed.Action);
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public void Parse_VersionFlag_ReturnsShowVersion(string flag)
    {
        var parsed = CliRunner.Parse(new[] { flag });
        Assert.Equal(CliRunner.Action.ShowVersion, parsed.Action);
    }

    [Fact]
    public void Parse_BarePath_OpensInUi()
    {
        var parsed = CliRunner.Parse(new[] { @"C:\docs\notas.md" });
        Assert.Equal(CliRunner.Action.ShowUi, parsed.Action);
        Assert.Equal(@"C:\docs\notas.md", parsed.InputPath);
    }

    [Fact]
    public void Parse_ExportHtml_RequiresPath()
    {
        var parsed = CliRunner.Parse(new[] { "--export-html" });
        Assert.NotNull(parsed.ErrorMessage);
    }

    [Fact]
    public void Parse_ExportHtml_WithInputAndOutput()
    {
        var parsed = CliRunner.Parse(new[] { "--export-html", "doc.md", "--output", "salida.html" });
        Assert.Equal(CliRunner.Action.ExportHtml, parsed.Action);
        Assert.Equal("doc.md", parsed.InputPath);
        Assert.Equal("salida.html", parsed.OutputPath);
        Assert.Null(parsed.ErrorMessage);
    }

    [Fact]
    public void Parse_ExportHtml_WithoutOutput_LeavesOutputNull()
    {
        var parsed = CliRunner.Parse(new[] { "--export-html", "doc.md" });
        Assert.Equal(CliRunner.Action.ExportHtml, parsed.Action);
        Assert.Equal("doc.md", parsed.InputPath);
        Assert.Null(parsed.OutputPath);
    }

    [Theory]
    [InlineData("light", false, true)]
    [InlineData("dark", true, true)]
    public void Parse_ThemeFlag_SetsForceThemeAndDark(string theme, bool dark, bool forced)
    {
        var parsed = CliRunner.Parse(new[] { "--theme", theme });
        Assert.Equal(forced, parsed.ForceTheme);
        Assert.Equal(dark, parsed.Dark);
        Assert.Null(parsed.ErrorMessage);
    }

    [Fact]
    public void Parse_UnknownTheme_ReturnsError()
    {
        var parsed = CliRunner.Parse(new[] { "--theme", "neon" });
        Assert.NotNull(parsed.ErrorMessage);
    }

    [Fact]
    public void Parse_UnknownFlag_ReturnsError()
    {
        var parsed = CliRunner.Parse(new[] { "--no-existe" });
        Assert.NotNull(parsed.ErrorMessage);
    }

    [Fact]
    public void HelpText_MentionsKeyFlags()
    {
        string help = CliRunner.GetHelpText();
        Assert.Contains("--export-html", help);
        Assert.Contains("--output", help);
        Assert.Contains("--theme", help);
        Assert.Contains("--version", help);
        Assert.Contains("--help", help);
    }
}
