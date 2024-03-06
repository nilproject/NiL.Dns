using NiL.Dns.Records;

namespace NiL.Dns;

public struct DnsResponse
{
    public DnsResponse(string name, DnsRecordType type, DnsQClass @class, DnsSection section, uint ttl, DnsRecordDataBase rdata)
    {
        Name = name;
        Type = type;
        Class = @class;
        Ttl = ttl;
        RData = rdata;
        Section = section;
    }

    public string Name { get; }
    public DnsRecordType Type { get; }
    public DnsQClass Class { get; }
    public DnsSection Section { get; set; }
    public uint Ttl { get; }
    public DnsRecordDataBase RData { get; }
}