using FleetOps.Application.Manifests;
using Xunit;

namespace FleetOps.UnitTests.Application;

public sealed class ManifestCsvParserTests
{
    private const string Header =
        "container_number,description,gross_weight_kg,origin_port,destination_port,hazard_class";

    [Fact]
    public void Parses_a_clean_manifest()
    {
        var csv = $"{Header}\nCSQU3054383,Machine parts,12000,CAVAN,NLRTM,\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.True(result.IsClean);
        var row = Assert.Single(result.Rows);
        Assert.Equal("CSQU3054383", row.ContainerNumber);
        Assert.Equal(12_000m, row.GrossWeightKg);
        Assert.Null(row.HazardClass);
    }

    [Fact]
    public void Reports_missing_required_columns_and_stops()
    {
        var result = ManifestCsvParser.Parse("container_number,description\nCSQU3054383,Parts\n");

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Column == "header");
    }

    [Fact]
    public void Rejects_an_empty_file()
    {
        var result = ManifestCsvParser.Parse(string.Empty);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Column == "file");
    }

    [Fact]
    public void A_bad_row_does_not_discard_the_good_ones()
    {
        var csv = $"{Header}\n" +
                  "CSQU3054383,Machine parts,12000,CAVAN,NLRTM,\n" +
                  "BADCONTAINER,Junk,900,CAVAN,NLRTM,\n" +
                  "MSKU3820945,Textiles,8000,CAVAN,NLRTM,\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.Equal(2, result.Rows.Count);
        Assert.Single(result.Errors);
        Assert.Equal(3, result.Errors[0].LineNumber);
    }

    [Fact]
    public void Flags_a_container_that_repeats_within_the_file()
    {
        var csv = $"{Header}\n" +
                  "CSQU3054383,Parts,12000,CAVAN,NLRTM,\n" +
                  "CSQU3054383,Parts again,9000,CAVAN,NLRTM,\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.Single(result.Rows);
        Assert.Contains(result.Errors, e => e.Message.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("-100")]
    [InlineData("0")]
    [InlineData("45000")]
    public void Rejects_unusable_weights(string weight)
    {
        var csv = $"{Header}\nCSQU3054383,Parts,{weight},CAVAN,NLRTM,\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Column == "gross_weight_kg");
    }

    [Fact]
    public void Requires_both_ports()
    {
        var csv = $"{Header}\nCSQU3054383,Parts,12000,,NLRTM,\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Column.Contains("origin_port", StringComparison.Ordinal));
    }

    [Fact]
    public void Reads_the_hazard_class_when_present()
    {
        var csv = $"{Header}\nTGHU1234567,Paint,4000,CAVAN,NLRTM,3\n";

        var row = Assert.Single(ManifestCsvParser.Parse(csv).Rows);
        Assert.Equal("3", row.HazardClass);
    }

    [Fact]
    public void Header_matching_ignores_case_and_spacing()
    {
        var csv = "Container Number,Description,Gross Weight KG,Origin Port,Destination Port\n" +
                  "CSQU3054383,Parts,12000,CAVAN,NLRTM\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.Single(result.Rows);
        Assert.True(result.IsClean);
    }

    [Fact]
    public void Line_numbers_point_at_the_offending_line_in_the_file()
    {
        var csv = $"{Header}\n" +
                  "CSQU3054383,Parts,12000,CAVAN,NLRTM,\n" +
                  "MSKU3820945,Parts,12000,CAVAN,NLRTM,\n" +
                  "NOPE,Parts,12000,CAVAN,NLRTM,\n";

        var result = ManifestCsvParser.Parse(csv);

        Assert.Equal(4, Assert.Single(result.Errors).LineNumber);
    }
}
