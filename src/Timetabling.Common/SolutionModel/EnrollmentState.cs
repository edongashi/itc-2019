namespace Timetabling.Common.SolutionModel
{
    public class EnrollmentState
    {
        public EnrollmentState(int configIndex, int[] subparts)
        {
            ConfigIndex = configIndex;
            Subparts = subparts;
        }

        public readonly int ConfigIndex;

        public readonly int[] Subparts;
    }
}