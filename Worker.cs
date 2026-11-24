using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace Tdf.BackgroundService;

public sealed class Worker(
    IOptions<ConversionOptions> options,
    ILogger<Worker> logger,
    IHostApplicationLifetime lifetime) : Microsoft.Extensions.Hosting.BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ConvertAsync(options.Value, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Conversion canceled.");
            Environment.ExitCode = 1;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Document conversion failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }

    private async Task ConvertAsync(ConversionOptions settings, CancellationToken stoppingToken)
    {
        var executable = Path.GetFullPath(settings.LibreOfficePath);
        var inputFile = Path.GetFullPath(settings.InputFile);
        var outputDirectory = Path.GetFullPath(settings.OutputDirectory);

        if (!File.Exists(executable))
            throw new FileNotFoundException("LibreOffice executable was not found.", executable);
        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input document was not found.", inputFile);

        Directory.CreateDirectory(outputDirectory);
        var outputFile = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(inputFile) + ".pdf");
        if (string.Equals(inputFile, outputFile, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The input document and output PDF must have different paths.");

        var previousWriteTime = File.Exists(outputFile)
            ? File.GetLastWriteTimeUtc(outputFile)
            : (DateTime?)null;

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        // ArgumentList handles spaces in paths without manual quoting.
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf");
        startInfo.ArgumentList.Add(inputFile);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDirectory);

        logger.LogInformation("Converting {InputFile} to {OutputFile}", inputFile, outputFile);
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException("LibreOffice could not be started.");

        // Drain both streams concurrently to prevent a full pipe from blocking LibreOffice.
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, timeout.Token);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // LibreOffice exited between checking HasExited and killing the process.
            }

            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(standardOutput, standardError);
            if (stoppingToken.IsCancellationRequested)
                throw;
            throw new TimeoutException($"LibreOffice exceeded the {settings.TimeoutSeconds}-second timeout.");
        }

        var output = await standardOutput;
        var error = await standardError;
        if (!string.IsNullOrWhiteSpace(output))
            logger.LogInformation("LibreOffice: {Output}", output.Trim());
        if (!string.IsNullOrWhiteSpace(error))
            logger.LogWarning("LibreOffice: {Error}", error.Trim());

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"LibreOffice exited with code {process.ExitCode}. {error}");

        // LibreOffice can exit successfully even when it did not produce a PDF.
        var pdf = new FileInfo(outputFile);
        if (!pdf.Exists || pdf.Length == 0 ||
            (previousWriteTime.HasValue && pdf.LastWriteTimeUtc == previousWriteTime.Value))
        {
            throw new InvalidOperationException(
                $"LibreOffice did not create or update a nonempty PDF at '{outputFile}'. {output} {error}");
        }

        logger.LogInformation("PDF created successfully: {OutputFile}", outputFile);
    }
}
