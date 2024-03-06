using System.Net;
using System.Net.NetworkInformation;
using NiL.Dns;

namespace Sandbox;

internal class Program
{
    static void Main(string[] args)
    {
        asyncMain(args).GetAwaiter().GetResult();
    }

    static async Task asyncMain(string[] args)
    {
        var recordTypes = args.Where(x => Enum.TryParse<DnsRecordType>(x, true, out _)).Select(x => Enum.Parse<DnsRecordType>(x, true)).ToArray();
        var domains = args.Where(x => !Enum.TryParse<DnsRecordType>(x, true, out _) && !IPEndPoint.TryParse(x, out _)).ToArray();
        var dnsIps = getDnsIps();

        var requests = recordTypes.SelectMany(recordType => domains.Select(domain => new DnsQuery(domain, recordType, DnsQClass.Internet))).ToArray();
        var results = await DnsClient.Query(dnsIps, requests);

        foreach (var result in results)
        {
            Console.WriteLine(result.Name + " " + result.Type + " " + result.RData);
        }
    }

    private static List<IPAddress> getDnsIps()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
                                    .Where(x => x.OperationalStatus == OperationalStatus.Up
                                            && !x.IsReceiveOnly
                                            && x.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                                    .Reverse()
                                    .SelectMany(x => x.GetIPProperties().DnsAddresses)
                                    .ToList();
    }
}
