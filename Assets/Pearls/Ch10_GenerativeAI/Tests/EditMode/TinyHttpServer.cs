using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Pearls.GenerativeAI.Tests
{
    /// <summary>
    /// A deliberately tiny HTTP/1.1 responder on 127.0.0.1 for tests. Each request gets the
    /// response returned by Respond(hitNumber). Returning null holds the connection open
    /// without answering, which is how a hung backend looks to the client.
    /// </summary>
    public sealed class TinyHttpServer : IDisposable
    {
        public struct Reply
        {
            public int Status;
            public string Body;
            public string ExtraHeaders; // e.g. "Retry-After: 1\r\n"
        }

        private readonly TcpListener _listener;
        private readonly Thread _thread;
        private volatile bool _running = true;
        private int _hits;
        private int _clientsGaveUp;

        /// <summary>Hung requests whose client closed the connection (for example by aborting).</summary>
        public int ClientsGaveUp => Volatile.Read(ref _clientsGaveUp);

        public Func<int, Reply?> Respond = _ => new Reply { Status = 200, Body = "ok" };
        public int Hits => Volatile.Read(ref _hits);
        public string Url { get; }

        public TinyHttpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Url = $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
            _thread = new Thread(Loop) { IsBackground = true };
            _thread.Start();
        }

        private void Loop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { return; }
                ThreadPool.QueueUserWorkItem(_ => Handle(client));
            }
        }

        private void Handle(TcpClient client)
        {
            try
            {
                var stream = client.GetStream();
                ReadRequest(stream);
                int hit = Interlocked.Increment(ref _hits);
                Reply? reply = Respond(hit);
                if (reply == null)
                {
                    // Hang: keep the socket open until the client gives up or the server stops.
                    while (_running)
                    {
                        if (client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0)
                        {
                            Interlocked.Increment(ref _clientsGaveUp); // the client closed its end
                            break;
                        }
                        Thread.Sleep(20);
                    }
                    client.Close();
                    return;
                }

                byte[] body = Encoding.UTF8.GetBytes(reply.Value.Body ?? "");
                string head = $"HTTP/1.1 {reply.Value.Status} Status\r\n" +
                              "Content-Type: application/json\r\n" +
                              $"Content-Length: {body.Length}\r\n" +
                              "Connection: close\r\n" +
                              (reply.Value.ExtraHeaders ?? "") + "\r\n";
                byte[] headBytes = Encoding.ASCII.GetBytes(head);
                stream.Write(headBytes, 0, headBytes.Length);
                stream.Write(body, 0, body.Length);
                stream.Flush();
                client.Close();
            }
            catch
            {
                client.Close();
            }
        }

        private static void ReadRequest(NetworkStream stream)
        {
            var sb = new StringBuilder();
            var buffer = new byte[1];
            while (!sb.ToString().EndsWith("\r\n\r\n") && stream.Read(buffer, 0, 1) == 1)
                sb.Append((char)buffer[0]);

            int contentLength = 0;
            foreach (var line in sb.ToString().Split(new[] { "\r\n" }, StringSplitOptions.None))
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line.Substring(15).Trim(), out contentLength);

            var body = new byte[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                int n = stream.Read(body, read, contentLength - read);
                if (n <= 0) break;
                read += n;
            }
        }

        public void Dispose()
        {
            _running = false;
            _listener.Stop();
        }
    }
}
