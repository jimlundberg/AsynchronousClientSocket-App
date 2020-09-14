using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AsynchronousClientSocket
{
    class Program
    {
        /// <summary>
        /// State object for receiving data from remote device 
        /// </summary>
        public class StateObject
        {
            // Client socket
            public Socket workSocket = null;

            // Size of receive buffer
            public const int BufferSize = 256;

            // Receive buffer
            public byte[] buffer = new byte[BufferSize];

            // Received data string
            public StringBuilder sb = new StringBuilder();
        }

        /// <summary>
        /// Asynchronous Client
        /// </summary>
        public class AsynchronousClient
        {
            // ManualResetEvent instances signal completion
            private static readonly ManualResetEvent connectDone = new ManualResetEvent(false);
            private static readonly ManualResetEvent sendDone = new ManualResetEvent(false);
            private static readonly ManualResetEvent receiveDone = new ManualResetEvent(false);
            private const int Port = 3010;
            private const string Server = "127.0.0.1";
            private static String response;

            private static void StartClient()
            {
                bool simulationComplete = false;

                // Connect to a remote device
                try
                {
                    // Establish the remote endpoint for the socket
                    IPHostEntry ipHostInfo = Dns.GetHostEntry(Server);
                    IPAddress ipAddress = ipHostInfo.AddressList[0];
                    IPEndPoint remoteEP = new IPEndPoint(ipAddress, Port);

                    // Create a TCP/IP socket
                    Socket client = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                    // Connect to the remote endpoint
                    client.BeginConnect(remoteEP, new AsyncCallback(ConnectCallback), client);
                    connectDone.WaitOne();

                    // Send test data to the remote device
                    Send(client, "status<EOF>");

                    // Wait for send with timeout
                    sendDone.WaitOne();

                    // Receive the response from the remote device
                    Receive(client);

                    // Wait for Receive to complete or timeout
                    receiveDone.WaitOne();

                    // Write the response to the console
                    Console.WriteLine("Response received : {0}", response);

                    // Release the socket
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            /// <summary>
            /// Connect callback
            /// </summary>
            /// <param name="ar"></param>
            private static void ConnectCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the socket from the state object
                    Socket client = (Socket)ar.AsyncState;

                    // Complete the connection
                    client.EndConnect(ar);

                    Console.WriteLine("Socket connected to {0}", client.RemoteEndPoint.ToString());

                    // Signal that the connection has been made
                    connectDone.Set();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            /// <summary>
            /// Receive TCP/IP messages
            /// </summary>
            /// <param name="client"></param>
            private static void Receive(Socket client)
            {
                try
                {
                    // Create the state object
                    StateObject state = new StateObject
                    {
                        workSocket = client
                    };

                    // Begin receiving the data from the remote device
                    client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReceiveCallback), state);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            /// <summary>
            /// Receive callback
            /// </summary>
            /// <param name="ar"></param>
            private static void ReceiveCallback(IAsyncResult ar)
            {
                response = String.Empty;

                try
                {
                    // Retrieve the state object and the client socket
                    // from the asynchronous state object
                    StateObject state = (StateObject)ar.AsyncState;
                    Socket client = state.workSocket;

                    // Read data from the remote device
                    int bytesRead = client.EndReceive(ar);

                    if (bytesRead > 0)
                    {
                        // There might be more data, so store the data received so far
                        state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                        // Get the rest of the data
                        client.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                            new AsyncCallback(ReceiveCallback), state);
                    }
                    else
                    {
                        // All the data has arrived; put it in response
                        if (state.sb.Length > 1)
                        {
                            response = state.sb.ToString();
                        }

                        // Signal that all bytes have been received
                        receiveDone.Set();
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            /// <summary>
            /// Send message
            /// </summary>
            /// <param name="client"></param>
            /// <param name="data"></param>
            private static void Send(Socket client, String data)
            {
                // Convert the string data to byte data using ASCII encoding
                byte[] byteData = Encoding.ASCII.GetBytes(data);

                // Begin sending the data to the remote device
                client.BeginSend(byteData, 0, byteData.Length, 0,
                    new AsyncCallback(SendCallback), client);
            }

            /// <summary>
            /// Send callback
            /// </summary>
            /// <param name="ar"></param>
            private static void SendCallback(IAsyncResult ar)
            {
                try
                {
                    // Retrieve the socket from the state object
                    Socket client = (Socket)ar.AsyncState;

                    // Complete sending the data to the remote device
                    int bytesSent = client.EndSend(ar);
                    Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                    // Signal that all bytes have been sent
                    sendDone.Set();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }

            /// <summary>
            ///  Maing
            /// </summary>
            /// <param name="args"></param>
            /// <returns></returns>
            public static int Main(String[] args)
            {
                StartClient();
                return 0;
            }
        }
    }
}
