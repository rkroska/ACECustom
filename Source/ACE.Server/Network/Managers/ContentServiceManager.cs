using ACE.Common;
using ACE.Database;
using ACE.Database.SQLFormatters.World;
using ACE.Entity;
using ACE.Server.Command.Handlers;
using K4os.Compression.LZ4.Internal;
using log4net;
using MySqlX.XDevAPI;
using MySqlX.XDevAPI.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Network.Managers
{
    public static class ContentServiceManager
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static HttpListener listener;

        private static WeenieSQLWriter weenieSQLWriter;

        public static void Initialize()
        {
            var hosts = new List<IPAddress>();

            try
            {
                var splits = ConfigManager.Config.Server.Network.Host.Split(",");

                foreach (var split in splits)
                    hosts.Add(IPAddress.Parse(split));
            }
            catch (Exception ex)
            {
                log.Error($"Unable to use {ConfigManager.Config.Server.Network.Host} as host due to: {ex}");
                log.Error("Using IPAddress.Any as host instead.");
                hosts.Clear();
                hosts.Add(IPAddress.Any);
            }

            if (ConfigManager.Config.Server.Network.ContentPort.HasValue)
            {
                //listener = new ConnectionListener(hosts[0], ConfigManager.Config.Server.Network.ContentPort.Value);
                //listener = TcpListener.Create((int)ConfigManager.Config.Server.Network.ContentPort.Value);
                listener = new HttpListener();
#if DEBUG
                listener.Prefixes.Add($"http://localhost:{ConfigManager.Config.Server.Network.ContentPort.Value}/content/");
#elif RELEASE
                listener.Prefixes.Add($"http://infiniteleaftide.online:{ConfigManager.Config.Server.Network.ContentPort.Value}/content/");
#endif
                log.Info($"Binding ConnectionListener to {hosts[0]}:{ConfigManager.Config.Server.Network.ContentPort.Value}");
                listener.Start();
                var res = listener.BeginGetContext(new AsyncCallback(OnRequest), listener);
                
            }
        }

        public static void OnRequest(System.IAsyncResult ar)
        {
            HttpListener listener = (HttpListener)ar.AsyncState;
            // Call EndGetContext to complete the asynchronous operation.
            HttpListenerContext context = listener.EndGetContext(ar);
            HttpListenerRequest request = context.Request;
            // Obtain a response object.
            HttpListenerResponse response = context.Response;
            string type = request.QueryString["type"]; //todo: add more types
            string sid = request.QueryString["id"];
            string user = request.QueryString["user"];
            string pass = request.QueryString["pass"];
            if (string.IsNullOrEmpty(user)) //todo: actually check user, password and account level
            {
                Receive(ar);
                return;
            }
            if (string.IsNullOrEmpty(pass))
            {
                Receive(ar);
                return;
            }
            uint id = 0;
            if (sid != null)
            {
                id = uint.Parse(sid);
            }

            //todo switch on type
            var weenie = DatabaseManager.World.GetWeenie(id);
            if (weenie != null)
            {
                if (weenieSQLWriter == null)
                {
                    weenieSQLWriter = new WeenieSQLWriter();
                    weenieSQLWriter.WeenieNames = DatabaseManager.World.GetAllWeenieNames();
                    weenieSQLWriter.SpellNames = DatabaseManager.World.GetAllSpellNames();
                    weenieSQLWriter.TreasureDeath = DatabaseManager.World.GetAllTreasureDeath();
                    weenieSQLWriter.TreasureWielded = DatabaseManager.World.GetAllTreasureWielded();
                    weenieSQLWriter.PacketOpCodes = PacketOpCodeNames.Values;
                }

                MemoryStream mem = new MemoryStream();
                StreamWriter sw = new StreamWriter(mem);

                try
                {
                    weenieSQLWriter.CreateSQLDELETEStatement(weenie, sw);
                    sw.WriteLine();
                    weenieSQLWriter.CreateSQLINSERTStatement(weenie, sw);
                    sw.WriteLine();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(mem.ToArray(), 0, (int)mem.Length));
                sw.Close();
                // Get a response stream and write the response to it.
                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
            else
            {
                string defaultResponse = "Item not found";
                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(defaultResponse);
                // Get a response stream and write the response to it.
                response.ContentLength64 = buffer.Length;
                System.IO.Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
            }
           
            
            Receive(ar);
        }

        private static void Receive(IAsyncResult res)
        {
            HttpListener listener = (HttpListener)res.AsyncState;
            listener.BeginGetContext(new AsyncCallback(OnRequest), listener);
        }
    }
}
