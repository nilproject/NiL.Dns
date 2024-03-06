namespace NiL.Dns.Records;

public class MxRecord : DnsRecordDataBase
{
    public ushort Preference { get; internal set; }
    public string Exchange { get; internal set; }

    public override string ToString() => Preference + " " + Exchange;
}
