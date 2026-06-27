namespace Raqmi.Client;

public static class ServerUrlNormalizer
{
    private const int DefaultPort = 3000;

    /// <summary>Accepte une URL complète ou une IP / nom d'hôte local (port 3000 par défaut).</summary>
    public static string Normalize(string input, int defaultPort = DefaultPort)
    {
        var value = input.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(value)) return value;

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return value;

        if (value.StartsWith('['))
        {
            var withPort = value.Contains("]:") ? value : $"{value}:{defaultPort}";
            return "http://" + withPort;
        }

        var colon = value.LastIndexOf(':');
        var host = colon > 0 && int.TryParse(value[(colon + 1)..], out _) ? value[..colon] : value;
        var portPart = colon > 0 && int.TryParse(value[(colon + 1)..], out var port) ? port : defaultPort;

        if (System.Net.IPAddress.TryParse(host, out _))
            return $"http://{host}:{portPart}";

        return colon > 0 && int.TryParse(value[(colon + 1)..], out _)
            ? "http://" + value
            : $"http://{host}:{portPart}";
    }
}
