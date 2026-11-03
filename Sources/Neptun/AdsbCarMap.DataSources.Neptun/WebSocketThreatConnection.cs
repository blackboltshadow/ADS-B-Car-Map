using System.Net.WebSockets;
using System.Text;

namespace AdsbCarMap.DataSources.Neptun
{
    /// <summary>
    /// Connection to the threat stream over a WebSocket.
    /// </summary>
    public sealed class WebSocketThreatConnection : IThreatConnection
    {
        /// <summary>
        /// Largest frame accepted in bytes. The whole list of the threats is about 10 KB, so a much larger frame is not from the stream.
        /// </summary>
        public const int MaxMessageBytes = 1024 * 1024;

        /// <summary>
        /// Size of the buffer that one read fills.
        /// </summary>
        private const int ReadBufferBytes = 16 * 1024;

        /// <summary>
        /// Address of the stream.
        /// </summary>
        private readonly Uri address;

        /// <summary>
        /// Value of the User-Agent header, or null for none.
        /// </summary>
        private readonly string? userAgent;

        /// <summary>
        /// The socket of the connection.
        /// </summary>
        private readonly ClientWebSocket socket = new ClientWebSocket();

        /// <summary>
        /// Creates a connection that is not open yet.
        /// </summary>
        /// <param name="address">Address of the stream, starting with wss://.</param>
        /// <param name="userAgent">Value of the User-Agent header, or null for none.</param>
        public WebSocketThreatConnection(Uri address, string? userAgent)
        {
            this.address = address;
            this.userAgent = userAgent;
        }

        /// <inheritdoc />
        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                socket.Options.SetRequestHeader("User-Agent", userAgent);
            }

            await socket.ConnectAsync(address, cancellationToken);
        }

        /// <inheritdoc />
        /// <exception cref="InvalidDataException">The frame is larger than <see cref="MaxMessageBytes"/>.</exception>
        public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[ReadBufferBytes];

            using MemoryStream message = new MemoryStream();

            while (true)
            {
                ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                message.Write(buffer, 0, result.Count);

                if (message.Length > MaxMessageBytes)
                {
                    throw new InvalidDataException("Ramka strumienia jest większa niż dopuszczalny rozmiar.");
                }

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
