using System.Net;
using AdsbCarMap.Data;
using Xunit;

namespace AdsbCarMap.Tests
{
    /// <summary>
    /// Tests of the record of requests sent to external servers.
    /// </summary>
    public class RequestLogTests
    {
        /// <summary>
        /// Handler that answers with a status, throws an exception or waits for cancellation.
        /// </summary>
        private sealed class StubHandler : HttpMessageHandler
        {
            /// <summary>
            /// Status returned when no exception is set.
            /// </summary>
            public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

            /// <summary>
            /// Exception thrown instead of an answer, or null.
            /// </summary>
            public Exception? Failure { get; set; }

            /// <inheritdoc />
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Failure != null)
                {
                    throw Failure;
                }

                return Task.FromResult(new HttpResponseMessage(Status));
            }
        }

        /// <summary>
        /// Creates an entry for the filter tests.
        /// </summary>
        /// <param name="host">Host of the request.</param>
        /// <param name="status">Status code, or null.</param>
        /// <returns>The entry.</returns>
        private static RequestLogEntry Entry(string host, int? status)
        {
            return new RequestLogEntry(DateTime.UtcNow, "GET", host, "/", status, TimeSpan.Zero, null, false);
        }

        /// <summary>
        /// A successful request is recorded with its host, path, status and method.
        /// </summary>
        [Fact]
        public async Task Handler_RecordsSuccess()
        {
            RequestLog log = new RequestLog();
            HttpClient client = new HttpClient(new RequestLoggingHandler(log, new StubHandler()));

            await client.GetAsync("https://api.test/v2/point/1/2/3?x=1");

            RequestLogEntry entry = Assert.Single(log.GetEntries());

            Assert.Equal("GET", entry.Method);
            Assert.Equal("api.test", entry.Host);
            Assert.Equal("/v2/point/1/2/3?x=1", entry.PathAndQuery);
            Assert.Equal(200, entry.StatusCode);
            Assert.False(entry.IsError);
        }

        /// <summary>
        /// An error status counts as an error.
        /// </summary>
        [Fact]
        public async Task Handler_ErrorStatus_IsError()
        {
            RequestLog log = new RequestLog();
            HttpClient client = new HttpClient(new RequestLoggingHandler(log, new StubHandler { Status = HttpStatusCode.Forbidden }));

            await client.GetAsync("https://api.test/");

            Assert.True(log.GetEntries()[0].IsError);
            Assert.Equal(403, log.GetEntries()[0].StatusCode);
        }

        /// <summary>
        /// A network failure is recorded with its description and rethrown.
        /// </summary>
        [Fact]
        public async Task Handler_NetworkFailure_IsRecordedAndRethrown()
        {
            RequestLog log = new RequestLog();
            HttpClient client = new HttpClient(new RequestLoggingHandler(log, new StubHandler { Failure = new HttpRequestException("brak sieci") }));

            await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("https://api.test/"));

            RequestLogEntry entry = log.GetEntries()[0];

            Assert.Null(entry.StatusCode);
            Assert.Contains("brak sieci", entry.Error);
            Assert.True(entry.IsError);
        }

        /// <summary>
        /// A request cancelled by the application is recorded but is not an error.
        /// </summary>
        [Fact]
        public async Task Handler_Cancelled_IsNotError()
        {
            RequestLog log = new RequestLog();
            HttpClient client = new HttpClient(new RequestLoggingHandler(log, new StubHandler()));
            using CancellationTokenSource source = new CancellationTokenSource();
            source.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://api.test/", source.Token));

            Assert.True(log.GetEntries()[0].Cancelled);
            Assert.False(log.GetEntries()[0].IsError);
        }

        /// <summary>
        /// A timeout, which cancels the request without the application asking, is an error.
        /// </summary>
        [Fact]
        public async Task Handler_Timeout_IsError()
        {
            RequestLog log = new RequestLog();
            HttpClient client = new HttpClient(new RequestLoggingHandler(log, new StubHandler { Failure = new TaskCanceledException("timeout") }));

            await Assert.ThrowsAsync<TaskCanceledException>(() => client.GetAsync("https://api.test/"));

            Assert.True(log.GetEntries()[0].IsError);
            Assert.Equal("Przekroczono czas oczekiwania", log.GetEntries()[0].Error);
        }

        /// <summary>
        /// The newest request comes first and the oldest ones are dropped over the capacity.
        /// </summary>
        [Fact]
        public void Add_KeepsNewestFirstWithinCapacity()
        {
            RequestLog log = new RequestLog(2);

            log.Add(Entry("a", 200));
            log.Add(Entry("b", 200));
            log.Add(Entry("c", 200));

            List<RequestLogEntry> entries = log.GetEntries();

            Assert.Equal(2, entries.Count);
            Assert.Equal("c", entries[0].Host);
            Assert.Equal("b", entries[1].Host);
        }

        /// <summary>
        /// Clearing empties the log and both changes raise the event.
        /// </summary>
        [Fact]
        public void Clear_EmptiesAndRaisesChanged()
        {
            RequestLog log = new RequestLog();
            int raised = 0;
            log.Changed += () => raised++;

            log.Add(Entry("a", 200));
            log.Clear();

            Assert.Empty(log.GetEntries());
            Assert.Equal(2, raised);
        }

        /// <summary>
        /// The filter keeps only errors and/or one host.
        /// </summary>
        [Fact]
        public void Filter_ByErrorsAndHost()
        {
            List<RequestLogEntry> entries = new List<RequestLogEntry> { Entry("a", 200), Entry("a", 403), Entry("b", 500), Entry("b", 200) };

            Assert.Equal(4, RequestLog.Filter(entries, false, null).Count);
            Assert.Equal(2, RequestLog.Filter(entries, true, null).Count);
            Assert.Equal(2, RequestLog.Filter(entries, false, "B").Count);
            Assert.Equal(403, Assert.Single(RequestLog.Filter(entries, true, "a")).StatusCode);
        }

        /// <summary>
        /// The hosts are listed once each, alphabetically.
        /// </summary>
        [Fact]
        public void GetHosts_DistinctSorted()
        {
            RequestLog log = new RequestLog();
            log.Add(Entry("b", 200));
            log.Add(Entry("a", 200));
            log.Add(Entry("b", 200));

            Assert.Equal(new List<string> { "a", "b" }, log.GetHosts());
        }

        /// <summary>
        /// A log needs room for at least one request.
        /// </summary>
        [Fact]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RequestLog(0));
        }
    }
}
