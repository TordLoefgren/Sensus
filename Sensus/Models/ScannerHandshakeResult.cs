namespace Sensus.Models
{
    public readonly record struct ScannerHandshakeResult(
        ScannerDefinition Definition,
        ScannerConfiguration Configuration
    );
}
