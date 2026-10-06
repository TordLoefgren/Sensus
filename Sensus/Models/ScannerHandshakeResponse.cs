namespace Sensus.Models
{
    public readonly record struct ScannerHandshakeResponse(
        ScannerDefinition Definition,
        ScannerConfiguration Configuration
    );
}
