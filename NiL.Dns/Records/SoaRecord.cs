namespace NiL.Dns.Records;

public class SoaRecord : DnsRecordDataBase
{
    public string MName { get; internal set; }
    public string RName { get; internal set; }
    public int Serial { get; internal set; }
    public int Refresh { get; internal set; }
    public int Retry { get; internal set; }
    public int Expire { get; internal set; }
    public int Minimum { get; internal set; }
}
