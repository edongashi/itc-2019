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

        public readonly ChunkedArray<int> RoomStates;

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
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < possibleClasses.Length; i++)
            {
                var c = possibleClasses[i];
                if (c == @class)
                {
                    continue;
                }

                var cState = classStates[c];
                if (cState.Room != room)
                {
                    continue;
                }

                var cTime = classes[c].PossibleSchedules[cState.Time];
                if (schedule.Overlaps(cTime))
                {
                    clashesWithOtherClasses--;
                }
            }

            // Eval clashes with other classes
            possibleClasses = newRoom.PossibleClasses;
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < possibleClasses.Length; i++)
            {
                var c = possibleClasses[i];
                if (c == @class)
                {
                    continue;
                }

                var cState = classStates[c];
                if (cState.Room != room)
                {
                    continue;
                }

                var cTime = classes[c].PossibleSchedules[cState.Time];
                if (schedule.Overlaps(cTime))
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

                    if (classEnrollmentState.Classes[enrollmentConfiguration.SubpartIndex] !=
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

                    var classIndexes = enrollmentState.Classes;
                    for (var j = 0; j < classIndexes.Length; j++)
                    {
                        var classIndex = classIndexes[j];
                        var classObject = subparts[j].Classes[classIndex];
                        var classId = classObject.Id;
                        Schedule currentSchedule;
                        int currentRoom;
                        if (classId == @class)
                        {
                            currentSchedule = schedule;
                            currentRoom = room;
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
            return this;
        }

        public Solution WithEnrollmentState(int student, int course, EnrollmentState state)
        {
            // Eval student
            // Update class attendees -> class capacity/room capacity
            return this;
        }
    }
}
