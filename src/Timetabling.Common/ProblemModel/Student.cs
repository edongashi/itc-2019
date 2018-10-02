using System.Collections.Generic;

namespace Timetabling.Common.ProblemModel
{
    public class Student
    {
        public Student(int id, int[] courses)
        {
            Id = id;
            Courses = courses;
        }

        public int Id { get; }

        public int[] Courses { get; }
    }

    public class StudentData : Student
    {
        public StudentData(int id, int[] courses, Dictionary<int, EnrollmentConfiguration> enrollmentConfigurations)
            : base(id, courses)
        {
            EnrollmentConfigurations = enrollmentConfigurations;
        }

        public Dictionary<int, EnrollmentConfiguration> EnrollmentConfigurations { get; }
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

        public int CourseIndex { get; }

        public int ConfigIndex { get; }

        public int SubpartIndex { get; }

        public int ClassIndex { get; }
    }
}
