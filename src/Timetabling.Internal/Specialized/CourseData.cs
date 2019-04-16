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
    }
}
