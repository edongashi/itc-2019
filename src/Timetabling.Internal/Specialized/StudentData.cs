using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Internal.Specialized
{
    public class StudentData : Student
    {
        internal StudentData(
            int id,
            int[] courses,
            Dictionary<int, EnrollmentConfiguration> enrollmentConfigurations,
            IEnumerable<ClassData> availableClasses)
            : base(id, courses)
        {
            EnrollmentConfigurations = enrollmentConfigurations;
            var enumerated = availableClasses as List<ClassData> ?? availableClasses?.ToList() ?? new List<ClassData>();
            LooseClasses = enumerated.Where(c => c.Capacity > 0 && c.Children.Count == 0).Select(c => c.Id).ToArray();
        }

        public readonly int[] LooseClasses;

        internal readonly Dictionary<int, EnrollmentConfiguration> EnrollmentConfigurations;
    }
}