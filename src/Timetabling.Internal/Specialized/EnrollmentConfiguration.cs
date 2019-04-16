namespace Timetabling.Internal.Specialized
{
    internal struct EnrollmentConfiguration
    {
        internal EnrollmentConfiguration(int courseIndex, int configIndex, int subpartIndex, int classIndex)
        {
            CourseIndex = courseIndex;
            ConfigIndex = configIndex;
            SubpartIndex = subpartIndex;
            ClassIndex = classIndex;
        }

        internal readonly int CourseIndex;

        internal readonly int ConfigIndex;

        internal readonly int SubpartIndex;

        internal readonly int ClassIndex;
    }
}