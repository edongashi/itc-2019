using System;
using System.Collections.Generic;
using Timetabling.Common.ProblemModel;
using Timetabling.Common.Utils;

namespace Timetabling.Common.SolutionModel
{
    public class Solution
    {
        private const double CapacityOverflowBase = 0.9d;
        private const double CapacityOverflowRate = 0.1d;

        public Solution(
            Problem problem,
            double hardPenalty,
            int softPenalty,
            ChunkedArray<ClassState> classStates,
            ChunkedArray<StudentState> studentStates)
        {
            Problem = problem;
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
            ClassStates = classStates;
            StudentStates = studentStates;
        }

        public readonly Problem Problem;

        public readonly double HardPenalty;

        public readonly int SoftPenalty;

        public readonly ChunkedArray<ClassState> ClassStates;

        public readonly ChunkedArray<StudentState> StudentStates;

        public double NormalizedPenalty => HardPenalty + SoftPenalty / (SoftPenalty + 1d);

        public Solution WithRoom(int @class, int room)
        {
            var classStates = ClassStates;
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
                              - state.RoomHardPenalty
                              - state.CommonHardPenalty
                              - state.RoomCapacityPenalty
                              - state.RoomUnavailablePenalty;
            var softPenalty = SoftPenalty
                              - state.RoomSoftPenalty
                              - state.CommonSoftPenalty
                              - oldRoomAssignment.Penalty * Problem.RoomPenalty;

            var ov = new ClassOverride(@class, room, state.Time);

            // Eval Room constraints of C
            var (roomHardPenalty, roomSoftPenalty) = classData.RoomConstraints.Evaluate(Problem, this, ov);
            hardPenalty += roomHardPenalty;
            softPenalty += Problem.DistributionPenalty * roomSoftPenalty;

            // Eval Common constraints of C
            var (commonHardPenalty, commonSoftPenalty) = classData.CommonConstraints.Evaluate(Problem, this, ov);
            hardPenalty += commonHardPenalty;
            softPenalty += Problem.DistributionPenalty * commonSoftPenalty;

            // Eval new room penalty
            softPenalty += classData.PossibleRooms[room].Penalty * Problem.RoomPenalty;

            // Eval new room capacity
            var newRoom = Problem.Rooms[classData.PossibleRooms[room].Id];
            var roomCapacityPenalty = 0d;
            if (state.Attendees > newRoom.Capacity)
            {
                roomCapacityPenalty += CapacityOverflowBase + (state.Attendees - newRoom.Capacity) * CapacityOverflowRate;
            }

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
            var clashesWithOtherClasses = 0;
            var possibleClasses = oldRoom.PossibleClasses;
            var roomId = oldRoom.Id;
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
                if (cState.Room == -1 || cData.PossibleRooms[cState.Room].Id != roomId)
                {
                    continue;
                }

                if (schedule.Overlaps(cData.PossibleSchedules[cState.Time]))
                {
                    clashesWithOtherClasses--;
                }
            }

            // Eval clashes with other classes
            possibleClasses = newRoom.PossibleClasses;
            roomId = newRoom.Id;
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
                if (cState.Room == -1 || cData.PossibleRooms[cState.Room].Id != roomId)
                {
                    continue;
                }

                if (schedule.Overlaps(cData.PossibleSchedules[cState.Time]))
                {
                    clashesWithOtherClasses++;
                }
            }

            hardPenalty += clashesWithOtherClasses;

            // Eval students of C
            var studentOverrides = new List<Override<StudentState>>();
            var studentClasses = new List<(Schedule schedule, int room)>();
            var courses = Problem.Courses;
            var possibleStudents = courses[classData.CourseId].PossibleStudents;
            var studentStates = StudentStates;
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

                var conflictingPairs = 0;
                var count = 0;
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
                        Schedule currentSchedule;
                        int currentRoom;
                        if (classId == @class)
                        {
                            currentSchedule = schedule;
                            currentRoom = roomId;
                        }
                        else
                        {
                            var classState = classStates[classId];
                            currentSchedule = classObject.PossibleSchedules[classState.Time];
                            currentRoom = classObject.PossibleRooms[classState.Room].Id;
                        }

