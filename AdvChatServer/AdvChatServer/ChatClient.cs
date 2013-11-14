using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Numerics;
using System.Text;

using System.Net.Sockets;
using System.Collections;

namespace AdvChatServer
{
    class ChatClient
    {

        //constant for linefeed character
        private const int LF = 10;

        //contains list of all the clients
        public static Hashtable AllClients = new Hashtable();

        //information about the client
        private TcpClient _client;
        private string _clientIP;
        private string _clientNick;

        //used to store partial request
        private string partialStr;
        

        //used for sending/receiving data
        private byte[] data;

        public ChatClient(TcpClient client)
        {
            _client = client;

            //get client IP address
            _clientIP = client.Client.RemoteEndPoint.ToString();

            //add current client to hastable
            AllClients.Add(_clientIP, this);

            //start reading data from the client in a separate thread
            data = new byte[_client.ReceiveBufferSize];
            _client.GetStream().BeginRead(data, 0, Convert.ToInt32(_client.ReceiveBufferSize), ReceiveMessage, null);
        }
        
        public void SendMessage(string message)
        {
            try
            {
                //send the text
                NetworkStream ns;

                lock (_client.GetStream())
                {
                    ns = _client.GetStream();
                    byte[] bytesToSend = System.Text.Encoding.ASCII.GetBytes(message);
                    ns.Write(bytesToSend, 0, bytesToSend.Length);
                    ns.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Broadcast(string message, string[] users)
        {
            if (users == null)
            {
                //broadcasting to everyone
                foreach (DictionaryEntry c in AllClients)
                {
                    ((ChatClient)c.Value).SendMessage(message + "\n");
                }
            }
            else
            {
                //broadcasting to selected users
                foreach (DictionaryEntry c in AllClients)
                {
                    foreach (string user in users)
                    {
                        if (((ChatClient)(c.Value))._clientNick == user)
                        {
                            ((ChatClient)(c.Value)).SendMessage(message + "\n");
                            //log it locally
                            Console.WriteLine("sending -----> " + message);
                            break;
                        }
                    }
                }
            }
        }

        





        

        

        
        // receiving a message from the client
        public void ReceiveMessage(IAsyncResult ar)
        {
            //read  from client
            int bytesRead;
            try
            {
                lock(_client.GetStream())
                {
                    bytesRead = _client.GetStream().EndRead(ar);
                }

                //client has disconnected
                if(bytesRead < 1)
                {
                    AllClients.Remove(_clientIP);
                    Broadcast("[Left]["+_clientNick+"] has left the chat.", null);
                    //Console.WriteLine(_clientNick+" has disconnected from chat.");
                    return;
                }
                else
                {
                    string messageReceived;
                    int i = 0;
                    int start = 0;

                    //loop until no more chars
                    while(data[i] != 0)
                    {
                        //do not scan more than what is read
                        if (i + 1 > bytesRead)
                            break;

                        if(data[i] == LF)
                        {
                            messageReceived = partialStr + System.Text.Encoding.ASCII.GetString(data, start, i - start);
                            Console.WriteLine("received <----- "+messageReceived);
                            if (messageReceived.StartsWith("[Join]"))
                            {
                                //====client is sending its
                                // nickname====
                                //---e.g. [Join][User1]---
                                //---extract user's name---
                                int nameLength = messageReceived.IndexOf("]", 6);
                                _clientNick = messageReceived.Substring(7, nameLength - 7);
                                //---tell everyone client has entered
                                // the chat---
                                Broadcast(messageReceived, null);
                            }
                            else if (messageReceived.StartsWith("[Usrs]"))
                            {
                                //client is requesting all usernames
                                //e.g. [Usrs]

                                //get all the users
                                string allUsers = "[Usrs][";
                                foreach (DictionaryEntry c in AllClients)
                                {
                                    //get all the users' name
                                    allUsers += ((ChatClient)(c.Value))._clientNick + ",";
                                }
                                allUsers += "]";

                                //e.g. [Usrs][User1,User2,etc]
                                Broadcast(allUsers, null);
                            }
                            else if (messageReceived.StartsWith("[Usrs]"))
                            {
                                //client is requesting all usernames
                                //e.g. [Usrs]

                                //get all the users
                                string allUsers = "[Usrs][";
                                foreach (DictionaryEntry c in AllClients)
                                {
                                    //get all the users' name
                                    allUsers += ((ChatClient)(c.Value))._clientNick + ",";
                                }
                                allUsers += "]";

                                //e.g. [Usrs][User1,User2,etc]
                                Broadcast(allUsers, null);
                            }
                            else if(messageReceived.StartsWith("[Talk]"))
                            {
                                //Chatting with someone
                                //[Talk][User2,User3]User1>Hello everyone!

                                //get all users
                                string[] users =
                                    messageReceived.Substring(7, messageReceived.IndexOf("]", 7) - 8).Split(',');

                                //send to specified users
                                Broadcast(messageReceived, users);
                            }
                            else if (messageReceived.StartsWith("[Left]"))
                            {
                                //client is requesting all usernames
                                //e.g. [Usrs]

                                //get all the users
                                string allUsers = "[Left][";
                                foreach (DictionaryEntry c in AllClients)
                                {
                                    //get all the users' name
                                    allUsers += ((ChatClient)(c.Value))._clientNick + ",";
                                }
                                allUsers += "]";

                                //e.g. [Usrs][User1,User2,etc]
                                //Broadcast(allUsers, null);

                                AllClients.Remove(_clientIP);
                                Broadcast("[Left][" + _clientNick + "] has left the chat.", null);
                                Console.WriteLine(_clientNick + " has disconnected from chat.");
                                return;
                            }
                            else if(messageReceived.StartsWith("[File]"))
                            {
                                //FTP Request
                                //e.g. [File][User1,User2][FileName.txt]

                                //get all users
                                string[] users =
                                    messageReceived.Substring(7, messageReceived.IndexOf("]", 7) - 8).Split(',');

                                int index = messageReceived.IndexOf("]", 7) + 2;

                                string fileName = messageReceived.Substring(index, messageReceived.Length - index - 1);

                                //see who initiated the request
                                string from = users[0];

                                //remove the first user (initiator)
                                for(int j=1; j<= users.Length - 1; j++)
                                {
                                    users[j - 1] = users[j];
                                }
                                users[users.Length - 1] = string.Empty;

                                //send to user
                                Broadcast("[File]["+ from +"]["+fileName+ "]",users);
                            }
                            else if(messageReceived.StartsWith("[Send_File]"))
                            {
                                //send file via FTP
                                //e.g. [Send_File][User1,User2]

                                //check send to who
                                string[] users =
                                    messageReceived.Substring(12, messageReceived.IndexOf("]", 12) - 12).Split(',');
                                string RecipientIP = string.Empty;

                                //find out the recipients IP address
                                foreach (DictionaryEntry c in AllClients)
                                {
                                    if(((ChatClient)(c.Value))._clientNick == users[1])
                                    {
                                        //send message to user
                                        RecipientIP = ((ChatClient) (c.Value))._clientIP.Substring(0,
                                                                                                   _clientIP.IndexOf(":"));
                                        break;
                                    }
                                }

                                users[1] = string.Empty;
                                //e.g. [Send_File][1.2.3.4]
                                Broadcast("[Send_File]["+RecipientIP + "]", users);
                            }

                            if(data[i] == 0 || i >= data.Length - 1)
                            {
                                Console.WriteLine("***HIT END OF PACKET***");
                                break;
                            }

                            start = i + 1;
                        }
                        i += 1;

                        if(i >= data.Length)
                        {
                            Console.WriteLine("***HIT END OF PACKET***");
                            break;
                        }
                    }

                    //partial string
                    if(start != i)
                    {
                        partialStr = System.Text.Encoding.ASCII.GetString(data, start, i - start);
                    }
                }

                //continue reading from client
                lock (_client.GetStream())
                {
                    _client.GetStream().BeginRead(data, 0, Convert.ToInt32(_client.ReceiveBufferSize), ReceiveMessage,
                                                  null);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString()+"\n");
                AllClients.Remove(_clientIP);
                _client.GetStream().Close();
                _client.Close();
                Broadcast("[Left]["+ _clientNick + "] has left the chat.",null);
            }
        }
    }
}
