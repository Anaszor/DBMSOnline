using System;

namespace DBMSOnline.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public class SecurityViolationException : DomainException
{
    public string RiskReason { get; }

    public SecurityViolationException(string message, string riskReason = "") : base(message)
    {
        RiskReason = riskReason;
    }
}

public class SsrfBlockedException : DomainException
{
    public string BlockedHost { get; }

    public SsrfBlockedException(string host, string reason)
        : base($"SSRF Protection Blocked Connection to '{host}': {reason}")
    {
        BlockedHost = host;
    }
}

public class DatabaseConnectionException : DomainException
{
    public DatabaseConnectionException(string message, Exception inner) : base(message, inner) { }
    public DatabaseConnectionException(string message) : base(message) { }
}
