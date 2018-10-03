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
            var clashesWithOtherClasses = 0;
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
                if (cState.Room == -1 || cData.PossibleRooms[cState.Room].Id != oldRoomId)
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
                if (cState.Room == -1 || cData.PossibleRooms[cState.Room].Id != newRoomId)
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
                        var cRoomId = classObject.PossibleRooms[classState.Room].Id;
                        if (schedule.Overlaps(cSchedule, travelTimes[cRoomId, oldRoomId]))
                        {
                            conflictingPairs--;
                        }

                        if (schedule.Overlaps(cSchedule, travelTimes[cRoomId, newRoomId]))
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
                        var travelTime = travelTimes[roomId, classObject.PossibleRooms[classState.Room].Id];
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
            }

            var enrollmentStates = (EnrollmentState[])studentState.EnrollmentStates.Clone();
            enrollmentStates[courseIndex] = newState;

            var hardPenalty = HardPenalty;
            var classStates = ClassStates;
            var courses = Problem.Courses;
            var rooms = Problem.Rooms;
            var studentCourses = studentData.Courses;
            var classUpdates = new List<Override<ClassState>>();
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
                    var oldClassObject = subpart.Classes[oldClassIndex];
                    var oldClassId = oldClassObject.Id;
                    var oldClassState = classStates[oldClassId];
                    var oldRoomCapacity = rooms[oldClassObject.PossibleRooms[oldClassState.Room].Id].Capacity;
                    var oldClassCapacity = oldClassObject.Capacity;
                    var oldAttendees = oldClassState.Attendees - 1;
                    hardPenalty -= oldClassState.RoomCapacityPenalty;
                    hardPenalty -= oldClassState.ClassCapacityPenalty;
                    var oldRoomCapacityPenalty = oldAttendees > oldRoomCapacity
                        ? CapacityOverflowBase + (oldAttendees - oldRoomCapacity) * CapacityOverflowRate
                        : 0d;
                    var oldClassCapacityPenalty = oldAttendees > oldClassCapacity
                        ? CapacityOverflowBase + (oldAttendees - oldClassCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(oldClassId,
                        oldClassState.WithAttendees(oldAttendees, oldClassCapacityPenalty, oldRoomCapacityPenalty)));
                    hardPenalty += oldRoomCapacityPenalty;
                    hardPenalty += oldClassCapacityPenalty;

                    // Enrolled class
                    var newClassObject = subpart.Classes[newClassIndex];
                    var newClassId = newClassObject.Id;
                    var newClassState = classStates[newClassId];
                    var newRoomCapacity = rooms[newClassObject.PossibleRooms[newClassState.Room].Id].Capacity;
                    var newClassCapacity = newClassObject.Capacity;
                    var newAttendees = newClassState.Attendees + 1;
                    hardPenalty -= newClassState.RoomCapacityPenalty;
                    hardPenalty -= newClassState.ClassCapacityPenalty;
                    var newRoomCapacityPenalty = newAttendees > newRoomCapacity
                        ? CapacityOverflowBase + (newAttendees - newRoomCapacity) * CapacityOverflowRate
                        : 0d;
                    var newClassCapacityPenalty = newAttendees > newClassCapacity
                        ? CapacityOverflowBase + (newAttendees - newClassCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(newClassId,
                        newClassState.WithAttendees(newAttendees, newClassCapacityPenalty, newRoomCapacityPenalty)));
                    hardPenalty += newRoomCapacityPenalty;
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
                    var classIndex = oldSubparts[i];

                    // Unenrolled class
                    var classObject = subpart.Classes[classIndex];
                    var classId = classObject.Id;
                    var classState = classStates[classId];
                    var roomCapacity = rooms[classObject.PossibleRooms[classState.Room].Id].Capacity;
                    var classCapacity = classObject.Capacity;
                    var attendees = classState.Attendees - 1;
                    hardPenalty -= classState.RoomCapacityPenalty;
                    hardPenalty -= classState.ClassCapacityPenalty;
                    var roomCapacityPenalty = attendees > roomCapacity
                        ? CapacityOverflowBase + (attendees - roomCapacity) * CapacityOverflowRate
                        : 0d;
                    var classCapacityPenalty = attendees > classCapacity
                        ? CapacityOverflowBase + (attendees - classCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(classId,
                        classState.WithAttendees(attendees, classCapacityPenalty, roomCapacityPenalty)));
                    hardPenalty += roomCapacityPenalty;
                    hardPenalty += classCapacityPenalty;
                }

                for (var i = 0; i < oldSubpartObjects.Length; i++)
                {
                    var subpart = newSubpartObjects[i];
                    var classIndex = newSubparts[i];

                    // Enrolled class
                    var classObject = subpart.Classes[classIndex];
                    var classId = classObject.Id;
                    var classState = classStates[classId];
                    var roomCapacity = rooms[classObject.PossibleRooms[classState.Room].Id].Capacity;
                    var classCapacity = classObject.Capacity;
                    var attendees = classState.Attendees + 1;
                    hardPenalty -= classState.RoomCapacityPenalty;
                    hardPenalty -= classState.ClassCapacityPenalty;
                    var roomCapacityPenalty = attendees > roomCapacity
                        ? CapacityOverflowBase + (attendees - roomCapacity) * CapacityOverflowRate
                        : 0d;
                    var classCapacityPenalty = attendees > classCapacity
                        ? CapacityOverflowBase + (attendees - classCapacity) * CapacityOverflowRate
                        : 0d;
                    classUpdates.Add(new Override<ClassState>(classId,
                        classState.WithAttendees(attendees, classCapacityPenalty, roomCapacityPenalty)));
                    hardPenalty += roomCapacityPenalty;
                    hardPenalty += classCapacityPenalty;
                }
            }

            var softPenalty = SoftPenalty;

            var count = 0;
            var travelTimes = Problem.TravelTimes;
            var studentPenalty = Problem.StudentPenalty;
            var studentClasses = new List<(Schedule schedule, int room)>();
            var conflictingPairs = 0;
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
                classStates.With(classUpdates),
                studentStates.With(new Override<StudentState>(student, new StudentState(enrollmentStates, conflictingPairs))));
        }
    }
}
