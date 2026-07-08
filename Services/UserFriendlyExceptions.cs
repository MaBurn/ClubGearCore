namespace ClubGear.Services;

public class UserFriendlyException : Exception
{
    public UserFriendlyException(string message)
        : base(message)
    {
    }

    public UserFriendlyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class ValidationException : UserFriendlyException
{
    public ValidationException(string message)
        : base(message)
    {
    }
}

public sealed class NotFoundException : UserFriendlyException
{
    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string resourceType, object id)
        : base($"{resourceType} mit ID '{id}' wurde nicht gefunden.")
    {
    }
}

public sealed class BusinessLogicException : UserFriendlyException
{
    public BusinessLogicException(string message)
        : base(message)
    {
    }
}

public sealed class PluginPermissionDeniedException : UserFriendlyException
{
    public PluginPermissionDeniedException(string moduleId, string permissionKey)
        : base($"Plugin '{moduleId}' darf die Berechtigung '{permissionKey}' nicht ausfuehren.")
    {
        ModuleId = moduleId;
        PermissionKey = permissionKey;
    }

    public string ModuleId { get; }

    public string PermissionKey { get; }
}
