namespace Timetabling.Internal.Specialized
{
    public class Chain
    {
        public Chain(int courseId, int configurationIndex, int[] values)
        {
            CourseId = courseId;
            ConfigIndex = configurationIndex;
            Indexes = values;
        }

        public readonly int CourseId;

        public readonly int ConfigIndex;

        public readonly int[] Indexes;
    }
}
