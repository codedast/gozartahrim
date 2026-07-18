namespace ServiceLib.Models.Dto;

public enum EAltIpSource
{
    Cloudflare,
    Fofa,
}

[Serializable]
public class AltIpFinderResult
{
    public string Ip { get; set; } = string.Empty;
    public EAltIpSource Source { get; set; }
    public bool TcpOk { get; set; }
    public bool TlsOk { get; set; }
    public int LatencyMs { get; set; } = -1;
}
