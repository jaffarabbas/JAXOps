namespace IISManager.Shared.Helpers;

public static class CorrelationIdHelper
{
    public static string Generate() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
}
