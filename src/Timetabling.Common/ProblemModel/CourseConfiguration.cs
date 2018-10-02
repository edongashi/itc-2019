using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Common.ProblemModel
{
    public class CourseConfiguration
    {
        public CourseConfiguration(int id, int courseId, Subpart[] subparts)
        {
            Id = id;
            CourseId = courseId;
            Subparts = subparts;
        }

        public int Id { get; }

        public int CourseId { get; }

        public Subpart[] Subparts { get; }

        public IEnumerable<Class> Classes => Subparts.SelectMany(s => s.Classes);
    }
}