namespace Timetabling.Common.SolutionModel
{
    public class ConstraintState
    {
        public ConstraintState(int hardPenalty, int softPenalty)
        {
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
        }

        public readonly int HardPenalty;

        public readonly int SoftPenalty;
    }
}