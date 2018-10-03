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
            BaselineConfiguration = -1;
            for (var i = 0; i < configurations.Length; i++)
            {
                var config = configurations[i];
                if (config.Baseline != null)
                {
                    Baseline = config.Baseline;
                    BaselineConfiguration = i;
                    break;
                }
            }
        }

        public readonly int Id;

        public readonly CourseConfiguration[] Configurations;

        public readonly int BaselineConfiguration;

        public readonly int[] Baseline;

        public IEnumerable<Class> Classes => Configurations.SelectMany(c => c.Classes);
    }
}