                        for (var k = 0; k < count; k++)
                        {
                            var (previousSchedule, previousRoom) = studentClasses[k];
                            if (currentSchedule.Overlaps(previousSchedule, travelTimes[previousRoom, currentRoom]))
                            {
                                conflictingPairs++;
                            }
                        }

                        studentClasses.Add((currentSchedule, currentRoom));
                        count++;
                    }
                }

                studentClasses.Clear();
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
                state.TimeHardPenalty,
                state.TimeSoftPenalty,
                commonHardPenalty,
                commonSoftPenalty,
                roomHardPenalty,
                roomSoftPenalty,
                state.ClassCapacityPenalty,
                roomCapacityPenalty,
                roomUnavailablePenalty);

            return new Solution(
                Problem,
                hardPenalty,
                softPenalty,
                classStates.With(new Override<ClassState>(@class, newClassState)),
                studentStates.With(studentOverrides));
        }

        public Solution WithTime(int @class, int time)
        {
            var classStates = ClassStates;
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
                              - state.TimeHardPenalty
                              - state.CommonHardPenalty
                              - state.RoomUnavailablePenalty;
            var softPenalty = SoftPenalty
                              - state.RoomSoftPenalty
                              - state.CommonSoftPenalty
                              - oldSchedule.Penalty * Problem.TimePenalty;

            var ov = new ClassOverride(@class, state.Room, time);

            // Eval Time constraints of C
            var (timeHardPenalty, timeSoftPenalty) = classData.TimeConstraints.Evaluate(Problem, this, ov);
            hardPenalty += timeHardPenalty;
            softPenalty += Problem.DistributionPenalty * timeSoftPenalty;

            // Eval Common constraints of C
            var (commonHardPenalty, commonSoftPenalty) = classData.CommonConstraints.Evaluate(Problem, this, ov);
            hardPenalty += commonHardPenalty;
            softPenalty += Problem.DistributionPenalty * commonSoftPenalty;

            // Eval new time penalty
            var newSchedule = classData.PossibleSchedules[time];
            softPenalty += newSchedule.Penalty * Problem.TimePenalty;

            // Eval room availability at new time
            var roomUnavailablePenalty = 0d;
            var room = Problem.Rooms[classData.PossibleRooms[state.Room].Id];
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
            var clashesWithOtherClasses = 0;
            var possibleClasses = room.PossibleClasses;
            var roomId = room.Id;
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
                if (cState.Room == -1 || cData.PossibleRooms[cState.Room].Id != roomId)
                {
                    continue;
                }

                var cTime = cData.PossibleSchedules[cState.Time];
                if (oldSchedule.Overlaps(cTime))
                {
                    clashesWithOtherClasses--;
                }

                if (newSchedule.Overlaps(cTime))
                {
                    clashesWithOtherClasses++;
                }
            }

            hardPenalty += clashesWithOtherClasses;

            // Eval students of C
            var studentOverrides = new List<Override<StudentState>>();
            var studentClasses = new List<(Schedule schedule, int room)>();
            var courses = Problem.Courses;
            var possibleStudents = courses[classData.CourseId].PossibleStudents;
            var studentStates = StudentStates;
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

                var conflictingPairs = 0;
                var count = 0;
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
                        Schedule currentSchedule;
                        int currentRoom;
                        if (classId == @class)
                        {
                            currentSchedule = newSchedule;
                            currentRoom = roomId;
                        }
                        else
                        {
                            var classState = classStates[classId];
                            currentSchedule = classObject.PossibleSchedules[classState.Time];
                            currentRoom = classObject.PossibleRooms[classState.Room].Id;
                        }

                        for (var k = 0; k < count; k++)
                        {
                            var (previousSchedule, previousRoom) = studentClasses[k];
                            if (currentSchedule.Overlaps(previousSchedule, travelTimes[previousRoom, currentRoom]))
                            {
                                conflictingPairs++;
                            }
                        }

                        studentClasses.Add((currentSchedule, currentRoom));
                        count++;
                    }
                }

                studentClasses.Clear();
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
                timeHardPenalty,
                timeSoftPenalty,
                commonHardPenalty,
                commonSoftPenalty,
                state.RoomHardPenalty,
                state.RoomSoftPenalty,
                state.ClassCapacityPenalty,
                state.RoomCapacityPenalty,
                roomUnavailablePenalty);

            return new Solution(
                Problem,
                hardPenalty,
                softPenalty,
                classStates.With(new Override<ClassState>(@class, newClassState)),
                studentStates.With(studentOverrides));
        }

        public Solution WithEnrollment(int student, int @class)
        {
            var studentStates = StudentStates;
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
            var configIndex = newConfig.ConfigIndex;
            var oldState = states[courseIndex];
            var oldSubparts = oldState.Subparts;
            EnrollmentState newState;
            if (oldState.ConfigIndex == configIndex)
            {
                // Same config
                if (oldSubparts[newConfig.SubpartIndex] == newConfig.ClassIndex)
                {
                    // No change
                    return this;
                }

                newState = new EnrollmentState(configIndex, (int[])oldSubparts.Clone());
            }
            else
            {
                // Different config
                var courseId = studentData.Courses[courseIndex];
                newState = new EnrollmentState(configIndex, (int[])Problem.Courses[courseId].Configurations[configIndex].Baseline.Clone());
            }

            var newSubparts = newState.Subparts;
            var parentId = newClassData.ParentId;
            newSubparts[newConfig.SubpartIndex] = newConfig.ClassIndex;
            // Iterate parent chain
            while (parentId >= 0)
            {
                if (!enrollmentConfigurations.TryGetValue(parentId, out var parentConfig)
                    || parentConfig.CourseIndex != courseIndex || parentConfig.ConfigIndex != configIndex)
                {
                    throw new InvalidOperationException("Corrupt problem instance.");
                }

                newSubparts[parentConfig.SubpartIndex] = parentConfig.ClassIndex;
                var parentClass = classes[parentId];
                parentId = parentClass.ParentId;
            }

            var enrollmentStates = (EnrollmentState[])studentState.EnrollmentStates.Clone();
            enrollmentStates[courseIndex] = newState;

            var hardPenalty = HardPenalty;
            var classStates = ClassStates;
            if (oldState.ConfigIndex == configIndex)
            {
                // Diff only changed classes

                // todo
            }
            else
            {
                // Complete change

                // todo
            }

            var conflictingPairs = studentState.ConflictingPairs;
            var softPenalty = SoftPenalty;

            var count = 0;
            var courses = Problem.Courses;
            var travelTimes = Problem.TravelTimes;
            var studentPenalty = Problem.StudentPenalty;
            var studentClasses = new List<(Schedule schedule, int room)>();
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
                    var classState = classStates[classObject.Id];
                    var currentSchedule = classObject.PossibleSchedules[classState.Time];
                    var currentRoom = classObject.PossibleRooms[classState.Room].Id;

                    for (var k = 0; k < count; k++)
                    {
                        var (previousSchedule, previousRoom) = studentClasses[k];
                        if (currentSchedule.Overlaps(previousSchedule, travelTimes[previousRoom, currentRoom]))
                        {
                            conflictingPairs++;
                        }
                    }

                    studentClasses.Add((currentSchedule, currentRoom));
                    count++;
                }
            }

            var currentConflictingPairs = studentState.ConflictingPairs;
            if (conflictingPairs != currentConflictingPairs)
            {
                softPenalty += (conflictingPairs - currentConflictingPairs) * studentPenalty;
            }

            return new Solution(
                Problem,
                hardPenalty,
                softPenalty,
                classStates,
                studentStates.With(new Override<StudentState>(student, new StudentState(enrollmentStates, conflictingPairs))));
        }
    }
}
