using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NiL.Dns;
using NiL.Dns.Records;

namespace UnitTests
{
    [TestClass]
    public class SimpleTests
    {
        [TestMethod]
        public async Task GoogleComCanBeResolved()
        {
            var results = await DnsClient.Query([new IPAddress(0x08080808)], new DnsQuery("google.com", DnsRecordType.A, DnsQClass.Internet));

            var httpHandler = new HttpClientHandler();
            httpHandler.ServerCertificateCustomValidationCallback = delegate { return true; };
            httpHandler.AllowAutoRedirect = true;
            httpHandler.MaxAutomaticRedirections = 10;
            var client = new HttpClient(httpHandler);
            var response = client.GetAsync("https://" + new IPAddress((results.First().RData as ARecord)!.Address) + "/").GetAwaiter().GetResult();

            Assert.IsNotNull(response);
            var pageContent = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
            Assert.IsTrue(pageContent.Contains("google.com"));
        }
    }
}
