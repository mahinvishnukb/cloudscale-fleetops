using FleetOps.Application.Manifests;
using Xunit;

namespace FleetOps.UnitTests.Application;

public sealed class Rfc4180ReaderTests
{
    [Fact]
    public void Reads_simple_rows()
    {
        var records = Rfc4180Reader.ReadAll("a,b,c\n1,2,3\n");

        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "1", "2", "3" }, records[1]);
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        var records = Rfc4180Reader.ReadAll("a,b\r\n1,2\r\n");

        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "1", "2" }, records[1]);
    }

    [Fact]
    public void Preserves_commas_inside_quoted_fields()
    {
        var records = Rfc4180Reader.ReadAll("desc,weight\n\"Bolts, assorted\",1200\n");

        Assert.Equal("Bolts, assorted", records[1][0]);
        Assert.Equal("1200", records[1][1]);
    }

    [Fact]
    public void Unescapes_doubled_quotes()
    {
        var records = Rfc4180Reader.ReadAll("desc\n\"12\"\" pipe\"\n");

        Assert.Equal("12\" pipe", records[1][0]);
    }

    [Fact]
    public void Preserves_newlines_inside_quoted_fields()
    {
        var records = Rfc4180Reader.ReadAll("desc,port\n\"line one\nline two\",CAVAN\n");

        Assert.Equal(2, records.Count);
        Assert.Equal("line one\nline two", records[1][0]);
        Assert.Equal("CAVAN", records[1][1]);
    }

    [Fact]
    public void Trims_unquoted_fields_but_not_quoted_ones()
    {
        var records = Rfc4180Reader.ReadAll("a,b\n  padded  ,\"  kept  \"\n");

        Assert.Equal("padded", records[1][0]);
        Assert.Equal("  kept  ", records[1][1]);
    }

    [Fact]
    public void Skips_blank_lines()
    {
        var records = Rfc4180Reader.ReadAll("a,b\n\n1,2\n\n\n");
        Assert.Equal(2, records.Count);
    }

    [Fact]
    public void Handles_a_final_row_without_a_trailing_newline()
    {
        var records = Rfc4180Reader.ReadAll("a,b\n1,2");
        Assert.Equal(2, records.Count);
        Assert.Equal(new[] { "1", "2" }, records[1]);
    }

    [Fact]
    public void Empty_input_yields_no_records()
        => Assert.Empty(Rfc4180Reader.ReadAll(string.Empty));
}
