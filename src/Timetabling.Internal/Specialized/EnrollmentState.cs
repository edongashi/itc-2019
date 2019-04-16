namespace Timetabling.Internal.Specialized
{
    internal class EnrollmentState
    {
        internal EnrollmentState(int configIndex, int[] subparts)
        {
            ConfigIndex = configIndex;
            Subparts = subparts;
        }

        internal readonly int ConfigIndex;

        internal readonly int[] Subparts;
    }
}
