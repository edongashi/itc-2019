namespace Timetabling.Internal
{
    public class Student
    {
        public Student(int id, int[] courses)
        {
            Id = id;
            Courses = courses;
        }

        public readonly int Id;

        public readonly int[] Courses;
    }
}
