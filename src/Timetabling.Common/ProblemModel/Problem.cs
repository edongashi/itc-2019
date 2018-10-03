using System.Collections.Generic;
using System.Linq;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel
{
    public class Problem
    {
        public Problem(
            string name,
            int numberOfWeeks,
            int daysPerWeek,
            int slotsPerDay,
            int timePenalty,
            int roomPenalty,
            int distributionPenalty,
            int studentPenalty,
            Room[] rooms,
            Course[] courses,
            Student[] students,
            IConstraint[] constraints)
        {
            Name = name;
            NumberOfWeeks = numberOfWeeks;
            DaysPerWeek = daysPerWeek;
            SlotsPerDay = slotsPerDay;
            TimePenalty = timePenalty;
            RoomPenalty = roomPenalty;
            DistributionPenalty = distributionPenalty;
            StudentPenalty = studentPenalty;

            TravelTimes = new int[rooms.Length, rooms.Length];
            for (var i = 0; i < rooms.Length; i++)
            {
                foreach (var travelTime in rooms[i].TravelTimes)
                {
                    TravelTimes[i, travelTime.RoomId] = travelTime.Value;
                    TravelTimes[travelTime.RoomId, i] = travelTime.Value;
                }
            }

            Constraints = constraints;

            var roomClasses = new List<int>[rooms.Length];
            var rawClasses = courses.SelectMany(c => c.Classes).ToList();
            Classes = rawClasses
                .Select(c =>
                {
                    var classConstraints = constraints
                        .Where(constraint => constraint.InvolvesClass(c.Id))
                        .ToList();
                    return new ClassData(
                        c.Id,
                        c.ParentId,
                        c.CourseId,
                        c.Capacity,
                        c.PossibleRooms,
                        c.PossibleSchedules,
                        classConstraints.Where(constraint => constraint.Type == ConstraintType.Common),
                        classConstraints.Where(constraint => constraint.Type == ConstraintType.Time),
                        classConstraints.Where(constraint => constraint.Type == ConstraintType.Room),
                        rawClasses.Where(rc => rc.ParentId == c.Id).Select(rc => rc.Id));
                })
                .ToArray();

            foreach (var @class in Classes)
            {
                foreach (var room in @class.PossibleRooms)
                {
                    var slot = roomClasses[room.Id];
                    if (slot == null)
                    {
                        roomClasses[room.Id] = slot = new List<int>();
                    }

                    slot.AddIfNotExists(@class.Id);
                }
            }

            Rooms = new RoomData[rooms.Length];
            for (var i = 0; i < rooms.Length; i++)
            {
                var room = rooms[i];
                var roomData = new RoomData(
                    room.Id,
                    room.Capacity,
                    room.UnavailableSchedules,
                    room.TravelTimes,
                    roomClasses[room.Id]?.ToArray() ?? new int[0]);
                Rooms[i] = roomData;
            }

            var courseStudents = new List<int>[courses.Length];
            foreach (var student in students)
            {
                foreach (var course in student.Courses)
                {
                    var slot = courseStudents[course];
                    if (slot == null)
                    {
                        courseStudents[course] = slot = new List<int>();
                    }

                    slot.AddIfNotExists(student.Id);
                }
            }

            Courses = new CourseData[courses.Length];
            for (var i = 0; i < courses.Length; i++)
            {
                var course = courses[i];
                var courseData = new CourseData(
                    course.Id,
                    course.Configurations,
                    courseStudents[course.Id]?.ToArray() ?? new int[0]);
                Courses[i] = courseData;
            }

            Students = new StudentData[students.Length];
            for (var i = 0; i < students.Length; i++)
            {
                var student = students[i];
                var enrollmentConfig = new Dictionary<int, EnrollmentConfiguration>();
                var classes = new List<ClassData>();
                for (var courseIndex = 0; courseIndex < student.Courses.Length; courseIndex++)
                {
                    var courseId = student.Courses[courseIndex];
                    var courseObject = courses[courseId];
                    for (var configIndex = 0; configIndex < courseObject.Configurations.Length; configIndex++)
                    {
                        var configObject = courseObject.Configurations[configIndex];
                        for (var subpartIndex = 0; subpartIndex < configObject.Subparts.Length; subpartIndex++)
                        {
                            var subpartObject = configObject.Subparts[subpartIndex];
                            for (var classIndex = 0; classIndex < subpartObject.Classes.Length; classIndex++)
                            {
                                var classObject = subpartObject.Classes[classIndex];
                                var classData = Classes[classObject.Id];
                                if (classData.Capacity > 0)
                                {
                                    classes.Add(classData);
                                }

                                enrollmentConfig[classData.Id] = new EnrollmentConfiguration(courseIndex, configIndex, subpartIndex, classIndex);
                            }
                        }
                    }
                }

                Students[i] = new StudentData(student.Id, student.Courses, enrollmentConfig, classes);
            }
        }

        public readonly string Name;

        public readonly int NumberOfWeeks;

        public readonly int DaysPerWeek;

        public readonly int SlotsPerDay;

        public readonly int TimePenalty;

        public readonly int RoomPenalty;

        public readonly int DistributionPenalty;

        public readonly int StudentPenalty;

        public readonly RoomData[] Rooms;

        public readonly CourseData[] Courses;

        public readonly ClassData[] Classes;

        public readonly StudentData[] Students;

        public readonly int[,] TravelTimes;

        public readonly IConstraint[] Constraints;
    }
}
