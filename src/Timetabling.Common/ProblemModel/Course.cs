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

        public int Id { get; }

        public CourseConfiguration[] Configurations { get; }

        public IEnumerable<Class> Classes => Configurations.SelectMany(c => c.Classes);
    }
}