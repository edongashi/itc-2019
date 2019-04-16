namespace Timetabling.Internal
{
    public class Subpart
    {
        public Subpart(int id, int courseId, Class[] classes)
        {
            Id = id;
            CourseId = courseId;
            Classes = classes;
        }

        public readonly int Id;

        public readonly int CourseId;

        public readonly Class[] Classes;
    }
}
