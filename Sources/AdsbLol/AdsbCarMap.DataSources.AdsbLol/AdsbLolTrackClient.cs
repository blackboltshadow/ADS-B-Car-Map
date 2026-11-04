using System.Net;
using System.Text.Json;
using AdsbCarMap.DataSources.Interfaces;
using AdsbCarMap.DataSources.Models;

namespace AdsbCarMap.DataSources.AdsbLol
{
    /// <summary>
    /// Client of the adsb.lol recordings of aircraft paths (the trace files served by the tar1090 web map, one file per aircraft). The file holds the recorded positions of the last hours, so the client cuts out the last flight. It only fetches and converts the data; pausing the requests after limit responses is up to the manager.
    /// </summary>
    public sealed class AdsbLolTrackClient : IFlightTrackSource
    {
        /// <summary>
        /// Address of the web map host, which serves the trace files (not the API host).
        /// </summary>
        public const string DefaultBaseAddress = "https://adsb.lol/";

        /// <summary>
        /// Flag of a trace point meaning that a new flight starts at this point.
        /// </summary>
        private const int NewLegFlag = 2;

        /// <summary>
        /// Longest silence inside one flight; a longer gap between two points starts a new flight.
        /// </summary>
        private static readonly TimeSpan MaxGap = TimeSpan.FromMinutes(30);

        /// <summary>
        /// HTTP client with the base address of the web map.
        /// </summary>
        private readonly HttpClient httpClient;

        /// <summary>
        /// Creates a client using the given HTTP client.
        /// </summary>
        /// <param name="httpClient">HTTP client with the base address of the web map; it should decompress gzip responses.</param>
        public AdsbLolTrackClient(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        /// <inheritdoc />
        public string Name => AdsbLolClient.SourceName;

        /// <inheritdoc />
        /// <exception cref="JsonException">Invalid JSON response.</exception>
        public async Task<FlightTrack?> GetTrackAsync(string hex, CancellationToken cancellationToken)
        {
            string normalized = hex.Trim().ToLowerInvariant();

            if (normalized.Length < 2)
            {
                return null;
            }

            using HttpResponseMessage response = await httpClient.GetAsync("data/traces/" + normalized.Substring(normalized.Length - 2) + "/trace_full_" + Uri.EscapeDataString(normalized) + ".json", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new RateLimitedException(GetRetryAfter(response));
            }

            response.EnsureSuccessStatusCode();

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions(), cancellationToken);

            return Parse(normalized, document.RootElement);
        }

        /// <summary>
        /// Converts a trace file to the path of its last flight.
        /// </summary>
        /// <param name="hex">ICAO address of the aircraft.</param>
        /// <param name="root">Root of the trace file.</param>
        /// <returns>The path of the last flight or null when the file holds no usable position.</returns>
        public static FlightTrack? Parse(string hex, JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("timestamp", out JsonElement timestamp) || timestamp.ValueKind != JsonValueKind.Number
                || !root.TryGetProperty("trace", out JsonElement trace) || trace.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            DateTime baseUtc = DateTime.UnixEpoch.AddSeconds(timestamp.GetDouble());
            List<TrackPoint> points = new List<TrackPoint>();

            foreach (JsonElement row in trace.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() < 4
                    || row[0].ValueKind != JsonValueKind.Number || row[1].ValueKind != JsonValueKind.Number || row[2].ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                int flags = row.GetArrayLength() > 6 && row[6].ValueKind == JsonValueKind.Number ? row[6].GetInt32() : 0;
                bool onGround = row[3].ValueKind == JsonValueKind.String;
                int? altitude = row[3].ValueKind == JsonValueKind.Number ? (int)Math.Round(row[3].GetDouble()) : null;
                DateTime pointUtc = baseUtc.AddSeconds(row[0].GetDouble());

                if ((flags & NewLegFlag) != 0 || (points.Count > 0 && pointUtc - points[points.Count - 1].TimestampUtc > MaxGap))
                {
                    points.Clear();
                }

                points.Add(new TrackPoint(row[1].GetDouble(), row[2].GetDouble(), altitude, onGround, pointUtc));
            }

            return points.Count == 0 ? null : new FlightTrack(hex, points);
        }

        /// <summary>
        /// Reads the pause requested by the server with the Retry-After header.
        /// </summary>
        /// <param name="response">The limit response.</param>
        /// <returns>The requested pause or zero when the header is missing.</returns>
        private static TimeSpan GetRetryAfter(HttpResponseMessage response)
        {
            TimeSpan? requested = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);

            return requested.HasValue && requested.Value > TimeSpan.Zero ? requested.Value : TimeSpan.Zero;
        }
    }
}
