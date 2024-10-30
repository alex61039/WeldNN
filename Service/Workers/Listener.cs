using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WeldingService.Models;

namespace WeldingService.Workers
{
    public class Listener : BaseWorker
    {
        protected override string Name => "TCP/IP Listener";

        private const int timeoutSeconds = 30;
        private int threadCounts = 0;
        private int server_listener_port;
        private IPAddress server_listener_ip;
        private TcpListener tcpListener;
        private Thread listenThread;

        public Listener() : base()
        {
            server_listener_port = Convert.ToInt32(ConfigurationManager.AppSettings["ServerListenerPort"]);

            var ipString = ConfigurationManager.AppSettings["ServerListenerIP"];
            server_listener_ip = String.IsNullOrEmpty(ipString) || ipString == "*"
                ? IPAddress.Any
                : IPAddress.Parse(ipString);
        }

        protected override void InternalExecute()
        {
            // Create listner
            tcpListener = new TcpListener(server_listener_ip, server_listener_port);

            listenThread = new Thread(new ThreadStart(tcpListenerThread));
            listenThread.Start();
        }


        protected override void BeforeStop()
        {
            Logger.Log(LogLevel.Notice, "Stopping Listener");

            if (tcpListener != null)
            {
                try
                {
                    tcpListener.Stop();
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Stopping TCP listener");
                }
            }

            try
            {
                listenThread.Abort();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Stopping listen Thread");
            }

        }

        void tcpListenerThread()
        {
            // Start TCP listener
            try
            {
                tcpListener.Start();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, String.Format("Error starting TCP Listener on {0}:{1}", server_listener_ip.ToString(), server_listener_port));
            }

            while (true)
            {
                TcpClient client = null;
                Thread clientThread = null;

                try
                {
                    // Perform a blocking call to accept requests.
                    client = tcpListener.AcceptTcpClient();

                    // Create a thread to handle communication
                    // with connected client
                    clientThread = new Thread(new ParameterizedThreadStart(clientConnectionHandlerThread));

                    clientThread.IsBackground = true;
                    clientThread.Start(client);
                }
                catch (Exception e)
                {
                    Logger.LogException(e, "tcpListenerThread: starting clientConnectionHandlerThread");
                }

                if (m_exit.WaitOne(100))
                {
                    try
                    {
                        if (clientThread != null)
                            clientThread.Abort();
                    }
                    catch { }

                    try
                    {
                        if (client != null)
                        {
                            client.Close();
                            client.Dispose();
                        }
                    }
                    catch { }

                    break;
                }
            }

        }

        /// <summary>
        /// Thread - client connected to the server
        /// </summary>
        /// <param name="client">TcpClient</param>
        void clientConnectionHandlerThread(object client)
        {
            TcpClient tcpClient = (TcpClient)client;
            NetworkStream stream = tcpClient.GetStream();

            // Time connected
            DateTime timeConnectionStarted = DateTime.Now;

            // Time to timeout. Resets on every packet/message
            DateTime timeoutOn = DateTime.Now.AddSeconds(timeoutSeconds);

            // incremental counter of threads. Just for logging.
            int thisThreadCounter = threadCounts++;

            // Retrieve client's info
            var ipEndPoint = ((System.Net.IPEndPoint)(tcpClient.Client.RemoteEndPoint));
            var ipStr = ipEndPoint.Address.ToString();

            // Fetch MAC-address
            var macStr = "";
            byte[] macAddrArr = null;
            try
            {
                macAddrArr = BusinessLayer.Utils.Network.NetworkHelper.GetMacAddress(ipEndPoint.Address);

                var physicalAddress = macAddrArr != null ? new System.Net.NetworkInformation.PhysicalAddress(macAddrArr) : null;
                macStr = physicalAddress != null ? physicalAddress.ToString() : "";
            }
            catch (Exception ex)
            {
                Logger.LogException(ex,
                    String.Format("clientConnectionHandlerThread: Error fetching MAC address: {0}", ipStr));
                // return;
            }

            Logger.Log(LogLevel.Notice, "CONNECTED:   IP: {0};    MAC: {1};  Thread: {2}", ipStr, macStr, thisThreadCounter);


            // Buffer for reading data
            Byte[] bytes = new Byte[4096];
            String data = null;

            // Loop to receive all the data sent by the client.
            var connected = true;
            while (connected)
            {
                int bytesRead = 0;
                try
                {
                    if (stream.DataAvailable)
                    {
                        bytesRead = stream.Read(bytes, 0, 4096);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Reading stream");
                    connected = false;
                }

                if (bytesRead > 0)
                {
                    // Translate data bytes to a ASCII string.
                    data = Encoding.ASCII.GetString(bytes, 0, bytesRead);

                    // data = ":DC4F220AC7D4;001820000000000000002000000000000000000000000000000000000000000000000000000001300000180000000000000000000000000000000000000000000007D";
                    Logger.Log(LogLevel.Debug, " - thread {1}, IP={2}, MAC={3}: {4}", DateTime.Now, thisThreadCounter, ipStr, macStr, data);

                    // Try to fetch MAC from message:
                    // ':DC4F220AC7D4;0E018.......'
                    if (!String.IsNullOrEmpty(data))
                    {
                        var pos1 = data.IndexOf(':');
                        if (pos1 >= 0)
                        {
                            var pos2 = data.IndexOf(';', pos1);
                            if (pos2 > 0 && pos2 - pos1 == 13)
                            {
                                macStr = data.Substring(pos1 + 1, 12);
                                Logger.Log(LogLevel.Debug, " - MAC from packet: {0}", macStr);
                            }
                        }
                    }

                    // Queue/dump the data sent by the client.
                    var packet = new Packet
                    {
                        IP = ipStr,
                        MAC = macStr,
                        Data = data
                    };

                    // ==============================================================================================
                    // Enqueue packet to parse and store
                    Domain.IncomingPacketsQueue.Enqueue(packet);

                    // ==============================================================================================
                    // Wait for a time, maybe packet gets parsed
                    // Thread.Sleep(300);

                    // ==============================================================================================
                    // Check Response
                    Packet outboudPacket;
                    if (Domain.OutboundPacketsRepository.TryGet(macStr, out outboudPacket))
                    {
                        // Send reponse
                        try
                        {
                            Logger.Log(LogLevel.Debug, "Sending response to {0}, {1}:\n'{2}'", ipStr, macStr, outboudPacket.Data);

                            byte[] response_data = Encoding.ASCII.GetBytes(outboudPacket.Data);
                            stream.Write(response_data, 0, response_data.Length);
                            stream.Flush();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogException(ex, String.Format("Error sending packet to {0}: {1}", macStr, outboudPacket.Data));
                        }
                    }

                    // Update timeout
                    timeoutOn = DateTime.Now.AddSeconds(timeoutSeconds);
                }
                else
                {
                    // Check timeout
                    if (DateTime.Now > timeoutOn)
                    {
                        connected = false;
                        Logger.Log(LogLevel.Notice, "Timed out.");
                    }
                }



                // Send back a response.
                // byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);
                // stream.Write(msg, 0, msg.Length);
                // Console.WriteLine("Sent: {0}", data);
            }

            Logger.Log(LogLevel.Notice, "DISCONNECTED THREAD {0}!", thisThreadCounter);


            // Close connections 
            try
            {
                try
                {
                    stream.Dispose();
                }
                catch { }
                try
                {
                    tcpClient.Close();
                }
                catch { }
                try
                {
                    tcpClient.Dispose();
                }
                catch { }
            }
            catch { }
        }

    }
}
