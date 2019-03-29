namespace Timetabling.Common.ProblemModel
{
    public class CourseData : Course
    {
        public CourseData(int id, CourseConfiguration[] configurations, int[] possibleStudents)
            : base(id, configurations)
        {
            PossibleStudents = possibleStudents;
        }

        public readonly int[] PossibleStudents;
    }
}