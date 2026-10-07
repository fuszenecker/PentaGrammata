using System;

namespace PentaGrammata.Exceptions;

public sealed class ConfigurationPersistenceException : Exception
{
    public ConfigurationPersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
