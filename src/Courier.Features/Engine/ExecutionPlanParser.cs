using Courier.Domain.Engine;
using Courier.Domain.Entities;

namespace Courier.Features.Engine;

public static class ExecutionPlanParser
{
    private const string ForEach = "flow.foreach";
    private const string If = "flow.if";
    private const string Else = "flow.else";
    private const string End = "flow.end";

    public static List<ExecutionNode> Parse(List<JobStep> steps)
    {
        var index = 0;
        var nodes = ParseBlock(steps, ref index, blockType: null);

        if (index < steps.Count)
            throw new InvalidOperationException(
                $"Unexpected step at position {index}: '{steps[index].TypeKey}'. " +
                "Possible orphaned flow.else or flow.end without a matching block.");

        return nodes;
    }

    private static List<ExecutionNode> ParseBlock(List<JobStep> steps, ref int index, string? blockType)
    {
        var nodes = new List<ExecutionNode>();

        while (index < steps.Count)
        {
            var step = steps[index];
            var typeKey = step.TypeKey.ToLowerInvariant();

            switch (typeKey)
            {
                case ForEach:
                {
                    var foreachIndex = index;
                    index++; // advance past flow.foreach
                    var body = ParseBlock(steps, ref index, ForEach);
                    nodes.Add(new ForEachNode(foreachIndex, body));
                    break;
                }

                case If:
                {
                    var ifIndex = index;
                    index++; // advance past flow.if
                    var thenBranch = ParseBlock(steps, ref index, If);

                    List<ExecutionNode>? elseBranch = null;

                    // Check if we stopped at a flow.else
                    if (index < steps.Count &&
                        steps[index].TypeKey.Equals(Else, StringComparison.OrdinalIgnoreCase))
                    {
                        index++; // advance past flow.else
                        elseBranch = ParseBlock(steps, ref index, If);
                    }

                    nodes.Add(new IfElseNode(ifIndex, thenBranch, elseBranch));
                    break;
                }

                case Else:
                {
                    if (blockType != If)
                        throw new InvalidOperationException(
                            $"Unexpected flow.else at position {index}. " +
                            "flow.else must appear inside a flow.if block.");
                    // Don't advance — let the caller (If case) handle it
                    return nodes;
                }

                case End:
                {
                    if (blockType is null)
                        throw new InvalidOperationException(
                            $"Unexpected flow.end at position {index} without a matching flow.foreach or flow.if.");
                    index++; // advance past flow.end
                    return nodes;
                }

                default:
                    nodes.Add(new StepNode(index));
                    index++;
                    break;
            }
        }

        // We exhausted steps without finding a flow.end
        if (blockType is not null)
            throw new InvalidOperationException(
                $"Unterminated {blockType} block. Expected flow.end but reached end of step list.");

        return nodes;
    }
}
