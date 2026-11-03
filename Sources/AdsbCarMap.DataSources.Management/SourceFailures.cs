namespace AdsbCarMap.DataSources.Management
{
    /// <summary>
    /// Tells which exceptions raised while a data source is asked are a failure of the source, as opposed to a cancellation or a defect of the application. The platform HTTP handler does not always wrap its errors in <see cref="HttpRequestException"/>: on Android a connection that is closed while the answer is read can surface as a Java exception ("Socket closed"), which is not an <see cref="HttpRequestException"/> and cannot even be named in a library that does not reference Android. Code that handles only the exceptions it knows lets such an error escape, and in a background worker that silently ends the worker. So a failure is recognized by exclusion.
    /// </summary>
    public static class SourceFailures
    {
        /// <summary>
        /// Tells whether an exception raised by a source is a failure of the source that the caller handles by trying the next source or later.
        /// </summary>
        /// <param name="exception">The exception.</param>
        /// <param name="cancellationToken">Token of the request; a cancellation that it asked for is not a failure of the source.</param>
        /// <returns>False for a cancellation requested by the caller and for exceptions that mean the process is in a state that cannot be recovered; true for everything else, including a timeout, which is also reported as a cancellation.</returns>
        public static bool IsFailure(Exception exception, CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException)
            {
                return !cancellationToken.IsCancellationRequested;
            }

            return exception is not OutOfMemoryException && exception is not StackOverflowException && exception is not AccessViolationException;
        }
    }
}
