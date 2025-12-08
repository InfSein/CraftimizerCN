namespace CraftimizerCN.Simulator;

public enum CompletionState : byte
{
    Incomplete,
    ProgressComplete,
    NoMoreDurability,

    InvalidAction,
    MaxActionCountReached,
    NoMoreActions
}
