using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Globalization;

class Program
{
    const int UDP_DIAG_PORT = 6811;
    const int UDP_TST_PORT = 54321;

    static byte[] helloZGW = new byte[] { 0, 0, 0, 0, 0, 0x11 };

    struct GW
    {
        public int DiagnosticAddress { get; set; }
        public long MacAddress { get; set; }
        public string Vin { get; set; }
    }

    static void Main(string[] args)
    {
        var task = new Task(() =>
        {
            // setup the receive portion of the socket communication

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Any, UDP_TST_PORT));
            var buffer = new byte[256];
            var arg = new SocketAsyncEventArgs();
            arg.SetBuffer(buffer, 0, buffer.Length);
            arg.RemoteEndPoint = new IPEndPoint(0, 0);

            arg.Completed += (s, e) =>
            {
                Console.WriteLine("remoteEP: {0}", e.RemoteEndPoint);
                // "   2 DIAGADR10BMWMAC001000024C15BMWVINWBS3R9C54DEADBEEF"
                var r = Encoding.UTF8.GetString(buffer, 0, e.BytesTransferred);
                Console.WriteLine("reply: {0}", r);
                if (r.Length >= 55)
                {
                    var i = r.IndexOf("BMWMAC");
                    var mac = r.Substring(i + 6, 12);
                    i = r.IndexOf("BMWVIN");
                    var vin = r.Substring(i + 6, 17);
                    i = r.IndexOf("DIAGADR");
                    var addr = r.Substring(i + 7, 2);

                    var gw = new GW
                    {
                        DiagnosticAddress = int.Parse(addr, System.Globalization.NumberStyles.AllowHexSpecifier),
                        MacAddress = long.Parse(mac, NumberStyles.AllowHexSpecifier),
                        Vin = vin
                    };
                    Console.WriteLine("Diagaddr: {0}, MAC: {1}, VIN: {2}", addr, mac, vin);
                    Console.WriteLine("Diagaddr: {0:X02}, MAC: {1:X12}, VIN: {2}", gw.DiagnosticAddress, gw.MacAddress, gw.Vin);
                }
            };

            socket.ReceiveFromAsync(arg);

            // now loop through all network interfaces and send a hello packet

            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var netif in nics)
            {
                var ipProps = netif.GetIPProperties();
                foreach (var ipAddr in ipProps.UnicastAddresses)
                {
                    if (!IPAddress.IsLoopback(ipAddr.Address) &&
                    ipAddr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var broadcast = GetBroadcastAddress(ipAddr);
                        Console.WriteLine(ipAddr.IPv4Mask == null ? "No subnet defined" : "IPv4Address: " + ipAddr.Address + "\nIpv4 Mask: " + ipAddr.IPv4Mask.ToString() + "\nBroadcast: " + broadcast);
                        var ep = new IPEndPoint(broadcast, UDP_DIAG_PORT);
                        socket.SendTo(helloZGW, ep);
                    }
                }
            }
        });

        task.RunSynchronously();

        Console.ReadLine();
    }

    public static IPAddress GetBroadcastAddress(UnicastIPAddressInformation unicastAddress)
    {
        return GetBroadcastAddress(unicastAddress.Address, unicastAddress.IPv4Mask);
    }

    public static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
    {
        uint ipAddress = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
        uint ipMaskV4 = BitConverter.ToUInt32(mask.GetAddressBytes(), 0);
        uint broadCastIpAddress = ipAddress | ~ipMaskV4;

        return new IPAddress(BitConverter.GetBytes(broadCastIpAddress));
    }
}