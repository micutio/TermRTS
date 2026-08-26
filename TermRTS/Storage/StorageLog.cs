using Microsoft.Extensions.Logging;

namespace TermRTS.Storage;

internal static partial class StorageLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Cannot find component of Type {ComponentType}")]
    public static partial void ComponentTypeNotFound(ILogger logger, Type componentType);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Cannot find component of Type {ComponentType} for entity {EntityId}")]
    public static partial void ComponentTypeNotFoundForEntity(ILogger logger, Type componentType,
        int entityId);
}