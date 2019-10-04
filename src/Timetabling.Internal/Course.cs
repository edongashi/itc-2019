using System.Collections.Generic;
using System.Linq;
using Timetabling.Internal.Specialized;

namespace Timetabling.Internal
{
    public class Course
    {
        public Course(int id, CourseConfiguration[] configurations)
        {
            Id = id;
            Configurations = configurations;
            ClassChains = Configurations
                .SelectMany((config, index) => config.ClassChains.Select(c => new Chain(id, index, c)))
                .ToArray();
        }

        public readonly int Id;

        public readonly CourseConfiguration[] Configurations;

        internal readonly Chain[] ClassChains;

        internal IEnumerable<Class> Classes => Configurations.SelectMany(c => c.Classes);
    }
}
