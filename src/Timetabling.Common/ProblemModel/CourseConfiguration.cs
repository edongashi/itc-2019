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

        public readonly int Id;

        public readonly int CourseId;

        public readonly Subpart[] Subparts;

        public readonly int[] Baseline;

        public IEnumerable<Class> Classes => Subparts.SelectMany(s => s.Classes);
    }
}