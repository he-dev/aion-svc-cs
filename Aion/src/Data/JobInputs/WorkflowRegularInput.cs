namespace Aion.Data.JobInputs;

public record WorkflowRegularInput
{
    public required WorkflowPath WorkflowPath { get; init; }
    public required WorkflowMode WorkflowMode { get; init; }
}