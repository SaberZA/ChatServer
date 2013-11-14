using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using System.Net.Sockets;
using System.Net;
using System.Threading;

namespace AdvChatServer
{
    class Program
    {
        private static int portNo = 500;
        private static string localIP;
        //public static Hashtable AllClients = new Hashtable();
        //public static ChatClient user { get; private set; }


        static void Main(string[] args)
        {
            grabLocalInfo();

            IPAddress localAdd = IPAddress.Parse(localIP);
            TcpListener listener = new TcpListener(localAdd,portNo);
            listener.Start();
            Console.WriteLine("Server started @ "+DateTime.Now);

            while(true)
            {
                ChatClient user = new ChatClient(listener.AcceptTcpClient());
                
                //AllClients.Add(user.RemoteEndPoint.ToString(), user);
                //user.HasFinishedReceivingMessage += new ChatClient.FinishReceivingDataHandler(user_HasFinishedReceivingMessage);
                //user.start(temp);
                
            }
        }




        private static void grabLocalInfo()
        {
            //grab ipv4 directly
            IPHostEntry hostEntry = null;
            hostEntry = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            for (int i = 0; i < hostEntry.AddressList.Length; i++)
            {
                if (hostEntry.AddressList[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    // found IPv4 address
                    IPAddress ipAddress = new IPAddress(hostEntry.AddressList[i].Address);
                    localIP = ipAddress.ToString();
                    break;
                }
            }

            //get port number for listening
            int tempPort;
            do
            {
                //Console.Write("Please enter listening port: ");
                //Int32.TryParse(Console.ReadLine(), out tempPort);
                tempPort = 500;
            } while (tempPort == 0 || tempPort == 80 || tempPort == 8080);
            portNo = tempPort;
        }
    }
}
