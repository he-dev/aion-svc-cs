namespace Aion.Data.JobInputs;

public record WorkflowRegularInput
{
    public required string WorkflowName { get; init; }
    public required WorkflowMode WorkflowMode { get; init; }
}