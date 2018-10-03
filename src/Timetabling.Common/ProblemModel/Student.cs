using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Common.ProblemModel
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

    public class StudentData : Student
    {
        public StudentData(
            int id,
            int[] courses,
            Dictionary<int, EnrollmentConfiguration> enrollmentConfigurations,
            IEnumerable<ClassData> availableClasses)
            : base(id, courses)
        {
            EnrollmentConfigurations = enrollmentConfigurations;
            var enumerated = availableClasses as List<ClassData> ?? availableClasses?.ToList() ?? new List<ClassData>();
            AvailableClasses = enumerated.Select(c => c.Id).ToArray();
            LooseClasses = enumerated.Where(c => c.Children.Count == 0).Select(c => c.Id).ToArray();
        }

        public readonly Dictionary<int, EnrollmentConfiguration> EnrollmentConfigurations;

        public readonly int[] AvailableClasses;

        public readonly int[] LooseClasses;
    }

    public struct EnrollmentConfiguration
    {
        public EnrollmentConfiguration(int courseIndex, int configIndex, int subpartIndex, int classIndex)
        {
            CourseIndex = courseIndex;
            ConfigIndex = configIndex;
            SubpartIndex = subpartIndex;
            ClassIndex = classIndex;
        }

        public readonly int CourseIndex;

        public readonly int ConfigIndex;

        public readonly int SubpartIndex;

        public readonly int ClassIndex;
    }
}
