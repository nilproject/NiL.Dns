using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NiL.Dns.Records;

namespace NiL.Dns;

public static class DnsClient
{
    public static Task<IReadOnlyList<DnsResponse>> Query(IEnumerable<IPAddress> dnsAddresses, params DnsQuery[] dnsQueries)
    {
        var queryBinary = new List<byte>
        {
            0xBE,
            0xAF,

            0x01, // 0 - QR 0000 - OPCODE 0 - AA 0 - RC 1 - RD
            0x80, // 1 - RA 000 - Z 0000 - RCODE

            0x00, // 16 bit count of queries
            checked((byte)dnsQueries.Length),

            0x00, // 16 bit count of answers
            0x00,

            0x00, // 16 bit count of records in answer
            0x00,

            0x00, // 16 bit count of additional records in answer
            0x00
        };

        foreach (var query in dnsQueries)
        {
            var labels = query.Host.Split('.');

            foreach (var label in labels)
            {
                queryBinary.Add(checked((byte)label.Length));
                for (var i = 0; i < label.Length; i++)
                {
                    queryBinary.Add(checked((byte)label[i]));
                }
            }

            queryBinary.Add(0);

            queryBinary.Add(0);
            queryBinary.Add(checked((byte)query.DnsRecordType));

            queryBinary.Add(0);
            queryBinary.Add(checked((byte)query.DnsQueryClass));
        }

        var bytesArray = queryBinary.ToArray();

        var clients = new List<TcpClient>();

        var work = true;

        var result = new TaskCompletionSource<IReadOnlyList<DnsResponse>>();

        var connections = 0;
        var init = true;
        var sync = new object();

        lock (sync)
            foreach (var dnsIp in dnsAddresses)
            {
                if (dnsIp.AddressFamily != AddressFamily.InterNetwork && dnsIp.AddressFamily != AddressFamily.InterNetworkV6)
                    continue;

                connections++;

                //Task.Run(() =>
                //{
                //    Monitor.Enter(sync);
                //    Monitor.Exit(sync);

                var client = new UdpClient(dnsIp.AddressFamily);
                client.BeginSend(
                    bytesArray,
                    bytesArray.Length,
                    new IPEndPoint(dnsIp, 53),
                    _ =>
                    {
                        try
                        {
                            if (!work)
                            {
                                client.Dispose();
                                return;
                            }

                            var start = Environment.TickCount;
                            while (client.Available == 0 && Environment.TickCount - start < 1000)
                                Thread.Sleep(1);

                            if (client.Available == 0)
                                return;

                            var response = new byte[client.Available];
                            var count = client.Client.Receive(response);
                            if (count != 0)
                            {
                                var responses = parseResponse(response);
                                lock (sync)
                                {
                                    if (!result.Task.IsCompleted)
                                        result.SetResult(responses);
                                }
                            }
                        }
                        finally
                        {
                            Interlocked.Decrement(ref connections);
                            if (!init && connections == 0)
                                lock (sync)
                                {
                                    if (!result.Task.IsCompleted)
                                        result.SetResult(null);
                                }
                        }
                    },
                    null);
                //});

                // для TCP нужно добавить два байта в начале - длина сообщения без этих двух байтов
            }

        init = false;

        if (connections == 0 && !result.Task.IsCompleted)
            result.SetResult(null);

        result.Task.ContinueWith(_ =>
        {
            work = false;
            foreach (var client in clients)
                client.Dispose();
        });

        return result.Task;
    }

