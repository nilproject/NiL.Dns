namespace NiL.Dns;

public struct DnsQuery
{
    public DnsQuery(string host, DnsRecordType dnsRecordType, DnsQClass dnsQClass)
    {
        Host = host;
        DnsRecordType = dnsRecordType;
        DnsQueryClass = dnsQClass;
    }

    public string Host { get; }
    public DnsRecordType DnsRecordType { get; }
    public DnsQClass DnsQueryClass { get; }
}
