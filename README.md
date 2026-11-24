# LibreOffice PDF background worker

A .NET 10 Worker Service that runs a conversion once at startup, logs the result,
and stops. It uses BackgroundService and the .NET generic host. It is a console
host; Windows Service installation is not configured.

This was only created to test https://bugs.documentfoundation.org/show_bug.cgi?id=157815
to see if a .NET Worker Service can reliably run a LibreOffice conversion in the background and wait for completion. 
It is not intended to be a production-ready solution.

## Run

Install the .NET 10 SDK and LibreOffice, then run from this directory:

    dotnet run

The default conversion is equivalent to:

    "C:/Program Files/LibreOffice/program/soffice.exe" --headless --convert-to pdf "D:/tdf/tmp/Test.odt" --outdir "D:/tdf/tmp/Files_Output"

The expected result is D:/tdf/tmp/Files_Output/Test.pdf. The worker creates the
output directory if necessary. LibreOffice may overwrite an existing PDF with
the same name. --headless runs LibreOffice without showing its UI.

## Configuration

Edit the Conversion section of appsettings.json, or override values on the command line:

    dotnet run --Conversion:InputFile="D:/tdf/tmp/Example.docx" --Conversion:OutputDirectory="D:/tdf/tmp/Files_Output"

Both the supplied ODT example and Word documents such as DOCX can be used.
TimeoutSeconds defaults to 120. Ctrl+C cancels the conversion and terminates the
started process tree. The program exits with code 0 after success and 1 after a
conversion failure or cancellation.

Close other LibreOffice instances before running this sample. LibreOffice can
forward commands to an existing instance, which can interfere with waiting for
completion and cancellation.

The worker captures LibreOffice output, checks its exit code, and verifies that a
nonempty PDF was created or updated, since LibreOffice can return code 0 on failure.

.NET Worker Service reference:
https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
