using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Timetabling.Common.ProblemModel;
using Timetabling.Common.Utils;

namespace Timetabling.Common.SolutionModel
{
    public interface ISolution
    {
        ScheduleAssignment GetTime(int @class);

        Room GetRoom(int @class);

        int GetRoomId(int @class);

        Problem Problem { get; }
    }

    public static class SolutionExtensions
    {
        public static int TravelTime(this ISolution solution, int class1, int class2)
        {
            var room1 = solution.GetRoom(class1).Id;
            var room2 = solution.GetRoom(class2).Id;
            return solution.Problem.TravelTimes[room1, room2];
        }
    }

    public class Solution : ISolution
    {
        private struct ClassOverride
        {
            public ClassOverride(int @class, int room, int time)
            {
                Class = @class;
                Room = room;
                Time = time;
            }

            public readonly int Class;

            public readonly int Room;

            public readonly int Time;
        }

        private class SolutionProxy : ISolution
        {
            private readonly Solution inner;
            private readonly ClassOverride classOverride;

            public SolutionProxy(Solution inner, ClassOverride classOverride)
            {
                this.inner = inner;
                this.classOverride = classOverride;
            }

            public ScheduleAssignment GetTime(int @class)
            {
                if (classOverride.Class == @class)
                {
                    return inner.Problem.Classes[@class].PossibleSchedules[classOverride.Time];
                }

                return inner.GetTime(@class);
            }

            public int GetRoomId(int @class)
            {
                if (classOverride.Class == @class)
                {
                    if (classOverride.Room < 0)
                    {
                        return -1;
                    }

                    return inner.Problem.Classes[@class].PossibleRooms[classOverride.Room].Id;
                }

                return inner.GetRoomId(@class);
            }

            public Room GetRoom(int @class)
            {
                if (classOverride.Class == @class)
                {
                    if (classOverride.Room < 0)
                    {
                        return null;
                    }

                    return inner.Problem.Rooms[inner.Problem.Classes[@class].PossibleRooms[classOverride.Room].Id];
                }

                return inner.GetRoom(@class);
            }

            public Problem Problem => inner.Problem;
        }

        private ISolution With(ClassOverride ov)
        {
            return new SolutionProxy(this, ov);
        }

        internal const double CapacityOverflowBase = 0.9d;
        internal const double CapacityOverflowRate = 0.1d;

        private readonly ChunkedArray<ClassState> classStates;
        private readonly ChunkedArray<StudentState> studentStates;
        private readonly ChunkedArray<ConstraintState> constraintStates;

        internal Solution(
            Problem problem,
            double hardPenalty,
            int softPenalty,
            ChunkedArray<ClassState> classStates,
            ChunkedArray<StudentState> studentStates,
            ChunkedArray<ConstraintState> constraintStates)
        {
            Problem = problem;
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
            this.classStates = classStates;
            this.studentStates = studentStates;
            this.constraintStates = constraintStates;
        }

        public readonly Problem Problem;

        public readonly double HardPenalty;

        public readonly int SoftPenalty;

        Problem ISolution.Problem => Problem;

        public double Penalty => HardPenalty + SoftPenalty / (SoftPenalty + 1d);

        public (double hardPenalty, int softPenalty) CalculatePenalty()
        {
            var (distHard, distSoft) = CalculateDistributionPenalty();

            return (distHard,
                TimePenalty() * Problem.TimePenalty
                + RoomPenalty() * Problem.RoomPenalty
                + StudentPenalty() * Problem.StudentPenalty
                + distSoft * Problem.DistributionPenalty);
        }

        public int StudentPenalty()
        {
            var conflicts = 0;
            //for (var i = 0; i < studentStates.Length; i++)
            //{
            //    var state = studentStates[i];
            // TODO
            //}

            return conflicts;
        }

        public int FailedHardConstraints()
        {
            int count = 0;
            foreach (var constraint in Problem.Constraints.Where(c => c.Required))
            {
                if (constraint.Evaluate(this).hardPenalty > 0d)
                {
                    count++;
                }
            }

            return count;
        }

        public int FailedSoftConstraints()
        {
            int count = 0;
            foreach (var constraint in Problem.Constraints.Where(c => !c.Required))
            {
                if (constraint.Evaluate(this).softPenalty > 0)
                {
                    count++;
                }
            }

            return count;
        }

        public int RoomPenalty()
        {
            var roomPenalty = 0;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = classStates[i];
                if (state.Room != -1)
                {
                    roomPenalty += @class.PossibleRooms[state.Room].Penalty;
                }
            }

            return roomPenalty;
        }

        public int TimePenalty()
        {
            var timePenalty = 0;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = classStates[i];
                timePenalty += @class.PossibleSchedules[state.Time].Penalty;
            }

