namespace Timetabling.Common.ProblemModel
{
    public class Subpart
    {
        public Subpart(int id, int courseId, Class[] classes)
        {
            Id = id;
            CourseId = courseId;
            Classes = classes;
        }

        public int Id { get; }

        public int CourseId { get; }

        public Class[] Classes { get; }
    }
}