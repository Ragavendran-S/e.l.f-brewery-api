namespace e.l.f.Validation
{
    public class ValidationException : Exception
    {
        public IDictionary<string, string[]> Errors { get; }
        public ValidationException(string message, IDictionary<string, string[]>? errors = null) : base(message)

        {
            Errors = errors ?? new Dictionary<string, string[]>();
        }
    }

    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }

    public class UpstreamApiException : Exception
    {
        public int StatusCode { get; }
        public string? ResponseBody { get; }
        public UpstreamApiException(int statusCode, string message, string? responseBody = null) : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}