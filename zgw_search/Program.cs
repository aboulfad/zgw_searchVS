/* MIT License

Copyright(c) 2016 aboulfad @BF, BP, CTUK

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

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

namespace zgw_search
{
    public class Program
    {
        private const int UDP_DIAG_PORT = 6811;
        private const int UDP_TST_PORT = 54321;

        static byte[] helloZGW = new byte[] { 0,0,0,0,0,0x11 };

        static IPAddress GetBroadCastIP(IPAddress host, IPAddress mask)
        {
            byte[] broadcastIPBytes = new byte[4];
            byte[] hostBytes = host.GetAddressBytes();
            byte[] maskBytes = mask.GetAddressBytes();
            for (int i = 0; i < 4; i++)
            {
                broadcastIPBytes[i] = (byte)(hostBytes[i] | (byte)~maskBytes[i]);
            }
            return new IPAddress(broadcastIPBytes);
        }

        static Socket getSocket() //create and bind socket
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                   ProtocolType.Udp);
            try
            {
                var localep = new IPEndPoint(IPAddress.Any, UDP_TST_PORT);
                s.Bind(localep);
            }
            catch (Exception e)
            {
                Console.WriteLine("Winsock error: " + e.ToString());
            }
            return s;
        }

        static void pingZGW(Socket sock)
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var netif in nics)
            {
                var ipProps = netif.GetIPProperties();
                foreach (var ipAddr in ipProps.UnicastAddresses)
                {
                    if (!IPAddress.IsLoopback(ipAddr.Address) && ipAddr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        var broadcast =  GetBroadCastIP( ipAddr.Address,  ipAddr.IPv4Mask);
                        Trace.TraceInformation(ipAddr.IPv4Mask == null ? "No subnet defined" : "Network Interface" + netif.Description + "\nipAddr: " + ipAddr.Address + "\nNetmask: " + ipAddr.IPv4Mask.ToString() + "\nBroadcast: " + broadcast);
                        var ep = new IPEndPoint(broadcast, UDP_DIAG_PORT);
                        sock.SendTo(helloZGW, ep);
                        Trace.TraceInformation("{0} was sent to {1}", BitConverter.ToString(helloZGW), ep);
                    }
                }
            }
            //MessageBox.Show("Message sent", "My Application",
            //MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);
        }

        [STAThread]
         static void Main()
        {
            /*Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1()); */
            Trace.Listeners.Add(new ConsoleTraceListener());
            //Trace.Listeners.Add(new TextWriterTraceListener(new System.IO.StreamWriter(@".\zgw_search.log", false)));
            
            var sock = getSocket();
            pingZGW(sock);

            byte[] data = new byte[256];
            var sender = new IPEndPoint(IPAddress.Any, UDP_TST_PORT);
            var ipRemote = (EndPoint)(sender);
            Console.WriteLine("Waiting to receive datagrams from client...");
            int len = sock.ReceiveFrom(data, data.Length, SocketFlags.None, ref ipRemote);
            string ZGW_reply = string.Join(string.Empty, Encoding.ASCII.GetString(data, 0, len).Skip(6));
            Trace.TraceInformation("Response is:{0}", ZGW_reply);

            string pattern = @"DIAGADR(.*)BMWMAC(.*)BMWVIN(.*)";
            foreach (Match match in Regex.Matches(ZGW_reply, pattern, RegexOptions.IgnoreCase))
            {
                string diagAddr = match.Groups[1].Value;
                string ZgwIP = ((IPEndPoint)(ipRemote)).Address.ToString();
                string ZgwMAC = match.Groups[2].Value;
                string ZgwVIN = match.Groups[3].Value;             
                Trace.TraceInformation("\nDiagAddr: {0}\nZgw VIN: {1}\nZgwMAC: {2}\nZgwVIN: {3}", diagAddr, ZgwIP, ZgwMAC, ZgwVIN);
            }
            sock.Close();
            Trace.Flush();
        }
    }
}
