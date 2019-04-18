using System.Linq;

namespace Timetabling.Internal.Specialized
{
    public class CourseData : Course
    {
        internal CourseData(int id, CourseConfiguration[] configurations, int[] possibleStudents)
            : base(id, configurations)
        {
            PossibleStudents = possibleStudents;
        }

        public readonly int[] PossibleStudents;

        internal int MaxClasses()
        {
            return Configurations.Max(c => c.Subparts.Length);
        }
    }
}
