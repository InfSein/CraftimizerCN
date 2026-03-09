namespace CraftimizerCN.Simulator.Actions;

internal sealed class DutyAction2() : BaseAction(
    ActionCategory.Other, 1, 27, 2,
    durabilityCost: 0,
    increasesStepCount: false
    )
{
    public override void UseSuccess(Simulator s)
    {
        base.UseSuccess(s);
    }
}
