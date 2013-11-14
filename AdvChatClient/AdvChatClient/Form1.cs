using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using System.Net.Sockets;
using System.IO;
using Microsoft.Win32.SafeHandles;


namespace AdvChatClient
{
    public partial class Form1 : Form
    {
        //get own IP address
        private System.Net.IPHostEntry ips = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());

        //port numbers and server IP address
        private int PORTNO = 500;
        private int FTPPORTNO = 501;
        private string SERVERIP = "10.10.7.142";
        private const long PERCENT100 = 100;
        private TcpClient client;

        //used for sending and receiving data\
        private byte[] data;

        //for FTP use
        private System.IO.FileStream fs;
        private string filename;
        private string fullfilename;

        //used to store partial request
        private string partialStr;
        private byte[] partialFile;
        

        //header flag
        private bool readHeader;
        private long messageSize;
        private long totalBytes;
        private byte[] completeFile;

        public Form1()
        {
            InitializeComponent();
        }

        private string localIP = "";

        private void btnSignIn_Click(object sender, EventArgs e)
        {
            //grab ipv4 directly
            //IPHostEntry hostEntry = null;
            //hostEntry = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
            //for (int i = 0; i < hostEntry.AddressList.Length; i++) {
            //  if (hostEntry.AddressList[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
            //      // found IPv4 address
            //      IPAddress ipAddress = new IPAddress(hostEntry.AddressList[i].Address);
            //      localIP = ipAddress.ToString();
            //      break;
            //  }
            //}

            

            if (btnSignIn.Text == "Sign In")
            {
                //---Sign in to the server---
                try
                {
                    IPAddress tempOut;
                    if (IPAddress.TryParse(txtServerIp.Text, out tempOut) != null)
                    {
                        SERVERIP = txtServerIp.Text;
                    }

                    Int32 tempIntOut;
                    if (Int32.TryParse(txtServerPort.Text, out tempIntOut) != null)
                    {
                        PORTNO = Convert.ToInt32(txtServerPort.Text);
                        FTPPORTNO = PORTNO + 1;
                    }

                    client = new TcpClient();
                    //---connect to the server---
                    client.Connect(SERVERIP, PORTNO);
                    data = new byte[client.ReceiveBufferSize];
                    
                    //---inform the server of your nickname---
                    SendMessage("[Join][" + txtNick.Text + "]");
                    //---begin reading data asynchronously from the
                    // server---
                    client.GetStream().BeginRead(
                    data, 0, System.Convert.ToInt32(
                    client.ReceiveBufferSize), ReceiveMessage,
                    null);
                    //'---change the button and textbox---
                    btnSignIn.Text = "Sign Out";
                    btnSend.Enabled = true;
                    txtNick.Enabled = false;
                    
                    //---get all users connected---
                    SendMessage("[Usrs]");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
            else
            {
                //---Sign off from the server---
                SendMessage("[Left][" + txtNick.Text + "]");
                Disconnect();
                
                lstUsers.Items.Clear();
                //---change the button and textbox---
                btnSignIn.Text = "Sign In";
                btnSend.Enabled = false;
                txtNick.Enabled = true;
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            //e.g. [Talk][User2,User3,etc]User1>Hello World!

            //select users to chat
            if (lstUsers.SelectedItems.Count < 1)
            {
                MessageBox.Show("You must select at least one person to chat with");
                return;
            }

            //formulate message
            string message = "[Talk][";

            if (lstUsers.SelectedItems[0] == "All Users")
            {
                List<string> allUsers = new List<string>();
                foreach (var item in lstUsers.Items)
                {
                    if ((string) item != "All Users" && (string) item != txtNick.Text)
                    {
                        allUsers.Add(item.ToString());
                    }
                }

                if (allUsers.Count < 1)
                {
                    allUsers.Add(txtNick.Text);
                }

                foreach (string user in allUsers)
                {
                    message += user + ",";
                }
            }
            else
            {
                //check who to chat with
                foreach (object user in lstUsers.SelectedItems)
                {
                    message += user + ",";
                }
            }
            message += "]" + txtNick.Text + ">" + txtMessage.Text;

            //update the message history
            string currentBlock = txtMessageHistory.Text;
            txtMessageHistory.Text = currentBlock + txtNick.Text + ">" + txtMessage.Text + Environment.NewLine;

            //send message
            SendMessage(message);
            txtMessage.Clear();
            

            
        }

        public void SendMessage(string message)
        {
            //adds a linefeed char

            message += "\n";
            try
            {
                //sends the text
                System.Net.Sockets.NetworkStream ns;
                lock (client.GetStream())
                {
                    ns = client.GetStream();
                    byte[] bytesToSend = System.Text.Encoding.ASCII.GetBytes(message);
                    //sends the text
                    ns.Write(bytesToSend, 0, bytesToSend.Length);
                    ns.Flush();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public void ReceiveMessage(IAsyncResult ar)
        {
            try
            {
                int bytesRead;

                bytesRead = client.GetStream().EndRead(ar);
                if (bytesRead < 1)
                    return;
                else
                {
                    string messageReceived;
                    int i = 0;
                    int start = 0;
                    //loop until no more chars
                    while (data[i] != 0)
                    {
                        //do not scan more than what is read
                        if (i + 1 > bytesRead)
                        {
                            break;
                        }

                        //if LF is detected
                        if (data[i] == 10)
                        {
                            messageReceived = partialStr + System.Text.Encoding.ASCII.GetString(data, start, i - start) +
                                              Environment.NewLine;

                            //update the messageHistory
                            object[] para = { messageReceived };
                            this.Invoke(new delUpdateHistory(this.UpdateHistory), para);
                            start = i + 1;
                        }
                        i += 1;
                        if (i >= data.Length)
                        {
                            Console.WriteLine("***HIT END OF PACKET***");
                            break;
                        }
                    }

                    //partial request
                    if (start != i)
                    {
                        partialStr = System.Text.Encoding.ASCII.GetString(data, start, i - start);
                        //txtMessageHistory.AppendText("***WRITING PARTIAL***\n");
                    }
                    //continue reading for more data
                    client.GetStream().BeginRead(data, 0, Convert.ToInt32(client.ReceiveBufferSize), ReceiveMessage, null);
                }

                

            }
            catch (Exception ex)
            {
                if(displayDisconnectionError)
                    MessageBox.Show(ex.ToString());
            }
        }

        public delegate void delUpdateHistory(string str);

        public void UpdateHistory(string str)
        {
            if(str.StartsWith("[Join]"))
            {
                //e.g. [Join][User1]
                //extract user's name
                int nameLength = str.IndexOf("]", 6);

                //display in the listBox
                lstUsers.Items.Add(str.Substring(7, nameLength - 7));
                return;
            }
            else if(str.StartsWith("[Left]"))
            {
                //e.g. [Left][User1]
                //extract user's name
                int nameLength = str.IndexOf("]", 6);
                try
                {
                    //remove the user from the listbox
                    lstUsers.Items.RemoveAt(lstUsers.Items.IndexOf(str.Substring(7,nameLength - 7)));
                    txtMessageHistory.AppendText("User[" + str.Substring(7, nameLength - 7) + "] has left the chat." + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    
                }
                return;
            }
            else if(str.StartsWith("[Usrs]"))
            {
                //e.g. [Usrs][User1,User2,etc]
                //extract the usernames
                string[] users = str.Substring(7, str.Length - 8).Split(',');
                lstUsers.Items.Clear();
                lstUsers.Items.Add("All Users");
                //add the users to the ListBox
                foreach (var user in users)
                {
                    lstUsers.Items.Add(user);
                }
                lstUsers.SelectedIndex = 0;
                //remove the last empty user
                lstUsers.Items.RemoveAt(lstUsers.Items.Count - 1);
                return;
            }
            else if(str.StartsWith("[File]"))
            {
                //e.g. [File][User1][Filename.ext]
                //get username
                string[] users = str.Substring(7, str.IndexOf("]", 7) - 7).Split(',');

                //extract the filename
                int index = str.IndexOf("]", 7) + 2;
                this.filename = str.Substring(index, str.Length - index - 3);

                //prompt the user
                DialogResult response;
                response = MessageBox.Show("Do you want to download the file " + filename, "Download",
                                           MessageBoxButtons.YesNo);

                //proceed with download
                if(response == DialogResult.Yes)
                {
                    startFTPListener();

                    //tell the client that he can proceed to send the file
                    //e.g. [Send_File][User1, User2]
                    SendMessage("[Send_File]["+users[0] + ","+txtNick.Text + "]");

                    //start the FTP process
                    //FTP_Receive(filename);
                    readHeader = false;
                    ReceiveFTP();
                    //this.Invoke(new delReceiveFTP(this.ReceiveFTP));
                }
                return;
            }
            else if(str.StartsWith("[Send_File]"))
            {
                //e.g. [Send_File][1.2.3.4]
                //extract the IP address of the file recipient
                string userIP = str.Substring(12, str.Length - 15);

                //start the FTP process
                //FTP_Send(fullfilename, userIP);

                SendFTP(fullfilename,userIP);
                return;
            }
            else if(str.StartsWith("[Talk]"))
            {
                //e.g. [Talk][User1]Hello!
                //display the message in the textbox
                str = str.Substring(str.IndexOf("]", 7) + 1);
                txtMessageHistory.AppendText(str);
            }

        }

        private void btnFTP_Click(object sender, EventArgs e)
        {
            //formulate the message
            //e.g. [File][User1,User2,User3,][Filename.ext]
            string message = "[File][" + txtNick.Text + ",";

            if (lstUsers.SelectedItems.Count < 1)
            {
                MessageBox.Show("You must select at least one person to send to");
                return;
            }

            //check who to send to
            foreach (object user in lstUsers.SelectedItems)
            {
                message += user + ",";
            }

            //select the file to send
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = "C:\\";
            openFileDialog.Filter = "txt files (*.txt) |*.txt|All files (*.*)|*.*";
            openFileDialog.FilterIndex = 2;
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                fullfilename = openFileDialog.FileName;
                filename = fullfilename.Substring(fullfilename.LastIndexOf("\\") + 1);
                message += "][" + filename + "]";
                SendMessage(message);
            }

        }

        public void FTP_Send(string filename, string recipientIP)
        {
            //connect to the recipient
            System.Net.Sockets.TcpClient tcpClient = new TcpClient();

            tcpClient.Connect(recipientIP, FTPPORTNO);
            int bufferSize = tcpClient.ReceiveBufferSize;
            NetworkStream nws = tcpClient.GetStream();

            //---open the file---
            FileStream fs;
            fs = new FileStream(filename, FileMode.Open,
            FileAccess.Read);
            byte[] bytesToSend = new byte[fs.Length];
            int numBytesRead = fs.Read(bytesToSend, 0,
            bytesToSend.Length);
            int totalBytes = 0;

            for (int i = 0; i <= fs.Length / bufferSize; i++)
            {
                //---send the file---
                if (fs.Length - (i * bufferSize) > bufferSize)
                {
                    nws.Write(bytesToSend, i * bufferSize,
                    bufferSize);
                    nws.Flush();
                    totalBytes += bufferSize;
                }
                else
                {
                    nws.Write(bytesToSend, i * bufferSize,
                    (int)fs.Length - (i * bufferSize));
                    nws.Flush();
                    totalBytes += (int)fs.Length - (i * bufferSize);
                }



                //---update the status label---
                //toolStripStatusLabel1.Text = "Sending " + totalBytes +
                //" bytes....";
                //Application.DoEvents();
            }
            //int i = 0;

            //do
            //{

                //---send the file---
                nws.Write(bytesToSend, 0, numBytesRead);
                totalBytes += numBytesRead;
             



                //    //---update the status label---
               // toolStripStatusLabel1.Text = "Sending " + totalBytes +
                //" bytes....";
                //Application.DoEvents();
                //i+= bufferSize;
            //} while (i< numBytesRead);

            toolStripStatusLabel1.Text = "Sending " + totalBytes + " bytes....Done.";
            fs.Close();
            tcpClient.Close();
        }

        public void FTP_Receive(string filename)
        {
            try
            {
                //---get the local IP address---
                System.Net.IPAddress localAdd =
                System.Net.IPAddress.Parse(
                ips.AddressList[0].ToString());
                //---start listening for incoming connection---
                System.Net.Sockets.TcpListener listener = new
                System.Net.Sockets.TcpListener(localAdd,
                FTPPORTNO);
                listener.Start();
                //---read incoming stream---
                TcpClient tcpClient = listener.AcceptTcpClient();
                NetworkStream nws = tcpClient.GetStream();
                //---delete the file if it exists---
                if (File.Exists("c:\\temp\\" + filename))
                {
                    File.Delete("c:\\temp\\" + filename);
                }
                //---create the file---
                fs = new System.IO.FileStream("c:\\temp\\" + filename,
                FileMode.Append, FileAccess.Write);
                int counter = 0;
                int totalBytes = 0;
                //string buffer ="";
                byte[] buffer = new byte[8192];
                int bufferSize = tcpClient.ReceiveBufferSize;
                
                do
                {
                    //---read the incoming data---
                    txtMessageHistory.AppendText("Debug: " + tcpClient.ReceiveBufferSize + "\n");
                    int bytesRead = nws.Read(buffer, 0, tcpClient.ReceiveBufferSize);
                    
                    totalBytes += bytesRead;
                    fs.Write(data, 0, bytesRead);
                    //---update the status label---
                    toolStripStatusLabel1.Text = "Receiving " +
                    totalBytes + " bytes....";
                    Application.DoEvents();
                    counter += 1;
                } while (nws.DataAvailable);
                toolStripStatusLabel1.Text = "Receiving " + totalBytes
                + " bytes....Done.";
                fs.Close();
                tcpClient.Close();
                listener.Stop();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public void SendFTP(string filename, string recipientIP)
        {
            TcpClient tcpClient = null;
            FileStream fs = null;
            NetworkStream nws = null;
            try
            {
                //add send method here
                //connect to the recipient
                tcpClient = new TcpClient();
                tcpClient.Connect(recipientIP, FTPPORTNO);
                //int bufferSize = tcpClient.ReceiveBufferSize;
                nws = tcpClient.GetStream();

                //---open the file---
                //FileStream fs;
                fs = new FileStream(filename, FileMode.Open,
                FileAccess.Read);

                
                byte[] bytesToSend = new byte[fs.Length];
                int numBytesRead = fs.Read(bytesToSend, 0, bytesToSend.Length);
                int totalBytes = 0;

                /*************HEADER******************/
                byte[] messageType = System.Text.Encoding.ASCII.GetBytes("FILE");
                byte[] messageSize = System.Text.Encoding.ASCII.GetBytes(Convert.ToString(numBytesRead));
                /*************************************/

                //pad messageSize allocation in full message
                byte[] withPaddedSize = checkPadding(messageSize);
                
                byte[] completeWithHeader = new byte[4+8+fs.Length];
                System.Array.Copy(messageType,0,completeWithHeader,0, 4);
                System.Array.Copy(withPaddedSize, 0, completeWithHeader, 4, 8);
                System.Array.Copy(bytesToSend, 0, completeWithHeader, 12, bytesToSend.Length);


                nws.Write(completeWithHeader, 0, completeWithHeader.Length);
                toolStripProgressBar1.Minimum = 0;
                toolStripProgressBar1.Maximum = numBytesRead;
                toolStripProgressBar1.Value = 0;

                toolStripStatusLabel1.Text = "Sending " + numBytesRead + " bytes....Done.";
                toolStripProgressBar1.Value = numBytesRead;
                fs.Close();
                tcpClient.Close();


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString() +"_InSendFTP");
                if(fs != null)
                    fs.Close();
                if(tcpClient != null)
                    tcpClient.Close();
            }
        }

        private byte[] checkPadding(byte[] messageSize)
        {
            byte[] temp;
            int messageSizeByteLength = messageSize.Length;
            if ((8 - messageSizeByteLength) > 0)
            {
                temp = new byte[8];
                int padAmount = 8 - messageSizeByteLength;
                for (int i = 0; i < padAmount; i++)
                {
                    temp[i] = 48;//ASCII '0'
                    //temp[i] = 0;//ASCII 'NUL'
                }
                System.Array.Copy(messageSize, 0, temp, padAmount, messageSizeByteLength);
            }
            else
            {
                temp = messageSize; 
            }

            return temp;
        }

        private TcpListener listenerFtp;

        private void startFTPListener()
        {
            try
            {
                //the use of IPHostEntry may contain ipv6 and ipv4 addresses the correct one must be determined
                //we want the ipv4 address
                //---get the local IP address---
                System.Net.IPAddress localAdd = null;
                foreach (var ipAddress in ips.AddressList)
                {
                    if(ipAddress.AddressFamily == AddressFamily.InterNetwork)
                    {
                        localAdd = ipAddress;
                    }
                }
                
                if(localAdd == null)
                {
                    MessageBox.Show("Local IPV4 address could not be determined.");
                    return;
                }

                //---start listening for incoming connection---
                listenerFtp = new
                    System.Net.Sockets.TcpListener(localAdd,
                                                   FTPPORTNO);
                listenerFtp.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public delegate void delReceiveFTP();

        public void ReceiveFTP()
        {
            DateTime timeTaken = DateTime.Now;
            TcpClient tcpClient = null;
            System.Net.Sockets.TcpListener listener = null;
             try
             {
                 //---read incoming stream---
                 tcpClient = listenerFtp.AcceptTcpClient();
                 NetworkStream nws = tcpClient.GetStream();
                 
                 int counter = 0;
                 int totalBytes = 0;
                 byte[] buffer = new byte[8192];
                 int bufferSize = tcpClient.ReceiveBufferSize;
                 if (!Directory.Exists("c:\\Temp\\"))
                 {
                     MessageBox.Show("Directory does not exist");
                     return;
                 }
                 //---delete the file if it exists---
                 if (File.Exists("c:\\Temp\\" + filename))
                 {
                     txtMessageHistory.AppendText("File Exists and will be replaced."+Environment.NewLine);
                     File.Delete("c:\\Temp\\" + filename);
                 }
                 //---create the file---
                 fs = new System.IO.FileStream("c:\\Temp\\" + filename,
                                               FileMode.Append, FileAccess.Write);

                 int readCounter = 0;
                 toolStripStatusLabel1.Text = "Status";
                 toolStripProgressBar1.Visible = true;
                 
                 do
                 {
                     
                     int bytesRead = nws.Read(data, 0, client.ReceiveBufferSize);
                     if (bytesRead >= 1)
                     {
                         AsyncReceiveFTP(bytesRead);
                     }
                     else
                     {
                         break;
                     }
                     readCounter += bytesRead;
                 } while (readCounter < messageSize);
                 if (completeFile == null)
                 {
                     MessageBox.Show("Output file is null");
                 }
                 else
                 {
                     long bytesPerKByte = 1024;
                     long totalB;
                     if(File.Exists("c:\\Test\\" + filename))
                     {
                         FileInfo fileInfo = new FileInfo("c:\\Test\\" + filename);
                         totalB = fileInfo.Length;
                         txtMessageHistory.AppendText("TotalKB of File: " + totalB + Environment.NewLine);
                     }
                     else
                     {
                         totalB = totalBytes;
                     }
                     
                     TimeSpan ts = DateTime.Now - timeTaken;
                     toolStripProgressBar1.Visible = false;
                     toolStripStatusLabel1.Text = "File transfer complete. Size: " + messageSize +
                                                      " Bytes. Time taken: " + ts +
                                                      " seconds";
                 }
                 
                 fs.Close();
                 tcpClient.Close();
                 listenerFtp.Stop();
             }
             catch (Exception ex)
             {
                //if(displayDisconnectionError)
                 MessageBox.Show(ex.ToString() + " in receiveFTP()"); 
                 if(fs != null)
                    fs.Close();
                 if(tcpClient != null)
                    tcpClient.Close();
                 if (listenerFtp != null)
                     listenerFtp.Stop();
             }
        }

        public delegate void delAsyncReceiveFTP(int bytesRead);

        public void AsyncReceiveFTP(int bytesRead)
        {
            try
            {
                if (bytesRead < 1)
                {
                    txtMessageHistory.AppendText("***0 bytes read***"+Environment.NewLine);
                    return;
                }
                else
                {
                    byte[] fileReceived;
                    int i = 12;
                    int start = 12;
                    int partSize = 0;
                    if (partialFile != null)
                    {
                        partSize = partialFile.Length;
                    }

                    if (!readHeader)
                    {
                        string messageType = System.Text.Encoding.ASCII.GetString(data, 0, 4);

                        if (messageType != "FILE")
                        {
                            MessageBox.Show("Data Corrupt - File Type = " + messageType, "Error");
                            return;
                        }
                        else
                        {
                            messageSize = Convert.ToInt64(System.Text.Encoding.ASCII.GetString(data, 4, 8));
                            //txtMessageHistory.AppendText("***MessageSize: "+messageSize+Environment.NewLine);
                            //txtMessageHistory.AppendText("***MessageSize in Bytes: "+data[4]+" "
                            //    + data[5] + " "
                            //    + data[6] + " "
                            //    + data[7] + " "
                            //    + data[8] + " "
                            //    + data[9] + " "
                            //    + data[10] + " "
                            //    + data[11] + " "
                            //    + messageSize + Environment.NewLine);

                            toolStripProgressBar1.Minimum = 0;
                            toolStripProgressBar1.Maximum = 100;

                            readHeader = true;

                            //update partialFile size
                            byte[] temp = new byte[partSize + bytesRead - 12];

                            if (partSize != 0 && partialFile != null)
                                System.Array.Copy(partialFile, 0, temp, 0, partSize);
                            System.Array.Copy(data, 12, temp, partSize, bytesRead - 12);

                            //update the messageHistory
                            completeFile = new byte[temp.Length];
                            int byteCounter = 0;
                            foreach (var b in temp)
                            {
                                completeFile[byteCounter] = b;
                                byteCounter++;
                            }
                            totalBytes += bytesRead - 12;
                            
                            double progress = ((double) totalBytes/messageSize);
                            int progressPercent = Convert.ToInt32(progress * 100.00);
                            if (progressPercent > 100)
                                progressPercent = 100;
                            toolStripProgressBar1.Value = progressPercent;
                            writeCompleteFile();
                            
                        }
                    }
                    else
                    {
                        //update partialFile size
                        byte[] temp = new byte[partSize + bytesRead];

                        if (partSize != 0 && partialFile != null)
                            System.Array.Copy(partialFile, 0, temp, 0, partSize);
                        System.Array.Copy(data, 0, temp, partSize, bytesRead);

                        //update the messageHistory
                        completeFile = new byte[temp.Length];
                        int byteCounter = 0;
                        foreach (var b in temp)
                        {
                            completeFile[byteCounter] = b;
                            byteCounter++;
                        }
                        totalBytes += bytesRead;
                        
                        double progress = ((double)totalBytes / messageSize);
                        int progressPercent = Convert.ToInt32(progress*100.00);
                        if (progressPercent > 100)
                            progressPercent = 100;
                        toolStripProgressBar1.Value = progressPercent;

                        writeCompleteFile();
                    }
                    
                    #region old capture method
                    //loop until maximum bytes
                    /*while (totalBytes < bytesRead)
                    {
                        //if (totalBytes == messageSize)
                        //{
                        //txtMessageHistory.AppendText("***Reached MessageSize*** " + messageSize + "\n");

                        ////update partialFile size
                        //byte[] temp = new byte[partSize + (i - start)];

                        //if(partSize != 0 && partialFile != null)
                        //    System.Array.Copy(partialFile, 0, temp, 0, partSize);
                        //System.Array.Copy(data, start, temp, partSize, i - start);

                        ////update the messageHistory
                        //completeFile = new byte[temp.Length];
                        //int byteCounter = 0;
                        //foreach (var b in temp)
                        //{
                        //    completeFile[byteCounter] = b;
                        //    byteCounter++;
                        //}
                        //totalBytes++;
                        //start = i + 1;
                        //}
                        if (!readHeader)
                        {
                            if (totalBytes == bytesRead)
                            {
                                txtMessageHistory.AppendText("***Reached bytesRead - 12*** " + bytesRead + "\n");

                                //update partialFile size
                                byte[] temp = new byte[partSize + (i - start)];

                                if (partSize != 0 && partialFile != null)
                                    System.Array.Copy(partialFile, 0, temp, 0, partSize);
                                System.Array.Copy(data, start, temp, partSize, i - start);

                                //update the messageHistory
                                completeFile = new byte[temp.Length];
                                int byteCounter = 0;
                                foreach (var b in temp)
                                {
                                    completeFile[byteCounter] = b;
                                    byteCounter++;
                                }
                                totalBytes++;
                                //start = i + 1;
                            }
                            else
                            {
                                totalBytes++;
                                toolStripStatusLabel1.Text = "Receiving " + totalBytes + " bytes...";
                                Thread.Sleep(5);

                            }
                            toolStripStatusLabel1.Text = "Receiving " + totalBytes + " bytes...";
                            i += 1;
                            if (i >= data.Length)
                            {
                                Console.WriteLine("***HIT END OF PACKET***");
                                break;
                            }
                        }
                        else
                        {
                            if (totalBytes == bytesRead - 12)
                            {
                                txtMessageHistory.AppendText("***Reached bytesRead - 12*** " + bytesRead + "\n");

                                //update partialFile size
                                byte[] temp = new byte[partSize + (i - start)];

                                if (partSize != 0 && partialFile != null)
                                    System.Array.Copy(partialFile, 0, temp, 0, partSize);
                                System.Array.Copy(data, start, temp, partSize, i - start);

                                //update the messageHistory
                                completeFile = new byte[temp.Length];
                                int byteCounter = 0;
                                foreach (var b in temp)
                                {
                                    completeFile[byteCounter] = b;
                                    byteCounter++;
                                }
                                totalBytes++;
                                //start = i + 1;
                            }
                            else
                            {
                                totalBytes++;
                                //toolStripStatusLabel1.Text = "Receiving " + totalBytes + " bytes...";

                            }
                            toolStripStatusLabel1.Text = "Receiving " + totalBytes + " bytes...";
                            i += 1;
                            if (i >= data.Length)
                            {
                                Console.WriteLine("***HIT END OF PACKET***");
                                break;
                            }
                        }
                    }

                    txtMessageHistory.AppendText("***Totalbytes= " + totalBytes + "\n");

                    if (start != i)
                    {

                        partialFile = new byte[data.Length];
                        if (readHeader)
                            System.Array.Copy(data, 0, partialFile, 0, data.Length);
                        else
                        {
                            System.Array.Copy(data, 4, partialFile, 0, data.Length - 4);
                        }
                        txtMessageHistory.AppendText("***Partial File*** " + partialFile.Length + ":: Totalbytes= " + totalBytes + "\n");
                    }*/
#endregion

                }
                
            }
            catch (Exception ex)
            {
                if (displayDisconnectionError)
                {
                    if ((MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.YesNo) == DialogResult.Yes))
                    {
                        displayDisconnectionError = false;
                    }
                }

            }
        }
        
        private bool displayDisconnectionError = true;

        public delegate void delCompleteFile();

        private bool canDelete = true;

        public void writeCompleteFile()
        {
            if(completeFile != null)
            {
                try
                {
                    fs.Write(completeFile, 0, completeFile.Length);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString()+" in writeCompleteFile()");
                }
                
            }
            else
            {
                MessageBox.Show("File is null");
            }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Disconnect();
        }

        

        public void Disconnect()
        {
            try
            {
                displayDisconnectionError = false;
                
                client.GetStream().Close();
                client.Close();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.ToString());
            }

        }

        private void txtMessageHistory_TextChanged(object sender, EventArgs e)
        {
            txtMessageHistory.SelectionStart = txtMessageHistory.Text.Length;
            txtMessageHistory.ScrollToCaret();
        }
    }
}