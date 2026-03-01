namespace Courier.Domain.Enums;

[Flags]
public enum TriggerEvent
{
    FileCreated = 1,
    FileModified = 2,
    FileExists = 4
}
