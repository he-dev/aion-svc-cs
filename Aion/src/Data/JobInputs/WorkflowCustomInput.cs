namespace Aion.Data.JobInputs;

public record WorkflowCustomInput
{
    public required WorkflowPath WorkflowPath { get; init; }
    public required WorkflowMode WorkflowMode { get; init; }
    public required string[] WorkflowSteps { get; set; }
}