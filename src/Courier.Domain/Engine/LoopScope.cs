namespace Courier.Domain.Engine;

public sealed record LoopScope(
    int Depth,
    int CurrentIndex,
    int TotalItems);
