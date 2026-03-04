namespace Courier.Domain.Engine;

public abstract record ExecutionNode;

public sealed record StepNode(int StepIndex) : ExecutionNode;

public sealed record ForEachNode(
    int StepIndex,
    List<ExecutionNode> Body) : ExecutionNode;

public sealed record IfElseNode(
    int StepIndex,
    List<ExecutionNode> ThenBranch,
    List<ExecutionNode>? ElseBranch) : ExecutionNode;
