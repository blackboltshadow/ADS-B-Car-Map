using System.Net;
using System.Text;
using AdsbCarMap.Data;
using AdsbCarMap.DataSources.Management.Privacy;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of putting together the sources of the flight data: nothing stays null and the settings reach the right parts.
    /// </summary>
    public class DataCompositionTests : IDisposable
    {
        /// <summary>
        /// Handler that records the hosts that were asked, in order, and answers every request with the same status and body.
        /// </summary>
        private sealed class RecordingHandler : HttpMessageHandler
        {
            /// <summary>
            /// Status code returned for every request.
            /// </summary>
            private readonly HttpStatusCode status;

            /// <summary>
            /// Body returned for every request.
            /// </summary>
            private readonly string body;

            /// <summary>
            /// Creates the handler.
            /// </summary>
            /// <param name="status">Status code returned for every request.</param>
            /// <param name="body">Body returned for every request.</param>
            public RecordingHandler(HttpStatusCode status, string body)
            {
                this.status = status;
                this.body = body;
            }

            /// <summary>
            /// Hosts of the requests in the order in which they were made.
            /// </summary>
            public List<string> Hosts { get; } = new List<string>();

            /// <summary>
            /// Paths of the requests in the order in which they were made.
            /// </summary>
            public List<string> Paths { get; } = new List<string>();

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                lock (Hosts)
                {
                    Hosts.Add(request.RequestUri!.Host);
                    Paths.Add(request.RequestUri.AbsolutePath);
                }

                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
            }
        }

        /// <summary>
        /// Directory of the files of the memory, removed after the test.
        /// </summary>
        private readonly string directory = Path.Combine(Path.GetTempPath(), "composition-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Creates the composition over a recording handler.
        /// </summary>
        /// <param name="handler">Handler that receives all requests.</param>
        /// <param name="aircraftOrder">Names of the aircraft sources in the order of preference.</param>
        /// <param name="routeOrder">Names of the route sources in the order of preference.</param>
        /// <param name="fallback">Whether the next source is asked when the previous one gives no data.</param>
        /// <returns>The composition.</returns>
        private DataComposition Create(RecordingHandler handler, List<string> aircraftOrder, List<string> routeOrder, bool fallback = true)
        {
            DataCompositionSettings settings = new DataCompositionSettings(() => aircraftOrder, () => routeOrder, () => fallback, () => true, () => TimeSpan.FromMinutes(10));

            return new DataComposition(directory, address => new HttpClient(handler) { BaseAddress = new Uri(address) }, new AirportDatabase(), settings);
        }

        /// <summary>
        /// Removes the directory of the files of the memory.
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch (IOException)
            {
            }
        }

        /// <summary>
        /// Every part is created, so none of them can silently stay null.
        /// </summary>
        [Fact]
        public void Constructor_CreatesEveryPart()
        {
            DataComposition composition = Create(new RecordingHandler(HttpStatusCode.OK, "{}"), new List<string>(), new List<string>());

            Assert.NotNull(composition.AircraftSources);
            Assert.NotNull(composition.RouteSources);
            Assert.NotNull(composition.RouteMemory);
            Assert.NotNull(composition.AircraftMemory);
            Assert.NotNull(composition.Enricher);
            Assert.NotNull(composition.Keeper);
            Assert.Equal(0, composition.FlightInfoCount);
            Assert.True(Directory.Exists(directory));
        }

        /// <summary>
        /// Missing parts are reported at once with the name of the parameter.
        /// </summary>
        [Fact]
        public void Constructor_MissingParts_Throw()
        {
            DataCompositionSettings settings = new DataCompositionSettings(() => new List<string>(), () => new List<string>(), () => true, () => true, () => TimeSpan.FromMinutes(1));

            Assert.Throws<ArgumentNullException>(() => new DataComposition(directory, null!, new AirportDatabase(), settings));
            Assert.Throws<ArgumentNullException>(() => new DataComposition(directory, address => new HttpClient(), null!, settings));
            Assert.Throws<ArgumentNullException>(() => new DataComposition(directory, address => new HttpClient(), new AirportDatabase(), null!));
            Assert.Throws<ArgumentException>(() => new DataComposition(string.Empty, address => new HttpClient(), new AirportDatabase(), settings));
        }

        /// <summary>
        /// The position source sends the rounded position of the privacy settings, while the sources used directly are not touched.
        /// </summary>
        [Fact]
        public async Task PositionSource_SendsRoundedPosition()
        {
            RecordingHandler handler = new RecordingHandler(HttpStatusCode.OK, "{\"ac\":[]}");
            DataCompositionSettings settings = new DataCompositionSettings(() => new List<string> { "adsb.lol" }, () => new List<string>(), () => false, () => true, () => TimeSpan.FromMinutes(10), null, () => new PrivacyOptions(10.0));
            DataComposition composition = new DataComposition(directory, address => new HttpClient(handler) { BaseAddress = new Uri(address) }, new AirportDatabase(), settings);

            await composition.PositionSource.GetAircraftAsync(52.123456, 21.654321, 50.0, CancellationToken.None);
            await composition.AircraftSources.GetAircraftAsync(52.123456, 21.654321, 50.0, CancellationToken.None);

            Assert.StartsWith("/v2/point/52.12346/21.65432/", handler.Paths[1]);
            Assert.DoesNotContain("52.12346", handler.Paths[0]);
            Assert.DoesNotContain("21.65432", handler.Paths[0]);
        }

        /// <summary>
        /// Without privacy settings the position source sends the exact position.
        /// </summary>
        [Fact]
        public async Task PositionSource_WithoutPrivacySettings_SendsExactPosition()
        {
            RecordingHandler handler = new RecordingHandler(HttpStatusCode.OK, "{\"ac\":[]}");
            DataComposition composition = Create(handler, new List<string> { "adsb.lol" }, new List<string>());

            await composition.PositionSource.GetAircraftAsync(52.123456, 21.654321, 50.0, CancellationToken.None);

            Assert.Equal("/v2/point/52.12346/21.65432/50", handler.Paths[0]);
        }

        /// <summary>
        /// The order of the aircraft sources from the settings decides which service is asked first.
        /// </summary>
        [Fact]
        public async Task AircraftSources_FollowTheOrderFromTheSettings()
        {
            RecordingHandler first = new RecordingHandler(HttpStatusCode.OK, "{\"ac\":[]}");
            await Create(first, new List<string> { "adsb.lol", "airplanes.live" }, new List<string>()).AircraftSources.GetAircraftAsync(52.0, 21.0, 50.0, CancellationToken.None);

            RecordingHandler second = new RecordingHandler(HttpStatusCode.OK, "{\"ac\":[]}");
            await Create(second, new List<string> { "airplanes.live", "adsb.lol" }, new List<string>()).AircraftSources.GetAircraftAsync(52.0, 21.0, 50.0, CancellationToken.None);

            Assert.Equal(new List<string> { "api.adsb.lol" }, first.Hosts);
            Assert.Equal(new List<string> { "api.airplanes.live" }, second.Hosts);
        }

        /// <summary>
        /// The route sources are asked in the order from the settings until one of them knows the flight, and the fallback switch limits that to the first one.
        /// </summary>
        [Fact]
        public async Task RouteSources_FollowTheOrderAndTheFallbackSwitch()
        {
            RecordingHandler all = new RecordingHandler(HttpStatusCode.NotFound, "{}");
            Assert.Null(await Create(all, new List<string>(), new List<string> { "hexdb.io", "adsb.lol", "adsbdb.com" }).RouteSources.GetRouteAsync("LOT281", CancellationToken.None));

            RecordingHandler single = new RecordingHandler(HttpStatusCode.NotFound, "{}");
            Assert.Null(await Create(single, new List<string>(), new List<string> { "adsb.lol", "hexdb.io" }, false).RouteSources.GetRouteAsync("LOT281", CancellationToken.None));

            Assert.Equal(new List<string> { "hexdb.io", "api.adsb.lol", "api.adsbdb.com" }, all.Hosts);
            Assert.Equal(new List<string> { "api.adsb.lol" }, single.Hosts);
        }

        /// <summary>
        /// A route that hexdb.io gives as airport codes only is completed from the airports and stays in the memory shared with the keeper.
        /// </summary>
        [Fact]
        public async Task HexdbRoute_IsCompletedFromTheAirports()
        {
            AirportDatabase airports = new AirportDatabase();
            airports.LoadFrom(
                new string[]
                {
                    string.Join("\t", "EPWA", "WAW", "L", "Warsaw Chopin Airport", "Warsaw", "PL", "52.1657", "20.9671", "362", "1", "0"),
                    string.Join("\t", "EGLL", "LHR", "L", "London Heathrow Airport", "London", "GB", "51.4706", "-0.4619", "83", "1", "0"),
                },
                Array.Empty<string>());
            RecordingHandler handler = new RecordingHandler(HttpStatusCode.OK, "{\"flight\":\"LOT281\",\"route\":\"EPWA-EGLL\"}");
            DataCompositionSettings settings = new DataCompositionSettings(() => new List<string>(), () => new List<string> { "hexdb.io" }, () => false, () => true, () => TimeSpan.FromMinutes(10));
            DataComposition composition = new DataComposition(directory, address => new HttpClient(handler) { BaseAddress = new Uri(address) }, airports, settings);

            FlightRoute? route = await composition.RouteSources.GetRouteAsync("LOT281", CancellationToken.None);

            Assert.NotNull(route);
            Assert.Equal("WAW", route.OriginCode);
            Assert.Equal("LHR", route.DestinationCode);
            Assert.True(route.HasCoordinates);
            Assert.Equal("Warsaw", route.OriginCity);
        }

        /// <summary>
        /// The memory is shared between the parts and can be cleared as a whole.
        /// </summary>
        [Fact]
        public void ClearFlightInfo_EmptiesBothMemories()
        {
            DataComposition composition = Create(new RecordingHandler(HttpStatusCode.OK, "{}"), new List<string>(), new List<string>());
            composition.RouteMemory.Set("LOT281", new FlightRoute("WAW", null, "LHR", null));
            composition.AircraftMemory.Set("abc123", new AircraftDetails("abc123", "B738", null, null));

            Assert.Equal(2, composition.FlightInfoCount);

            composition.ClearFlightInfo();

            Assert.Equal(0, composition.FlightInfoCount);
        }
    }
}
