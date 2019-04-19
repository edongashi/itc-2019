using System;
using System.Collections.Generic;
using System.Linq;
using Timetabling.Internal.Specialized;
using Timetabling.Internal.Utils;

namespace Timetabling.Internal
{
    public class Problem
    {
        private const string CorruptInstance = "Corrupt problem instance.";

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
            rooms = rooms.OrderBy(r => r.Id).ToArray();
            courses = courses.OrderBy(c => c.Id).ToArray();
            students = students.OrderBy(s => s.Id).ToArray();
            constraints = constraints.OrderBy(c => c.Id).ToArray();

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
            HardConstraints = Constraints.Where(c => c.Required).ToArray();

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
                        courses.First(cs => cs.Classes.Any(cls => cls.Id == c.Id)).Id,
                        c.Capacity,
                        c.PossibleRooms,
                        c.PossibleSchedules,
                        classConstraints.Where(constraint => constraint.Type == ConstraintType.Common),
                        classConstraints.Where(constraint => constraint.Type == ConstraintType.Time),
                        classConstraints.Where(constraint => constraint.Type == ConstraintType.Room),
                        rawClasses.Where(rc => rc.ParentId == c.Id).Select(rc => rc.Id));
                })
                .OrderBy(c => c.Id)
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

            var (timeVariables, roomVariables, timeSparse, roomSparse) = GetClassVariables();
            TimeVariables = timeVariables;
            RoomVariables = roomVariables;
            TimeVariablesSparse = timeSparse;
            RoomVariablesSparse = roomSparse;
            AllClassVariables = timeVariables.Concat(roomVariables).ToArray();
            StudentVariables = GetStudentVariables();

            var n = Classes.Count(c => c.PossibleRooms.Length > 0);
            var combinations = n * (n - 1) / 2;
            WorstCaseClassConflicts = combinations;
            WorstCaseRoomsUnavailable = Rooms.Count(r => r.UnavailableSchedules.Length > 0);
            WorstSoftDistributionPenalty =
                distributionPenalty * Constraints.Where(c => !c.Required).Sum(c => c.WorstCase);
            WorstRoomPenalty = roomPenalty * Classes.Where(c => c.PossibleRooms.Length > 0)
                                   .Sum(c => c.PossibleRooms.Max(r => r.Penalty));
            WorstTimePenalty = timePenalty * Classes.Sum(c => c.PossibleSchedules.Max(s => s.Penalty));
            WorstStudentPenalty = studentPenalty * Students.Sum(s =>
            {
                var cn = s.Courses.Sum(cid => Courses[cid].MaxClasses());
                return cn * (cn - 1) / 2;
            });
            WorstSoftPenalty =
                WorstSoftDistributionPenalty + WorstRoomPenalty + WorstTimePenalty + WorstStudentPenalty;

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

        public readonly IConstraint[] HardConstraints;

        public readonly Variable[] TimeVariables;

        public readonly Variable[] RoomVariables;

        public readonly Variable[] AllClassVariables;

        public readonly Variable[] TimeVariablesSparse;

        public readonly Variable[] RoomVariablesSparse;

        public readonly CourseVariable[] StudentVariables;

        public readonly Solution InitialSolution;

        internal readonly double WorstCaseClassConflicts;

        internal readonly double WorstCaseRoomsUnavailable;

        internal readonly double WorstSoftDistributionPenalty;

        internal readonly double WorstRoomPenalty;

        internal readonly double WorstTimePenalty;

        internal readonly double WorstStudentPenalty;

        internal readonly double WorstSoftPenalty;

        private CourseVariable[] GetStudentVariables()
        {
            var variables = new List<CourseVariable>();
            for (var i = 0; i < Students.Length; i++)
            {
                var student = Students[i];
                for (var j = 0; j < student.Courses.Length; j++)
                {
                    var course = Courses[student.Courses[j]];
                    if (course.Configurations.Length == 1)
                    {
                        var subparts = course.Configurations[0].Subparts;
                        if (subparts.All(subpart => subpart.Classes.Length <= 1))
                        {
                            // One configuration, all subparts predetermined
                            // -> nothing that can be mutated in this course.
                            continue;
                        }
                    }
                    else if (course.Configurations.Length == 0)
                    {
                        throw new InvalidOperationException(CorruptInstance);
                    }

                    var configs = new List<int[][]>();
                    foreach (var config in course.Configurations)
                    {
                        var configVariables = new List<int[]>();
                        foreach (var subpart in config.Subparts)
                        {
                            var isParentSubpart = subpart.Classes.All(c => Classes[c.Id].HasChildren);

                            if (isParentSubpart)
                            {
                                continue;
                            }

                            if (subpart.Classes.All(c => c.Capacity <= 0))
                            {
                                continue;
                            }

                            var variable = subpart.Classes
                                .Where(c => c.Capacity > 0)
                                .Select(@class => @class.Id).ToArray();

                            if (variable.Length == 0)
                            {
                                throw new InvalidOperationException(CorruptInstance);
                            }

                            configVariables.Add(variable);
                        }

                        if (configVariables.Count != 0)
                        {
                            configs.Add(configVariables.ToArray());
                        }
                    }

                    if (configs.Count == 0)
                    {
                        throw new InvalidOperationException(CorruptInstance);
                    }

                    variables.Add(new CourseVariable(student.Id, configs.ToArray()));
                }
            }

            return variables.ToArray();
        }

        private (Variable[] timeVariables, Variable[] roomVariables, Variable[] timeSparse, Variable[] roomSparse) GetClassVariables()
        {
            var timeVariables = new List<Variable>();
            var roomVariables = new List<Variable>();
            var timeSparse = new Variable[Classes.Length];
            var roomSparse = new Variable[Classes.Length];
            foreach (var classData in Classes)
            {
                if (classData.PossibleSchedules.Length > 1)
                {
                    var variable = new Variable(classData.Id, classData.PossibleSchedules.Length, VariableType.Time);
                    timeVariables.Add(variable);
                    timeSparse[classData.Id] = variable;
                }

                if (classData.PossibleRooms.Length > 1)
                {
                    var variable = new Variable(classData.Id, classData.PossibleRooms.Length, VariableType.Room);
                    roomVariables.Add(variable);
                    roomSparse[classData.Id] = variable;
                }
            }

            return (timeVariables.ToArray(), roomVariables.ToArray(), timeSparse, roomSparse);
        }

        private Solution CreateInitialSolution()
        {
            const int chunkSize = 256;
            var hardPenalty = 0;
            var softPenalty = 0;
            var classStates = new ClassState[Classes.Length];
            var studentStates = new StudentState[Students.Length];
            for (var i = 0; i < classStates.Length; i++)
            {
                var classData = Classes[i];
                classStates[i] = new ClassState(
                    classData.PossibleRooms.Length > 0 ? 0 : -1,
                    classData.PossibleSchedules.Length > 0 ? 0 : throw new InvalidOperationException(CorruptInstance),
                    0, 0, 0, 0);
            }

            var totalPenaltyStudent = 0;
            var totalPenaltyTime = 0;
            var totalPenaltyRoom = 0;
            var totalPenaltyDistribution = 0;

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
                        throw new InvalidOperationException(CorruptInstance);
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
                            .WithAttendees(classState.Attendees + 1, 0, 0);
                        var schedule = classData.PossibleSchedules[classState.Time];
                        var room = classState.Room >= 0 ? classData.PossibleRooms[classState.Room].Id : -1;

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
                totalPenaltyStudent += conflicts;
                studentStates[i] = new StudentState(enrollmentStates.ToArray(), conflicts);
            }

            var classConflicts = 0;
            var roomsUnavailable = 0;
            for (var i = 0; i < classStates.Length; i++)
            {
                var state = classStates[i];
                var classData = Classes[i];
                var schedule = classData.PossibleSchedules[state.Time];

                softPenalty += TimePenalty * schedule.Penalty;
                totalPenaltyTime += schedule.Penalty;

                var roomId = -1;
                var roomCapacityPenalty = 0;
                var roomUnavailablePenalty = 0;
                var classCapacityPenalty = state.Attendees > classData.Capacity
                    ? Solution.CapacityOverflowBase + (state.Attendees - classData.Capacity) / Solution.CapacityOverflowRate
                    : 0;
                hardPenalty += classCapacityPenalty;

                if (state.Room >= 0)
                {
                    var roomAssignment = classData.PossibleRooms[state.Room];
                    roomId = roomAssignment.Id;
                    var room = Rooms[roomId];
                    roomCapacityPenalty = state.Attendees > room.Capacity
                        ? Solution.CapacityOverflowBase + (state.Attendees - room.Capacity) / Solution.CapacityOverflowRate
                        : 0;

                    hardPenalty += roomCapacityPenalty;

                    roomUnavailablePenalty = 0;
                    foreach (var unavailableSchedule in room.UnavailableSchedules)
                    {
                        if (schedule.Overlaps(unavailableSchedule))
                        {
                            roomUnavailablePenalty = 1;
                            break;
                        }
                    }

                    hardPenalty += roomUnavailablePenalty;
                    roomsUnavailable += roomUnavailablePenalty;

                    softPenalty += RoomPenalty * roomAssignment.Penalty;
                    totalPenaltyRoom += roomAssignment.Penalty;
                }

                for (var j = 0; j < i; j++)
                {
                    var otherClassState = classStates[j];
                    var otherClassData = Classes[j];
                    if (otherClassState.Room < 0)
                    {
                        continue;
                    }

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
                    classCapacityPenalty,
                    roomCapacityPenalty,
                    roomUnavailablePenalty);
            }

            hardPenalty += classConflicts;

            var constraintStates = new ConstraintState[Constraints.Length];

            var partialSolution = new Solution(
                this,
                hardPenalty,
                softPenalty,
                classConflicts,
                roomsUnavailable,
                new ChunkedArray<ClassState>(classStates, chunkSize),
                new ChunkedArray<StudentState>(studentStates, chunkSize),
                new ChunkedArray<ConstraintState>(constraintStates, chunkSize));

            foreach (var constraint in Constraints)
            {
                var (h, s) = constraint.Evaluate(this, partialSolution);
                var normalized = (constraint.Required ? (double)h : s) /
                                 constraint.WorstCase;
                constraintStates[constraint.Id] = new ConstraintState(h, s, normalized);
                hardPenalty += h;
                softPenalty += s * DistributionPenalty;
                totalPenaltyDistribution += s;
            }

            var initialSolution = new Solution(
                this,
                hardPenalty,
                softPenalty,
                classConflicts,
                roomsUnavailable,
                new ChunkedArray<ClassState>(classStates, chunkSize),
                new ChunkedArray<StudentState>(studentStates, chunkSize),
                new ChunkedArray<ConstraintState>(constraintStates, chunkSize));
            return initialSolution;
        }
    }
}
