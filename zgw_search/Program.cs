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

namespace zgw_search
{
    public class Program
    {
        private const int UDP_DIAG_PORT = 6811;
        private const int UDP_TST_PORT = 54321;

        static byte[] helloZGW = new byte[] { (byte)0, (byte)0, (byte)0, (byte)0, (byte)0, (byte)0x11 };

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
            Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                   ProtocolType.Udp);
            try
            {
                IPEndPoint localep = new IPEndPoint(IPAddress.Any, UDP_TST_PORT);
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
            NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface netif in nics)
            {
                IPInterfaceProperties ipProps = netif.GetIPProperties();
                foreach (UnicastIPAddressInformation ipAddr in ipProps.UnicastAddresses)
                {
                    if (!IPAddress.IsLoopback(ipAddr.Address) && ipAddr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        IPAddress broadcast =  GetBroadCastIP( ipAddr.Address,  ipAddr.IPv4Mask);
                        Console.WriteLine(ipAddr.IPv4Mask == null ? "No subnet defined" : "IPv4Address: " + ipAddr.Address + "\nIpv4 Mask: " + ipAddr.IPv4Mask.ToString() + "\nBroadcast: " + broadcast);
                        IPEndPoint ep = new IPEndPoint(broadcast, UDP_DIAG_PORT);
                        sock.SendTo(helloZGW, ep);
                    }
                }
            }
            //Console.WriteLine("Message sent to the broadcast address");
            //MessageBox.Show("Message sent", "My Application",
            //MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);
        }

        [STAThread]
         static void Main()
        {
            //Application.EnableVisualStyles();
            //Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new Form1());
            Socket sock = getSocket();
            pingZGW(sock);

            byte[] data = new byte[256];
            IPEndPoint sender = new IPEndPoint(IPAddress.Any, UDP_TST_PORT);
            EndPoint tmpRemote = (EndPoint)(sender);
            Console.WriteLine("Waiting to receive datagrams from client...");
            /*sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 3000);
            try
            {
                sock.ReceiveTimeout = 3000;
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket timeout!", ex.ToString());
            }
            */
            int len = sock.ReceiveFrom(data, data.Length, SocketFlags.None, ref tmpRemote);
            // tstdATA = "   2 DIAGADR10BMWMAC001000024C15BMWVINWBS3R9C54DEADBEEF"
            string ZGW_reply = string.Join(string.Empty, Encoding.ASCII.GetString(data, 0, len).Skip(6));
            Console.WriteLine("Response is:{0}", ZGW_reply);

            string pattern = @"(.*)BMWMAC(.*)BMWVIN(.*)";
            foreach (Match match in Regex.Matches(ZGW_reply, pattern, RegexOptions.IgnoreCase))
            {
                Console.WriteLine("DiagAddr: {0}\n ZgwMAC: {1}\n ZgwVIN: {2}", match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            } 

            //MessageBox.Show("Message Received" + data, "My Application",
            //MessageBoxButtons.OKCancel, MessageBoxIcon.Asterisk);
            sock.Close();         
        }
    }
}
