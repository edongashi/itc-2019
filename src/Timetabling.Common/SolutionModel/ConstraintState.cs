namespace Timetabling.Common.SolutionModel
{
    public class ConstraintState
    {
        public ConstraintState(double hardPenalty, int softPenalty)
        {
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
        }

        public readonly double HardPenalty;

        public readonly int SoftPenalty;
    }
}