using Microsoft.Extensions.Logging;

namespace e.l.f.Logging
{
    public static class EventIds
    {
        public static readonly EventId AuthStartup = new EventId(1000, "AuthStartup");
        public static readonly EventId TokenValidation = new EventId(1001, "TokenValidation");
        public static readonly EventId TokenValidationFailed = new EventId(1002, "TokenValidationFailed");
        public static readonly EventId JwtKeyWarning = new EventId(1003, "JwtKeyWarning");
    }
}
