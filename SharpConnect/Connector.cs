using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace SharpConnect
{
    /// <summary>
    ///     Connect client to Goalball Game Manager
    /// </summary>
    public class Connector
    {
        // Delegate for method to called when GamesListHandler event is triggered
        public delegate void GamesListHandler(object sender, EventArgs e);

        // Event to trigger when the games list is changed
        public event GamesListHandler GamesListChanged;

        // The port in which the initial connection must be made to
        const int MASTER_PORT = 7000;

        // Constant read buffer size and the byte array read buffer
        const int READ_BUFFER_SIZE = 255;
        private byte[] readBuffer = new byte[READ_BUFFER_SIZE];

        // The TCPClient for the connection
        private TcpClient client;

        // The username for this client
        private string userName;

        // The current message and response
        public string message = string.Empty;
        public string res = String.Empty;

        // List of users sent from the GGM
        public List<string> lstUsers = new List<string>();
        // List of games sent from the GGM
        public List<string> lstGames = new List<string>();

        // Commands that can be received
        public const string RCV_JOIN_GGM = "JOIN";
        public const string RCV_REFUSE = "REFUSE";
        public const string RCV_LISTUSERS = "LISTUSERS";
        public const string RCV_LISTGAMES = "LISTGAMES";
        public const string RCV_PORT = "PORT";
        public const string RCV_CHAT = "CHAT";
        public const string RCV_BROADCAST = "BROAD";

        // Commands that can be sent
        public const string SND_CONNECT_GGM = "CONNECT";
        public const string SND_DISCONNECT_GGM = "DISCONNECT";
        public const string SND_REQUESTUSERS = "REQUESTUSERS";
        public const string SND_REQUESTGAMES = "REQUESTGAMES";
        public const string SND_CREATEGAME = "CREATEGAME";
        public const string SND_CHAT = "CHAT";

        // Whether or not this is connected to the GGM
        private bool connected = false;

        /// <summary>
        ///     Allows for outside classes to check if there is a connection
        /// </summary>
        /// <returns></returns>
        public bool isConnected()
        {
            return connected;
        }

        /// <summary>
        ///     Tries to connect to the master port on the provided IP
        /// </summary>
        /// <param name="netIP"></param>
        /// <param name="uName"></param>
        /// <returns></returns>
        public bool Connect(string netIP, string uName)
        {
            try
            {
                // Set the username
                userName = uName;
                // Set up the TCP client
                client = new TcpClient(netIP, MASTER_PORT);
                // Start an asynchronous read invoking DoRead to avoid lagging the user interface
                client.GetStream().BeginRead(readBuffer, 0, READ_BUFFER_SIZE, new AsyncCallback(DoRead), null);
                // Try to connect to the server
                AttemptLogin(uName);
                connected = true;
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        ///     Sends connect message to server with given username
        /// </summary>
        /// <param name="user"></param>
        public void AttemptLogin(string user)
        {
            SendData(string.Format("{0}|{1}", SND_CONNECT_GGM, user));
        }

        /// <summary>
        ///     Sends disconnect message to server
        /// </summary>
        public void Disconnect()
        {
            SendData(SND_DISCONNECT_GGM);
        }

        /// <summary>
        ///     Sends a request to the server to provide a list of the users
        /// </summary>
        public void ListUsers()
        {
            SendData(SND_REQUESTUSERS);
        }

        /// <summary>
        ///     Sends a request to the server to provide a list of the games
        /// </summary>
        public void ListGames()
        {
            SendData(SND_REQUESTGAMES);
        }

        /// <summary>
        ///     Sends a request to the server to create a game
        /// </summary>
        public void CreateGame()
        {
            SendData(SND_CREATEGAME);
        }

        /// <summary>
        ///     Send chat to server
        /// </summary>
        /// <param name="info"></param>
        public void PacketTest(string info)
        {
            SendData(string.Format("{0}|{1}", SND_CHAT, info));
        }

        /// <summary>
        ///     Callback function for TcpClient.GetStream.Begin, starts an asynchronous read from a stream
        /// </summary>
        /// <param name="ar"></param>
        private void DoRead(IAsyncResult ar)
        {
            int BytesRead;
            try
            {
                // Finish asynchronous read into readBuffer and return number of bytes read
                BytesRead = client.GetStream().EndRead(ar);
                if (BytesRead < 1)
                {
                    // If no bytes were read, server has closed  
                    res = "Disconnected";
                    connected = false;
                    return;
                }

                // Convert the byte array the message was saved into, minus two for the carriage return and new line
                message = Encoding.ASCII.GetString(readBuffer, 0, BytesRead - 2);
                // Determine what to do with the provided command
                ProcessCommands(message);
                // Start a new asynchronous read into readBuffer
                client.GetStream().BeginRead(readBuffer, 0, READ_BUFFER_SIZE, new AsyncCallback(DoRead), null);

            }
            catch
            {
                res = "Disconnected";
                connected = false;
            }
        }

        /// <summary>
        ///     Process the command received from the server, and take appropriate action
        /// </summary>
        /// <param name="message"></param>
        private void ProcessCommands(string message)
        {
            // Message parts are divided by "|", break the string into an array accordingly
            string[] dataArray;
            
            dataArray = message.Split((char)124);

            // Determine the command received
            switch (dataArray[0])
            {
                case RCV_JOIN_GGM:
                    // Server acknowledged login
                    res = "You have joined the chat";
                    break;
                case RCV_REFUSE:
                    // Server refused login with this user name, try to log in with another
                    AttemptLogin(userName);
                    res = "Attempted Re-Login";
                    break;
                case RCV_LISTUSERS:
                    // Server sent a list of users
                    ListUsers(dataArray);
                    break;
                case RCV_LISTGAMES:
                    // Server sent a list of games
                    ListGames(dataArray);
                    break;
                case RCV_PORT:
                    // Server sent port to join game on
                    res = dataArray[1].ToString();
                    break;
                case RCV_CHAT:
                    // Received chat message, display it
                    res = dataArray[1].ToString();
                    break;
                case RCV_BROADCAST:
                    // Server sent a broadcast message, display it
                    res = string.Format("ServerMessage: {0}", dataArray[1].ToString());
                    break;
            }
        }

        /// <summary>
        ///     Use a StreamWriter to send a message to server
        /// </summary>
        /// <param name="data"></param>
        private void SendData(string data)
        {
            StreamWriter writer = new StreamWriter(client.GetStream());
            res = data + (char)13;
            writer.Write(data + (char)13);
            // Make sure all data is sent now
            writer.Flush();
        }

        /// <summary>
        ///     Updates the list of users for array received from server
        /// </summary>
        /// <param name="users"></param>
        private void ListUsers(string[] users)
        {
            // Clear list and add all from users
            lstUsers.Clear();
            for (int i = 1; i <= (users.Length - 1); i++)
            {
                lstUsers.Add(users[i]);
            }
        }

        /// <summary>
        ///     Updates the list of games for array received from server
        /// </summary>
        /// <param name="games"></param>
        private void ListGames(string[] games)
        {
            // Add all games to temp list
            List<string> temp = new List<string>();
            for (int i = 1; i <= (games.Length - 1); i++)
            {
                temp.Add(games[i]);
            }

            // Check if there are any differents and display them if so
            if (!temp.SequenceEqual(lstGames))
            {
                lstGames = temp;
                GamesListChanged(this, new EventArgs());
            }
        }
    }
}
