using System;
using System.Collections.Generic;
using System.Linq;
using Timetabling.Common.SolutionModel;
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

            var (timeVariables, roomVariables) = GetClassVariables();
            TimeVariables = timeVariables;
            RoomVariables = roomVariables;
            AllClassVariables = timeVariables.Concat(roomVariables).ToArray();
            InitialSolution = CreateInitialSolution();
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

        public readonly Variable[] TimeVariables;

        public readonly Variable[] RoomVariables;

        public readonly Variable[] AllClassVariables;

        public readonly CourseVariable[] EnrollmentVariables;

        public readonly Solution InitialSolution;

        private CourseVariable[] GetCourseVariables()
        {

        }

        private (Variable[] timeVariables, Variable[] roomVariables) GetClassVariables()
        {
            var timeVariables = new List<Variable>();
            var roomVariables = new List<Variable>();
            foreach (var classData in Classes)
            {
                if (classData.PossibleSchedules.Length > 1)
                {
                    timeVariables.Add(new Variable(classData.Id, classData.PossibleSchedules.Length));
                }

                if (classData.PossibleRooms.Length > 1)
                {
                    roomVariables.Add(new Variable(classData.Id, classData.PossibleRooms.Length));
                }
            }

            return (timeVariables.ToArray(), roomVariables.ToArray());
        }



        private Solution CreateInitialSolution()
        {
            const int chunkSize = 256;
            var hardPenalty = 0d;
            var softPenalty = 0;
            var classStates = new ClassState[Classes.Length];
            var studentStates = new StudentState[Students.Length];
            for (var i = 0; i < classStates.Length; i++)
            {
                var classData = Classes[i];
                classStates[i] = new ClassState(
                    classData.PossibleRooms.Length > 0 ? 0 : -1,
                    classData.PossibleSchedules.Length > 0 ? 0 : throw new InvalidOperationException("Corrupt problem instance."),
                    0, 0d, 0, 0d, 0, 0d, 0, 0d, 0d, 0d);
            }

            for (var i = 0; i < studentStates.Length; i++)
            {
                var studentData = Students[i];
                var enrollmentStates = new List<EnrollmentState>();
                var classesSoFar = new List<(Schedule schedule, int room)>();
                var conflicts = 0;
                foreach (var courseId in studentData.Courses)
                {
                    var course = Courses[courseId];
                    if (course.BaselineConfiguration == -1)
                    {
                        throw new InvalidOperationException("Corrupt problem instance.");
                    }

                    var enrollmentState = new EnrollmentState(course.BaselineConfiguration, course.Baseline);
                    enrollmentStates.Add(enrollmentState);

                    var enrolledSubparts = enrollmentState.Subparts;
                    var config = course.Configurations[course.BaselineConfiguration];
                    for (var j = 0; j < enrolledSubparts.Length; j++)
                    {
                        var subpart = config.Subparts[j];
                        var classIndex = enrolledSubparts[j];
                        var classObject = subpart.Classes[classIndex];
                        var classData = Classes[classObject.Id];
                        var classState = classStates[classObject.Id];
                        classStates[classObject.Id] = classState = classState
                            .WithAttendees(classState.Attendees + 1, 0d, 0d);
                        var schedule = classData.PossibleSchedules[classState.Time];
                        var room = classData.PossibleRooms[classState.Room].Id;

                        foreach (var (prevSchedule, prevRoom) in classesSoFar)
                        {
                            var travelTime = room >= 0 && prevRoom >= 0 ? TravelTimes[room, prevRoom] : 0;
                            if (schedule.Overlaps(prevSchedule, travelTime))
                            {
                                conflicts++;
                            }
                        }

                        classesSoFar.Add((schedule, room));
                    }
                }

                softPenalty += conflicts * StudentPenalty;
                studentStates[i] = new StudentState(enrollmentStates.ToArray(), conflicts);
            }

            var partialSolution = new Solution(
                this,
                hardPenalty,
                softPenalty,
                new ChunkedArray<ClassState>(classStates, chunkSize),
                new ChunkedArray<StudentState>(studentStates, chunkSize));

            var classConflicts = 0;
            for (var i = 0; i < classStates.Length; i++)
            {
                var state = classStates[i];
                var classData = Classes[i];
                var schedule = classData.PossibleSchedules[state.Time];
                var roomId = classData.PossibleRooms[state.Room].Id;
                var room = Rooms[roomId];
                var roomCapacityPenalty = state.Attendees > room.Capacity
                    ? Solution.CapacityOverflowBase + (state.Attendees - room.Capacity) * Solution.CapacityOverflowRate
                    : 0d;
                var classCapacityPenalty = state.Attendees > classData.Capacity
                    ? Solution.CapacityOverflowBase + (state.Attendees - classData.Capacity) * Solution.CapacityOverflowRate
                    : 0d;

                hardPenalty += roomCapacityPenalty;
                hardPenalty += classCapacityPenalty;

                var roomUnavailablePenalty = 0d;
                foreach (var unavailableSchedule in room.UnavailableSchedules)
                {
                    if (schedule.Overlaps(unavailableSchedule))
                    {
                        roomUnavailablePenalty = 1d;
                        break;
                    }
                }

                hardPenalty += roomUnavailablePenalty;

                var (commonHardPenalty, commonSoftPenalty) = classData.CommonConstraints.Evaluate(this, partialSolution);
                var (roomHardPenalty, roomSoftPenalty) = classData.RoomConstraints.Evaluate(this, partialSolution);
                var (timeHardPenalty, timeSoftPenalty) = classData.TimeConstraints.Evaluate(this, partialSolution);
                hardPenalty += commonHardPenalty;
                hardPenalty += roomHardPenalty;
                hardPenalty += timeHardPenalty;
                softPenalty += DistributionPenalty * commonSoftPenalty;
                softPenalty += DistributionPenalty * roomSoftPenalty;
                softPenalty += DistributionPenalty * timeSoftPenalty;

                for (var j = 0; j < i; j++)
                {
                    var otherClassState = classStates[j];
                    var otherClassData = Classes[j];
                    var otherRoomId = otherClassData.PossibleRooms[otherClassState.Room].Id;
                    if (roomId != otherRoomId)
                    {
                        continue;
                    }

                    var otherSchedule = otherClassData.PossibleSchedules[otherClassState.Time];
                    if (schedule.Overlaps(otherSchedule))
                    {
                        classConflicts++;
                    }
                }

                classStates[i] = new ClassState(
                    state.Room,
                    state.Time,
                    state.Attendees,
                    timeHardPenalty,
                    timeSoftPenalty,
                    commonHardPenalty,
                    commonSoftPenalty,
                    roomHardPenalty,
                    roomSoftPenalty,
                    classCapacityPenalty,
                    roomCapacityPenalty,
                    roomUnavailablePenalty);
            }

            hardPenalty += classConflicts;

            return new Solution(
                this,
                hardPenalty,
                softPenalty,
                new ChunkedArray<ClassState>(classStates, chunkSize),
                new ChunkedArray<StudentState>(studentStates, chunkSize));
        }
    }
}
