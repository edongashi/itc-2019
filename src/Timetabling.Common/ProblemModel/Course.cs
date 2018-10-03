using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Common.ProblemModel
{
    public class Course
    {
        public Course(int id, CourseConfiguration[] configurations)
        {
            Id = id;
            Configurations = configurations;
        }

        public readonly int Id;

        public readonly CourseConfiguration[] Configurations;

        public IEnumerable<Class> Classes => Configurations.SelectMany(c => c.Classes);
    }
}