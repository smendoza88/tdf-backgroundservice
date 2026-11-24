using Tdf.BackgroundService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddOptions<ConversionOptions>()
    .Bind(builder.Configuration.GetSection(ConversionOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.LibreOfficePath),
        "LibreOfficePath is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.InputFile),
        "InputFile is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.OutputDirectory),
        "OutputDirectory is required.")
    .Validate(options => options.TimeoutSeconds is > 0 and <= 3600,
        "TimeoutSeconds must be between 1 and 3600.")
    .ValidateOnStart();
builder.Services.AddHostedService<Worker>();

using var host = builder.Build();
await host.RunAsync();
