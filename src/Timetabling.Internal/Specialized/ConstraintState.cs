namespace Timetabling.Internal.Specialized
{
    public class ConstraintState
    {
        internal ConstraintState(int hardPenalty, int softPenalty)
        {
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
        }

        public readonly int HardPenalty;

        public readonly int SoftPenalty;
    }
}
