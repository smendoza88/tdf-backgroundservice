namespace Tdf.BackgroundService;

public sealed class ConversionOptions
{
    public const string SectionName = "Conversion";

    public required string LibreOfficePath { get; set; }
    public required string InputFile { get; set; }
    public required string OutputDirectory { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
}