            return timePenalty;
        }

        public int DistributionPenalty()
        {
            return CalculateDistributionPenalty().softPenalty;
        }

        private (double hardPenalty, int softPenalty) CalculateDistributionPenalty()
        {
            var hard = 0d;
            var soft = 0;
            foreach (var constraint in Problem.Constraints)
            {
                var (h, s) = constraint.Evaluate(this);
                hard += h;
                soft += s;
                Console.WriteLine($"{constraint.GetType().Name}->{h}/{s}");
            }

            return (hard, soft);
        }

        public ScheduleAssignment GetTime(int @class)
        {
            return Problem.Classes[@class].PossibleSchedules[classStates[@class].Time];
        }

        public Room GetRoom(int @class)
        {
            var state = classStates[@class];
            if (state.Room < 0)
            {
                return null;
            }

            return Problem.Rooms[Problem.Classes[@class].PossibleRooms[state.Room].Id];
        }

        public int GetRoomId(int @class)
        {
            var room = classStates[@class].Room;
            if (room < 0)
            {
                return -1;
            }

            return Problem.Classes[@class].PossibleRooms[room].Id;
        }

        public Solution WithVariable(int @class, int value, VariableType type)
        {
            switch (type)
            {
                case VariableType.None:
                    return this;
                case VariableType.Time:
                    return WithTime(@class, value);
                case VariableType.Room:
                    return WithRoom(@class, value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public Solution WithRoom(int @class, int room)
        {
            var classStates = this.classStates;
            if (@class < 0 || @class >= classStates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            var classes = Problem.Classes;
            var classData = classes[@class];
            if (room < 0 || room >= classData.PossibleRooms.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(room));
            }

            var state = classStates[@class];
            var oldRoomIndex = state.Room;
            if (room == oldRoomIndex)
            {
                return this;
            }

            var oldRoomAssignment = classData.PossibleRooms[oldRoomIndex];
            var oldRoom = Problem.Rooms[oldRoomAssignment.Id];
            var hardPenalty = HardPenalty
                              - state.RoomCapacityPenalty
                              - state.RoomUnavailablePenalty;
            var softPenalty = SoftPenalty
                              - oldRoomAssignment.Penalty * Problem.RoomPenalty;

            var self = With(new ClassOverride(@class, room, state.Time));

            // ReSharper disable once LocalVariableHidesMember
            var constraintStates = this.constraintStates;

            // Eval Room constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var roomConstraints = classData.RoomConstraints;
            for (var i = 0; i < roomConstraints.Length; i++)
            {
                var roomConstraint = roomConstraints[i];
                var current = constraintStates[roomConstraint.Id];
                var (roomHardPenalty, roomSoftPenalty) = roomConstraint.Evaluate(self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (roomHardPenalty != current.HardPenalty || roomSoftPenalty != current.SoftPenalty)
                {
                    hardPenalty += roomHardPenalty - current.HardPenalty;
                    softPenalty += Problem.DistributionPenalty * (roomSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(roomConstraint.Id,
                            new ConstraintState(roomHardPenalty, roomSoftPenalty)));
                }
            }

            // Eval Common constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var commonConstraints = classData.CommonConstraints;
            for (var i = 0; i < commonConstraints.Length; i++)
            {
                var commonConstraint = commonConstraints[i];
                var current = constraintStates[commonConstraint.Id];
                var (commonHardPenalty, commonSoftPenalty) = commonConstraint.Evaluate(self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (commonHardPenalty != current.HardPenalty || commonSoftPenalty != current.SoftPenalty)
                {
                    hardPenalty += commonHardPenalty - current.HardPenalty;
                    softPenalty += Problem.DistributionPenalty * (commonSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(commonConstraint.Id,
                            new ConstraintState(commonHardPenalty, commonSoftPenalty)));
                }
            }

            // Eval new room penalty
            softPenalty += classData.PossibleRooms[room].Penalty * Problem.RoomPenalty;

            // Eval new room capacity
            var newRoom = Problem.Rooms[classData.PossibleRooms[room].Id];
            var roomCapacityPenalty = state.Attendees > newRoom.Capacity
                ? CapacityOverflowBase + (state.Attendees - newRoom.Capacity) * CapacityOverflowRate
                : 0d;

            hardPenalty += roomCapacityPenalty;

            // Eval new room availability at time
            var roomUnavailablePenalty = 0d;
            var schedule = classData.PossibleSchedules[state.Time];
            var unavailableSchedules = newRoom.UnavailableSchedules;
            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < unavailableSchedules.Length; i++)
            {
                var unavailableSchedule = unavailableSchedules[i];
                if (schedule.Overlaps(unavailableSchedule))
                {
                    roomUnavailablePenalty = 1d;
                    break;
                }
            }

            hardPenalty += roomUnavailablePenalty;

            // Cleanup clashes in previous room
            var classConflicts = 0;
            var possibleClasses = oldRoom.PossibleClasses;
            var oldRoomId = oldRoom.Id;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < possibleClasses.Length; i++)
            {
                var c = possibleClasses[i];
                if (c == @class)
                {
                    continue;
                }

                var cData = classes[c];
                var cState = classStates[c];
                if (cData.PossibleRooms[cState.Room].Id != oldRoomId)
                {
                    continue;
                }

                if (schedule.Overlaps(cData.PossibleSchedules[cState.Time]))
                {
                    classConflicts--;
                }
            }

            // Eval clashes with other classes
            possibleClasses = newRoom.PossibleClasses;
            var newRoomId = newRoom.Id;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < possibleClasses.Length; i++)
            {
                var c = possibleClasses[i];
                if (c == @class)
                {
                    continue;
                }

                var cData = classes[c];
                var cState = classStates[c];
                if (cData.PossibleRooms[cState.Room].Id != newRoomId)
                {
                    continue;
                }

                if (schedule.Overlaps(cData.PossibleSchedules[cState.Time]))
                {
                    classConflicts++;
                }
            }

            hardPenalty += classConflicts;

            // Eval students of C
            var studentOverrides = new List<Override<StudentState>>();
            var courses = Problem.Courses;
            var possibleStudents = courses[classData.CourseId].PossibleStudents;
            var studentStates = this.studentStates;
            var students = Problem.Students;
            var travelTimes = Problem.TravelTimes;
            var studentPenalty = Problem.StudentPenalty;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var s = 0; s < possibleStudents.Length; s++)
            {
                var student = possibleStudents[s];
                var studentState = studentStates[student];
                if (studentState.SingleClass)
                {
                    continue;
                }

                var studentData = students[student];
                var enrollmentStates = studentState.EnrollmentStates;

                // Try short circuiting
                {
                    var enrollmentConfiguration = studentData.EnrollmentConfigurations[@class];
                    var classEnrollmentState = enrollmentStates[enrollmentConfiguration.CourseIndex];
                    if (classEnrollmentState.ConfigIndex != enrollmentConfiguration.ConfigIndex)
                    {
                        // Not assigned to class config
                        continue;
                    }

                    if (classEnrollmentState.Subparts[enrollmentConfiguration.SubpartIndex] !=
                        enrollmentConfiguration.ClassIndex)
                    {
                        // Not assigned to class
                        continue;
                    }
                }

                var conflictingPairs = studentState.ConflictingPairs;
                for (var i = 0; i < enrollmentStates.Length; i++)
                {
                    var enrollmentState = enrollmentStates[i];
                    var courseId = studentData.Courses[i];
                    var subparts = courses[courseId]
                        .Configurations[enrollmentState.ConfigIndex]
                        .Subparts;

                    var classIndexes = enrollmentState.Subparts;
                    for (var j = 0; j < classIndexes.Length; j++)
                    {
                        var classObject = subparts[j].Classes[classIndexes[j]];
                        var classId = classObject.Id;
                        if (classId == @class)
                        {
                            continue;
                        }

                        var classState = classStates[classId];
                        var cSchedule = classObject.PossibleSchedules[classState.Time];
                        var cRoom = classState.Room;
                        int travelTimeOld;
                        int travelTimeNew;
                        if (cRoom < 0)
                        {
                            travelTimeOld = 0;
                            travelTimeNew = 0;
                        }
                        else
                        {
                            var cRoomId = classObject.PossibleRooms[cRoom].Id;
                            travelTimeOld = travelTimes[cRoomId, oldRoomId];
                            travelTimeNew = travelTimes[cRoomId, newRoomId];
                        }

                        if (schedule.Overlaps(cSchedule, travelTimeOld))
                        {
                            conflictingPairs--;
                        }

                        if (schedule.Overlaps(cSchedule, travelTimeNew))
                        {
                            conflictingPairs++;
                        }
                    }
                }

                var currentConflictingPairs = studentState.ConflictingPairs;
                if (conflictingPairs != currentConflictingPairs)
                {
                    softPenalty += (conflictingPairs - currentConflictingPairs) * studentPenalty;
                    studentOverrides.Add(new Override<StudentState>(student, new StudentState(enrollmentStates, conflictingPairs)));
                }
            }

            var newClassState = new ClassState(
                room,
                state.Time,
                state.Attendees,
                state.ClassCapacityPenalty,
                roomCapacityPenalty,
                roomUnavailablePenalty);

            return new Solution(
                Problem,
                hardPenalty,
                softPenalty,
                classStates.With(new Override<ClassState>(@class, newClassState)),
                studentStates.With(studentOverrides),
                constraintStates);
        }

        public Solution WithTime(int @class, int time)
        {
            var classStates = this.classStates;
            if (@class < 0 || @class >= classStates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            var classes = Problem.Classes;
            var classData = classes[@class];
            if (time < 0 || time >= classData.PossibleSchedules.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(time));
            }

            var state = classStates[@class];
            var oldScheduleIndex = state.Time;
            if (time == oldScheduleIndex)
            {
                return this;
            }

            var oldSchedule = classData.PossibleSchedules[oldScheduleIndex];
            var hardPenalty = HardPenalty
                              - state.RoomUnavailablePenalty;
            var softPenalty = SoftPenalty
                              - oldSchedule.Penalty * Problem.TimePenalty;

            var self = With(new ClassOverride(@class, state.Room, time));

            // ReSharper disable once LocalVariableHidesMember
            var constraintStates = this.constraintStates;

            // Eval Time constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var timeConstraints = classData.TimeConstraints;
            for (var i = 0; i < timeConstraints.Length; i++)
            {
                var timeConstraint = timeConstraints[i];
                var current = constraintStates[timeConstraint.Id];
                var (timeHardPenalty, timeSoftPenalty) = timeConstraint.Evaluate(self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (timeHardPenalty != current.HardPenalty || timeSoftPenalty != current.SoftPenalty)
                {
                    hardPenalty += timeHardPenalty - current.HardPenalty;
                    softPenalty += Problem.DistributionPenalty * (timeSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(timeConstraint.Id,
                            new ConstraintState(timeHardPenalty, timeSoftPenalty)));
                }
            }

            // Eval Common constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var commonConstraints = classData.CommonConstraints;
            for (var i = 0; i < commonConstraints.Length; i++)
            {
                var commonConstraint = commonConstraints[i];
                var current = constraintStates[commonConstraint.Id];
                var (commonHardPenalty, commonSoftPenalty) = commonConstraint.Evaluate(self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (commonHardPenalty != current.HardPenalty || commonSoftPenalty != current.SoftPenalty)
                {
                    hardPenalty += commonHardPenalty - current.HardPenalty;
                    softPenalty += Problem.DistributionPenalty * (commonSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(commonConstraint.Id,
                            new ConstraintState(commonHardPenalty, commonSoftPenalty)));
                }
            }

            // Eval new time penalty
            var newSchedule = classData.PossibleSchedules[time];
            softPenalty += newSchedule.Penalty * Problem.TimePenalty;

            // Eval room availability at new time
            var roomUnavailablePenalty = 0d;
            var roomId = -1;
            if (state.Room >= 0)
            {
                roomId = classData.PossibleRooms[state.Room].Id;
                var room = Problem.Rooms[roomId];
                var unavailableSchedules = room.UnavailableSchedules;
                // ReSharper disable once LoopCanBeConvertedToQuery
                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < unavailableSchedules.Length; i++)
                {
                    var unavailableSchedule = unavailableSchedules[i];
                    if (newSchedule.Overlaps(unavailableSchedule))
                    {
                        roomUnavailablePenalty = 1d;
                        break;
                    }
                }

                hardPenalty += roomUnavailablePenalty;

                // Cleanup clashes in previous room
                // Eval clashes with other classes
                var classConflicts = 0;
                var possibleClasses = room.PossibleClasses;
                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < possibleClasses.Length; i++)
                {
                    var c = possibleClasses[i];
                    if (c == @class)
                    {
                        continue;
                    }

                    var cData = classes[c];
                    var cState = classStates[c];
                    if (cData.PossibleRooms[cState.Room].Id != roomId)
                    {
                        continue;
                    }

                    var cTime = cData.PossibleSchedules[cState.Time];
                    if (oldSchedule.Overlaps(cTime))
                    {
                        classConflicts--;
                    }

                    if (newSchedule.Overlaps(cTime))
                    {
                        classConflicts++;
                    }
                }

                hardPenalty += classConflicts;
            }

            // Eval students of C
            var studentOverrides = new List<Override<StudentState>>();
            var courses = Problem.Courses;
            var possibleStudents = courses[classData.CourseId].PossibleStudents;
            var studentStates = this.studentStates;
            var students = Problem.Students;
            var travelTimes = Problem.TravelTimes;
            var studentPenalty = Problem.StudentPenalty;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var s = 0; s < possibleStudents.Length; s++)
            {
                var student = possibleStudents[s];
                var studentState = studentStates[student];
                if (studentState.SingleClass)
                {
                    continue;
                }

                var studentData = students[student];
                var enrollmentStates = studentState.EnrollmentStates;

                // Try short circuiting
                {
                    var enrollmentConfiguration = studentData.EnrollmentConfigurations[@class];
                    var classEnrollmentState = enrollmentStates[enrollmentConfiguration.CourseIndex];
                    if (classEnrollmentState.ConfigIndex != enrollmentConfiguration.ConfigIndex)
                    {
                        // Not assigned to class config
                        continue;
                    }

                    if (classEnrollmentState.Subparts[enrollmentConfiguration.SubpartIndex] !=
                        enrollmentConfiguration.ClassIndex)
                    {
                        // Not assigned to class
                        continue;
                    }
                }

                var conflictingPairs = studentState.ConflictingPairs;
                for (var i = 0; i < enrollmentStates.Length; i++)
                {
                    var enrollmentState = enrollmentStates[i];
                    var courseId = studentData.Courses[i];
                    var subparts = courses[courseId]
                        .Configurations[enrollmentState.ConfigIndex]
                        .Subparts;

                    var classIndexes = enrollmentState.Subparts;
                    for (var j = 0; j < classIndexes.Length; j++)
                    {
                        var classObject = subparts[j].Classes[classIndexes[j]];
                        var classId = classObject.Id;
                        if (classId == @class)
                        {
                            continue;
                        }

                        var classState = classStates[classId];
                        var cSchedule = classObject.PossibleSchedules[classState.Time];
                        var cRoom = classState.Room;
                        int travelTime;
                        if (cRoom < 0 || roomId < 0)
                        {
                            travelTime = 0;
                        }
                        else
                        {
                            travelTime = travelTimes[roomId, classObject.PossibleRooms[cRoom].Id];
                        }

                        if (oldSchedule.Overlaps(cSchedule, travelTime))
                        {
                            conflictingPairs--;
                        }

                        if (newSchedule.Overlaps(cSchedule, travelTime))
                        {
                            conflictingPairs++;
                        }
                    }
                }

                var currentConflictingPairs = studentState.ConflictingPairs;
                if (conflictingPairs != currentConflictingPairs)
                {
                    softPenalty += (conflictingPairs - currentConflictingPairs) * studentPenalty;
                    studentOverrides.Add(new Override<StudentState>(student, new StudentState(enrollmentStates, conflictingPairs)));
                }
            }

            var newClassState = new ClassState(
                state.Room,
                time,
                state.Attendees,
                state.ClassCapacityPenalty,
                state.RoomCapacityPenalty,
                roomUnavailablePenalty);

            return new Solution(
                Problem,
                hardPenalty,
                softPenalty,
                classStates.With(new Override<ClassState>(@class, newClassState)),
                studentStates.With(studentOverrides),
                constraintStates);
        }

        public Solution WithEnrollment(int student, int @class)
        {
            var studentStates = this.studentStates;
            var studentState = studentStates[student];
            var studentData = Problem.Students[student];
            var enrollmentConfigurations = studentData.EnrollmentConfigurations;
            if (!enrollmentConfigurations.TryGetValue(@class, out var newConfig))
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            var classes = Problem.Classes;
            var newClassData = classes[@class];
            if (newClassData.HasChildren)
            {
                throw new InvalidOperationException("Only loose classes can be assigned directly.");
            }

            var states = studentState.EnrollmentStates;
            var courseIndex = newConfig.CourseIndex;
            var newConfigIndex = newConfig.ConfigIndex;
            var oldState = states[courseIndex];
            var oldConfigIndex = oldState.ConfigIndex;
            var oldSubparts = oldState.Subparts;
            EnrollmentState newState;
            if (oldConfigIndex == newConfigIndex)
            {
                // Same config
                if (oldSubparts[newConfig.SubpartIndex] == newConfig.ClassIndex)
                {
                    // No change
                    return this;
                }

                newState = new EnrollmentState(newConfigIndex, (int[])oldSubparts.Clone());
            }
            else
            {
                // Different config
                var courseId = studentData.Courses[courseIndex];
                newState = new EnrollmentState(newConfigIndex, (int[])Problem.Courses[courseId].Configurations[newConfigIndex].Baseline.Clone());
            }

            var newSubparts = newState.Subparts;
            var parentId = newClassData.ParentId;
            newSubparts[newConfig.SubpartIndex] = newConfig.ClassIndex;
            var updates = 1;
            // Iterate parent chain
            while (parentId >= 0)
            {
                if (!enrollmentConfigurations.TryGetValue(parentId, out var parentConfig)
                    || parentConfig.CourseIndex != courseIndex || parentConfig.ConfigIndex != newConfigIndex)
                {
                    throw new InvalidOperationException("Corrupt problem instance.");
                }

                newSubparts[parentConfig.SubpartIndex] = parentConfig.ClassIndex;
                var parentClass = classes[parentId];
                parentId = parentClass.ParentId;
                updates++;
            }

            var enrollmentStates = (EnrollmentState[])studentState.EnrollmentStates.Clone();
            enrollmentStates[courseIndex] = newState;

            var hardPenalty = HardPenalty;
            var classStates = this.classStates;
            var courses = Problem.Courses;
            var rooms = Problem.Rooms;
            var studentCourses = studentData.Courses;
            var classUpdates = new List<Override<ClassState>>();
            var oldRoomId = -1;
            var newRoomId = -1;
            Schedule oldSchedule = null;
            Schedule newSchedule = null;
            if (oldConfigIndex == newConfigIndex)
            {
                var courseId = studentCourses[courseIndex];
                var subparts = courses[courseId]
                    .Configurations[newConfigIndex]
                    .Subparts;

                // Diff only changed classes
                for (var i = 0; i < oldSubparts.Length; i++)
                {
                    var oldClassIndex = oldSubparts[i];
                    var newClassIndex = newSubparts[i];
                    if (oldClassIndex == newClassIndex)
                    {
                        continue;
                    }

                    var subpart = subparts[i];

                    // Unenrolled class
                    var oldClass = subpart.Classes[oldClassIndex];
                    var oldClassId = oldClass.Id;
                    var oldClassState = classStates[oldClassId];
                    var oldRoom = oldClassState.Room;
                    var oldAttendees = oldClassState.Attendees - 1;
                    double oldRoomCapacityPenalty;
                    if (oldRoom >= 0)
                    {
                        oldRoomId = oldClass.PossibleRooms[oldClassState.Room].Id;
                        var oldRoomCapacity = rooms[oldRoomId].Capacity;
                        oldRoomCapacityPenalty = oldAttendees > oldRoomCapacity
                            ? CapacityOverflowBase + (oldAttendees - oldRoomCapacity) * CapacityOverflowRate
                            : 0d;
                        hardPenalty += oldRoomCapacityPenalty;
                    }
                    else
                    {
                        oldRoomCapacityPenalty = 0d;
                    }

                    oldSchedule = oldClass.PossibleSchedules[oldClassState.Time];
                    var oldClassCapacity = oldClass.Capacity;
                    hardPenalty -= oldClassState.RoomCapacityPenalty;
                    hardPenalty -= oldClassState.ClassCapacityPenalty;
                    var oldClassCapacityPenalty = oldAttendees > oldClassCapacity
                        ? CapacityOverflowBase + (oldAttendees - oldClassCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(oldClassId,
                        oldClassState.WithAttendees(oldAttendees, oldClassCapacityPenalty, oldRoomCapacityPenalty)));
                    hardPenalty += oldClassCapacityPenalty;

                    // Enrolled class
                    var newClass = subpart.Classes[newClassIndex];
                    var newClassId = newClass.Id;
                    var newClassState = classStates[newClassId];
                    var newRoom = newClassState.Room;
                    var newAttendees = newClassState.Attendees + 1;
                    double newRoomCapacityPenalty;
                    if (newRoom >= 0)
                    {
                        newRoomId = newClass.PossibleRooms[newClassState.Room].Id;
                        var newRoomCapacity = rooms[newRoomId].Capacity;
                        newRoomCapacityPenalty = newAttendees > newRoomCapacity
                            ? CapacityOverflowBase + (newAttendees - newRoomCapacity) * CapacityOverflowRate
                            : 0d;
                        hardPenalty += newRoomCapacityPenalty;
                    }
                    else
                    {
                        newRoomCapacityPenalty = 0d;
                    }

                    newSchedule = newClass.PossibleSchedules[newClassState.Time];
                    var newClassCapacity = newClass.Capacity;
                    hardPenalty -= newClassState.RoomCapacityPenalty;
                    hardPenalty -= newClassState.ClassCapacityPenalty;
                    var newClassCapacityPenalty = newAttendees > newClassCapacity
                        ? CapacityOverflowBase + (newAttendees - newClassCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(newClassId,
                        newClassState.WithAttendees(newAttendees, newClassCapacityPenalty, newRoomCapacityPenalty)));
                    hardPenalty += newClassCapacityPenalty;
                }
            }
            else
            {
                var courseId = studentCourses[courseIndex];
                var configurations = courses[courseId].Configurations;
                var oldSubpartObjects = configurations[oldConfigIndex].Subparts;
                var newSubpartObjects = configurations[newConfigIndex].Subparts;

                for (var i = 0; i < oldSubpartObjects.Length; i++)
                {
                    var subpart = oldSubpartObjects[i];
                    var oldClassIndex = oldSubparts[i];

                    // Unenrolled class
                    var oldClass = subpart.Classes[oldClassIndex];
                    var oldClassId = oldClass.Id;
                    var oldClassState = classStates[oldClassId];
                    var oldRoom = oldClassState.Room;
                    var oldAttendees = oldClassState.Attendees - 1;
                    double oldRoomCapacityPenalty;
                    if (oldRoom >= 0)
                    {
                        oldRoomId = oldClass.PossibleRooms[oldClassState.Room].Id;
                        var oldRoomCapacity = rooms[oldRoomId].Capacity;
                        oldRoomCapacityPenalty = oldAttendees > oldRoomCapacity
                            ? CapacityOverflowBase + (oldAttendees - oldRoomCapacity) * CapacityOverflowRate
                            : 0d;
                        hardPenalty += oldRoomCapacityPenalty;
                    }
                    else
                    {
                        oldRoomCapacityPenalty = 0d;
                    }

                    oldSchedule = oldClass.PossibleSchedules[oldClassState.Time];
                    var oldClassCapacity = oldClass.Capacity;
                    hardPenalty -= oldClassState.RoomCapacityPenalty;
                    hardPenalty -= oldClassState.ClassCapacityPenalty;
                    var oldClassCapacityPenalty = oldAttendees > oldClassCapacity
                        ? CapacityOverflowBase + (oldAttendees - oldClassCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(oldClassId,
                        oldClassState.WithAttendees(oldAttendees, oldClassCapacityPenalty, oldRoomCapacityPenalty)));
                    hardPenalty += oldClassCapacityPenalty;
                }

                for (var i = 0; i < oldSubpartObjects.Length; i++)
                {
                    var subpart = newSubpartObjects[i];
                    var newClassIndex = newSubparts[i];

                    // Enrolled class
                    var newClass = subpart.Classes[newClassIndex];
                    var newClassId = newClass.Id;
                    var newClassState = classStates[newClassId];
                    var newRoom = newClassState.Room;
                    var newAttendees = newClassState.Attendees + 1;
                    double newRoomCapacityPenalty;
                    if (newRoom >= 0)
                    {
                        newRoomId = newClass.PossibleRooms[newClassState.Room].Id;
                        var newRoomCapacity = rooms[newRoomId].Capacity;
                        newRoomCapacityPenalty = newAttendees > newRoomCapacity
                            ? CapacityOverflowBase + (newAttendees - newRoomCapacity) * CapacityOverflowRate
                            : 0d;
                        hardPenalty += newRoomCapacityPenalty;
                    }
                    else
                    {
                        newRoomCapacityPenalty = 0d;
                    }

                    newSchedule = newClass.PossibleSchedules[newClassState.Time];
                    var newClassCapacity = newClass.Capacity;
                    hardPenalty -= newClassState.RoomCapacityPenalty;
                    hardPenalty -= newClassState.ClassCapacityPenalty;
                    var newClassCapacityPenalty = newAttendees > newClassCapacity
                        ? CapacityOverflowBase + (newAttendees - newClassCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(newClassId,
                        newClassState.WithAttendees(newAttendees, newClassCapacityPenalty, newRoomCapacityPenalty)));
                    hardPenalty += newClassCapacityPenalty;
                }
            }

            var travelTimes = Problem.TravelTimes;

            int conflictingPairs;
            if (oldConfigIndex == newConfigIndex && updates == 1)
            {
                // Single update, O(n) operation
                conflictingPairs = studentState.ConflictingPairs;
                for (var i = 0; i < enrollmentStates.Length; i++)
                {
                    var enrollmentState = enrollmentStates[i];
                    var courseId = studentData.Courses[i];
                    var subparts = courses[courseId]
                        .Configurations[enrollmentState.ConfigIndex]
                        .Subparts;

                    var classIndexes = enrollmentState.Subparts;
                    for (var j = 0; j < classIndexes.Length; j++)
                    {
                        var classObject = subparts[j].Classes[classIndexes[j]];
                        var classId = classObject.Id;
                        if (classId == @class)
                        {
                            continue;
                        }

                        var classState = classStates[classId];
                        var cSchedule = classObject.PossibleSchedules[classState.Time];
                        var cRoom = classState.Room;

                        int oldTravelTime;
                        int newTravelTime;
                        if (cRoom >= 0)
                        {
                            var cRoomId = classObject.PossibleRooms[classState.Room].Id;
                            oldTravelTime = oldRoomId >= 0 ? travelTimes[oldRoomId, cRoomId] : 0;
                            newTravelTime = newRoomId >= 0 ? travelTimes[newRoomId, cRoomId] : 0;
                        }
                        else
                        {
                            oldTravelTime = 0;
                            newTravelTime = 0;
                        }

                        // ReSharper disable PossibleNullReferenceException
                        if (oldSchedule.Overlaps(cSchedule, oldTravelTime))
                        {
                            conflictingPairs--;
                        }

                        if (newSchedule.Overlaps(cSchedule, newTravelTime))
                        {
                            conflictingPairs++;
                        }
                        // ReSharper restore PossibleNullReferenceException
                    }
                }
            }
            else
            {
                // Multiple updates, O(n²) operation

                // Todo: iterate without allocation
                var count = 0;
                var studentClasses = new List<(Schedule schedule, int roomId)>();
                conflictingPairs = 0;
                for (var i = 0; i < enrollmentStates.Length; i++)
                {
                    var enrollmentState = enrollmentStates[i];
                    var courseId = studentCourses[i];
                    var subparts = courses[courseId]
                        .Configurations[enrollmentState.ConfigIndex]
                        .Subparts;

                    var classIndexes = enrollmentState.Subparts;
                    for (var j = 0; j < classIndexes.Length; j++)
                    {
                        var classObject = subparts[j].Classes[classIndexes[j]];
                        var classState = classStates[classObject.Id];
                        var currentSchedule = classObject.PossibleSchedules[classState.Time];
                        var currentRoom = classState.Room;
                        var currentRoomId = currentRoom >= 0
                            ? classObject.PossibleRooms[currentRoom].Id
                            : -1;

                        for (var k = 0; k < count; k++)
                        {
                            var (previousSchedule, previousRoomId) = studentClasses[k];
                            var travelTime = currentRoomId >= 0 && previousRoomId >= 0
                                ? travelTimes[currentRoomId, previousRoomId]
                                : 0;
                            if (currentSchedule.Overlaps(previousSchedule, travelTime))
                            {
                                conflictingPairs++;
                            }
                        }

                        studentClasses.Add((currentSchedule, currentRoomId));
                        count++;
                    }
                }
            }

            var softPenalty = SoftPenalty;
            var currentConflictingPairs = studentState.ConflictingPairs;
            if (conflictingPairs != currentConflictingPairs)
            {
                softPenalty += (conflictingPairs - currentConflictingPairs) * Problem.StudentPenalty;
            }

            return new Solution(
                Problem,
                hardPenalty,
                softPenalty,
                classStates.With(classUpdates),
                studentStates.With(new Override<StudentState>(student, new StudentState(enrollmentStates, conflictingPairs))),
                constraintStates);
        }

        public bool HasClass(int student, int @class)
        {
            var state = studentStates[student];
            var studentData = Problem.Students[student];
            if (studentData.EnrollmentConfigurations.TryGetValue(@class, out var config))
            {
                var courseState = state.EnrollmentStates[config.CourseIndex];
                if (config.ConfigIndex != courseState.ConfigIndex)
                {
                    return false;
                }

                return courseState.Subparts[config.SubpartIndex] == config.ClassIndex;
            }

            return false;
        }

        public Solution RandomizedTimeClimb(int[] classes, Random random, Func<Solution, bool> constraint)
        {
            return RandomizedClimb(classes, random, VariableType.Time, constraint);
        }

        public Solution RandomizedRoomClimb(int[] classes, Random random, Func<Solution, bool> constraint)
        {
            return RandomizedClimb(classes, random, VariableType.Room, constraint);
        }

        private Solution RandomizedClimb(int[] classes, Random random, VariableType type, Func<Solution, bool> constraint)
        {
            if (classes.Length == 0)
            {
                return this;
            }

            int maxVariableLength;
            switch (classes.Length)
            {
                case 0:
                case 1:
                    maxVariableLength = 20;
                    break;
                case 2:
                    maxVariableLength = 8;
                    break;
                case 3:
                    maxVariableLength = 6;
                    break;
                case 4:
                    maxVariableLength = 3;
                    break;
                case 5:
                    maxVariableLength = 2;
                    break;
                case 6:
                    maxVariableLength = 2;
                    break;
                default:
                    maxVariableLength = 2;
                    break;
            }

            var sparseVariables = type == VariableType.Room ? Problem.RoomVariablesSparse : Problem.TimeVariablesSparse;
            var max = classes.Length - 1;

            var variables = new int[classes.Length][];

            var currentPenalty = Penalty;
            var bestQuality = this;
            for (var i = 0; i < classes.Length; i++)
            {
                var variable = sparseVariables[classes[i]].Shuffle(random);
                variables[i] = variable;
                if (variable.Length > 0)
                {
                    bestQuality = bestQuality.WithVariable(classes[i], variable[0], type);
                }
            }

            Solution bestSatisfied = null;
            if (constraint(bestQuality))
            {
                bestSatisfied = bestQuality;
            }

            bool Climb(Solution current, int index)
            {
                var variable = variables[index];
                var variableLength = Math.Min(variable.Length, maxVariableLength);
                if (variableLength > 0)
                {
                    var @class = classes[index];
                    for (var i = 0; i < variableLength; i++)
                    {
                        var value = variable[i];
                        var candidate = current.WithVariable(@class, value, type);

                        if (constraint(candidate))
                        {
                            if (bestSatisfied == null)
                            {
                                bestSatisfied = candidate;
                            }
                            else if (candidate.Penalty <= bestSatisfied.Penalty)
                            {
                                bestSatisfied = candidate;
                            }

                            if (bestSatisfied.Penalty < currentPenalty)
                            {
                                return true;
                            }
                        }

                        if (candidate.Penalty <= bestQuality.Penalty)
                        {
                            bestQuality = candidate;
                        }

                        if (index < max)
                        {
                            if (Climb(candidate, index + 1))
                            {
                                return true;
                            }
                        }
                    }
                }
                else if (index < max)
                {
                    if (Climb(current, index + 1))
                    {
                        return true;
                    }
                }

                return false;
            }

            Climb(bestQuality, 0);
            return bestSatisfied ?? bestQuality;
        }

        public XElement Serialize()
        {
            return Serialize(0d, 1, "", "", "", "");
        }

        public XElement Serialize(
            double runtime,
            int cores,
            string technique,
            string author,
            string institution,
            string country)
        {
            var result = new XElement("solution",
                new XAttribute("name", Problem.Name),
                new XAttribute("runtime", runtime.ToString("0.00")),
                new XAttribute("cores", cores.ToString()),
                new XAttribute("technique", technique),
                new XAttribute("author", author),
                new XAttribute("institution", institution),
                new XAttribute("country", country));

            for (var i = 0; i < classStates.Length; i++)
            {
                var state = classStates[i];
                var time = GetTime(i);
                var classElement = new XElement("class",
                    new XAttribute("id", (i + 1).ToString()),
                    new XAttribute("days", time.Days.ToBinary(Problem.DaysPerWeek)),
                    new XAttribute("start", time.Start.ToString()),
                    new XAttribute("weeks", time.Weeks.ToBinary(Problem.NumberOfWeeks)));
                if (state.Room >= 0)
                {
                    var room = GetRoom(i);
                    classElement.Add(new XAttribute("room", room.Id + 1));
                }

                for (var s = 0; s < studentStates.Length; s++)
                {
                    if (HasClass(s, i))
                    {
                        classElement.Add(
                            new XElement("student",
                                new XAttribute("id", (s + 1).ToString())));
                    }
                }

                result.Add(classElement);
            }

            return result;
        }
    }
}
