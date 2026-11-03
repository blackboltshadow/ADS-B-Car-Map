using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using Xunit;

namespace AdsbCarMap.DataSources.Neptun.Tests
{
    /// <summary>
    /// Tests of the real WebSocket connection against a server on the local machine.
    /// </summary>
    public class WebSocketThreatConnectionTests
    {
        /// <summary>
        /// Starts a server that accepts one WebSocket connection and runs a script on it.
        /// </summary>
        /// <param name="script">Receives the accepted socket and the User-Agent header of the request.</param>
        /// <returns>The address of the server and the task of the script.</returns>
        private static (Uri Address, HttpListener Listener, Task Task) StartServer(Func<WebSocket, string?, Task> script)
        {
            int port;

            using (TcpListener probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }

            HttpListener listener = new HttpListener();

            listener.Prefixes.Add("http://localhost:" + port + "/");
            listener.Start();

            Task task = Task.Run(async () =>
            {
                HttpListenerContext context = await listener.GetContextAsync();
                HttpListenerWebSocketContext socketContext = await context.AcceptWebSocketAsync(null);

                await script(socketContext.WebSocket, context.Request.UserAgent);
            });

            return (new Uri("ws://localhost:" + port + "/"), listener, task);
        }

        /// <summary>
        /// A frame that comes in several parts is joined, the User-Agent header is sent and a close gives null.
        /// </summary>
        [Fact]
        public async Task Receive_JoinsPartsSendsUserAgentAndReportsClose()
        {
            string? userAgent = null;
            (Uri address, HttpListener listener, Task serverTask) = StartServer(async (WebSocket socket, string? agent) =>
            {
                userAgent = agent;

                byte[] first = Encoding.UTF8.GetBytes("{\"type\":\"hear");
                byte[] second = Encoding.UTF8.GetBytes("tbeat\"} Ніжин");

                await socket.SendAsync(first, WebSocketMessageType.Text, false, CancellationToken.None);
                await socket.SendAsync(second, WebSocketMessageType.Text, true, CancellationToken.None);
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "koniec", CancellationToken.None);
            });

            try
            {
                using CancellationTokenSource limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using WebSocketThreatConnection connection = new WebSocketThreatConnection(address, "AdsbCarMap/test");

                await connection.ConnectAsync(limit.Token);

                Assert.Equal("{\"type\":\"heartbeat\"} Ніжин", await connection.ReceiveAsync(limit.Token));
                Assert.Null(await connection.ReceiveAsync(limit.Token));

                await serverTask.WaitAsync(TimeSpan.FromSeconds(10));
                Assert.Equal("AdsbCarMap/test", userAgent);
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// A frame larger than the limit is refused.
        /// </summary>
        [Fact]
        public async Task Receive_TooLargeFrame_Throws()
        {
            (Uri address, HttpListener listener, Task serverTask) = StartServer(async (WebSocket socket, string? agent) =>
            {
                byte[] chunk = new byte[256 * 1024];

                try
                {
                    for (int index = 0; index < 6; index++)
                    {
                        await socket.SendAsync(chunk, WebSocketMessageType.Text, false, CancellationToken.None);
                    }
                }
                catch (WebSocketException)
                {
                }
            });

            try
            {
                using CancellationTokenSource limit = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using WebSocketThreatConnection connection = new WebSocketThreatConnection(address, null);

                await connection.ConnectAsync(limit.Token);
                await Assert.ThrowsAsync<InvalidDataException>(() => connection.ReceiveAsync(limit.Token));
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// Connecting to an address where nobody listens fails.
        /// </summary>
        [Fact]
        public async Task Connect_NoServer_Throws()
        {
            int port;

            using (TcpListener probe = new TcpListener(IPAddress.Loopback, 0))
            {
                probe.Start();
                port = ((IPEndPoint)probe.LocalEndpoint).Port;
            }

            using WebSocketThreatConnection connection = new WebSocketThreatConnection(new Uri("ws://localhost:" + port + "/"), null);

            await Assert.ThrowsAnyAsync<Exception>(() => connection.ConnectAsync(CancellationToken.None));
        }
    }
}
