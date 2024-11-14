namespace chatserver.utils
{
    /// <summary>
    /// Represents the outcome of an operation, including status, optional exception code, message, result, and exception details.
    /// </summary>
    public class ExitStatus
    {
        /// <summary>
        /// The status code of the operation.
        /// </summary>
        public ExitCodes status { get; set; } = ExitCodes.OK;

        /// <summary>
        /// Optional code for specific exceptions.
        /// </summary>
        public CustomExceptionCdes? exceptionCode { get; set; }

        /// <summary>
        /// A descriptive message to send to the final user.
        /// </summary>
        public string message { get; set; } = "";

        /// <summary>
        /// Possible result of an operation.
        /// </summary>
        public object? result { get; set; }

        /// <summary>
        /// Details of any exception that occurred.
        /// </summary>
        public string? exception { get; set; }
    }


    public enum ExitCodes : ushort
    {
        OK = 200,
        UNKNOWN_ERROR = 1,
        EXCEPTION = 2,
        ERROR = 3,
        BAD_REQUEST = 4,
        NOT_FOUND = 404,
        NOT_AUTHORIZED = 401,
    }

    public enum CustomExceptionCdes : ushort
    {
        BAD_REQUESTS = 400,
    }
}
