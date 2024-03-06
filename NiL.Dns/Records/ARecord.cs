namespace NiL.Dns.Records;

public class ARecord : DnsRecordDataBase
{
    public byte[] Address { get; internal set; }

    public override string ToString() => Address[0] + "." + Address[1] + "." + Address[2] + "." + Address[3];
}
