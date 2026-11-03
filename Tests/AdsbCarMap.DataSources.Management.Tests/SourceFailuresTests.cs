using System.Net;
using AdsbCarMap.DataSources.Models;
using Xunit;

namespace AdsbCarMap.DataSources.Management.Tests
{
    /// <summary>
    /// Tests of the recognition of a failure of a data source.
    /// </summary>
    public class SourceFailuresTests
    {
        /// <summary>
        /// Network, platform and parsing errors and a timeout are failures of the source.
        /// </summary>
        [Fact]
        public void IsFailure_TrueForErrorsOfTheConnection()
        {
            Assert.True(SourceFailures.IsFailure(new HttpRequestException("x", null, HttpStatusCode.BadGateway), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new IOException("Socket closed"), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new System.Text.Json.JsonException("bad"), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new RateLimitedException(TimeSpan.Zero), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new InvalidOperationException("platform"), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new TaskCanceledException("timeout"), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new OperationCanceledException(), CancellationToken.None));
        }

        /// <summary>
        /// A cancellation that the caller asked for is not a failure, and neither are errors that cannot be recovered from.
        /// </summary>
        [Fact]
        public void IsFailure_FalseForRequestedCancellationAndFatalErrors()
        {
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.False(SourceFailures.IsFailure(new OperationCanceledException(cancellation.Token), cancellation.Token));
            Assert.False(SourceFailures.IsFailure(new TaskCanceledException(), cancellation.Token));
            Assert.False(SourceFailures.IsFailure(new OutOfMemoryException(), CancellationToken.None));
            Assert.False(SourceFailures.IsFailure(new AccessViolationException(), CancellationToken.None));
            Assert.True(SourceFailures.IsFailure(new IOException("Socket closed"), cancellation.Token));
        }
    }
}