    private static List<DnsResponse> parseResponse(byte[] response)
    {
        var i = 0;

        var id = (response[i++] << 8) | response[i++];

        var qr = (response[i] & 0x80) != 0;
        var opcode = (response[i] >> 3) & 0xf;
        var aa = (response[i] & 0x4) != 0;
        var tc = (response[i] & 0x2) != 0;
        var rd = (response[i] & 0x1) != 0;
        i++;

        var ra = (response[i] & 0x80) != 0;
        var z = (response[i] >> 4) & 0x7;
        var rcode = response[i] & 0xf;
        i++;

        var queryCount = (response[i++] << 8) | response[i++];
        var answerCount = (response[i++] << 8) | response[i++];
        var authorityAnswerCount = (response[i++] << 8) | response[i++];
        var additionalAnswerCount = (response[i++] << 8) | response[i++];

        var sbBuffer = new StringBuilder();

        var queries = new List<DnsQuery>();
        var answers = new List<DnsResponse>();

        for (var ri = 0; ri < queryCount; ri++)
        {
            sbBuffer.Length = 0;
            i += readString(response, sbBuffer, i);

            var qType = (response[i++] << 8) | response[i++];
            var qClass = (response[i++] << 8) | response[i++];

            queries.Add(new DnsQuery(sbBuffer.ToString(), (DnsRecordType)qType, (DnsQClass)qClass));
        }

        var recordsCount = answerCount + authorityAnswerCount + additionalAnswerCount;
        for (var ri = 0; ri < recordsCount; ri++)
        {
            sbBuffer.Length = 0;
            i += readString(response, sbBuffer, i);
            var name = sbBuffer.ToString();

            var type = (DnsRecordType)((response[i++] << 8) | response[i++]);

            var @class = (response[i++] << 8) | response[i++];

            var ttlUp = (response[i++] << 8) | response[i++];
            var ttlLow = (response[i++] << 8) | response[i++];
            var ttl = (uint)((ttlUp << 16) | ttlLow);

            var dataLen = (response[i++] << 8) | response[i++];

            var section = DnsSection.Answer;
            if (answerCount-- <= 0)
            {
                section = DnsSection.Authority;
                if (authorityAnswerCount-- <= 0)
                    section = DnsSection.Additional;
            }

            var dataPtr = i;
            i += dataLen;

            DnsRecordDataBase rdata = null;

            switch (type)
            {
                case DnsRecordType.AAAA:
                {
                    if (dataLen != 16)
                        throw new InvalidOperationException();

                    rdata = new ARecord { Address = new ArraySegment<byte>(response, dataPtr, dataLen).ToArray() };
                    break;
                }

                case DnsRecordType.A:
                {
                    if (dataLen != 4)
                        throw new InvalidOperationException();

                    rdata = new ARecord { Address = new ArraySegment<byte>(response, dataPtr, dataLen).ToArray() };
                    break;
                }

                case DnsRecordType.MX:
                {
                    var mxrecord = new MxRecord();
                    mxrecord.Preference = (ushort)((response[dataPtr++] << 8) | response[dataPtr++]);

                    sbBuffer.Length = 0;
                    dataPtr += readString(response, sbBuffer, dataPtr);
                    mxrecord.Exchange = sbBuffer.ToString();

                    rdata = mxrecord;
                    break;
                }

                case DnsRecordType.NS:
                {
                    sbBuffer.Length = 0;
                    dataPtr += readString(response, sbBuffer, dataPtr);
                    rdata = new NsRecord { NsDName = sbBuffer.ToString() };
                    break;
                }

                case DnsRecordType.SOA:
                {
                    var soaRecord = new SoaRecord();

                    sbBuffer.Length = 0;
                    dataPtr += readString(response, sbBuffer, dataPtr);
                    soaRecord.MName = sbBuffer.ToString();

                    sbBuffer.Length = 0;
                    dataPtr += readString(response, sbBuffer, dataPtr);
                    soaRecord.RName = sbBuffer.ToString();

                    soaRecord.Serial = BitConverter.ToInt32(response, dataPtr);
                    dataPtr += 4;
                    soaRecord.Refresh = BitConverter.ToInt32(response, dataPtr);
                    dataPtr += 4;
                    soaRecord.Retry = BitConverter.ToInt32(response, dataPtr);
                    dataPtr += 4;
                    soaRecord.Expire = BitConverter.ToInt32(response, dataPtr);
                    dataPtr += 4;
                    soaRecord.Minimum = BitConverter.ToInt32(response, dataPtr);
                    dataPtr += 4;

                    rdata = soaRecord;

                    break;
                }

                default:
                {
                    rdata = new UnknownRecord { Data = new ArraySegment<byte>(response, dataPtr, dataLen).ToArray() };

                    dataPtr += dataLen;
                    break;
                }
            }

            answers.Add(new DnsResponse(name, type, (DnsQClass)@class, section, ttl, rdata));
        }

        return answers;
    }

    private static int readString(byte[] response, StringBuilder name, int position)
    {
        var skipSize = 0;
        var incSkip = true;
        int len = 0;
        var expectLen = true;
        var work = true;

        while (work && position < response.Length)
        {
            work = false;

            if (expectLen)
            {
                work = true;

                if (incSkip)
                    skipSize++;

                len = response[position++];
                if (len == 0)
                    break;

                if ((len & 0xc0) == 0xc0)
                {
                    if (incSkip)
                        skipSize++;

                    incSkip = false;
                    position = response[position];
                    len = ((len ^ 0xc0) << 8) | response[position++];
                }
            }

            expectLen = true;

            while (len-- > 0)
            {
                if (incSkip)
                    skipSize++;

                name.Append((char)response[position++]);
            }

            name.Append('.');
        }

        if (name.Length > 0)
            name.Length--;

        return skipSize;
    }
}
