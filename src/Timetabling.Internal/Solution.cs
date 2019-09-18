using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Timetabling.Internal.Specialized;
using Timetabling.Internal.Utils;

namespace Timetabling.Internal
{
    public class Solution : IClassStates
    {
        private struct ClassOverride
        {
            internal ClassOverride(int @class, int room, int time)
            {
                Class = @class;
                Room = room;
                Time = time;
            }

            internal readonly int Class;

            internal readonly int Room;

            internal readonly int Time;
        }

        private class SolutionProxy : IClassStates
        {
            private readonly Solution inner;
            private readonly ClassOverride classOverride;

            internal SolutionProxy(Solution inner, ClassOverride classOverride)
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

            internal int GetRoomId(int @class)
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
        }

        private IClassStates With(ClassOverride ov)
        {
            return new SolutionProxy(this, ov);
        }

        internal const int CapacityOverflowBase = 0;
        internal const int CapacityOverflowRate = int.MaxValue;

        internal readonly ChunkedArray<ClassState> ClassStates;
        internal readonly ChunkedArray<StudentState> StudentStates;
        internal readonly ChunkedArray<ConstraintState> ConstraintStates;

        internal Solution(
            Problem problem,
            int hardPenalty,
            int softPenalty,
            int classConflicts,
            int roomsUnavailable,
            ChunkedArray<ClassState> classStates,
            ChunkedArray<StudentState> studentStates,
            ChunkedArray<ConstraintState> constraintStates)
        {
            Problem = problem;
            HardPenalty = hardPenalty;
            SoftPenalty = softPenalty;
            ClassConflicts = classConflicts;
            RoomsUnavailable = roomsUnavailable;
            ClassStates = classStates;
            StudentStates = studentStates;
            ConstraintStates = constraintStates;
            NormalizedClassConflicts = classConflicts / problem.WorstCaseClassConflicts;
            NormalizedRoomsUnavailable = roomsUnavailable / problem.WorstCaseRoomsUnavailable;
            NormalizedSoftPenalty = softPenalty / problem.WorstSoftPenalty;
        }

        public readonly int HardPenalty;

        public readonly int ClassConflicts;

        public readonly double NormalizedClassConflicts;

        public readonly int RoomsUnavailable;

        public readonly double NormalizedRoomsUnavailable;

        public readonly int SoftPenalty;

        public readonly double NormalizedSoftPenalty;

        public readonly Problem Problem;

        public double SearchPenalty => HardPenalty > 0
          ? 0.02d * HardPenalty + 0.1d * Math.Ceiling(10d * NormalizedSoftPenalty)
          : NormalizedSoftPenalty;

        internal (int hardPenalty, int softPenalty) CalculatePenalty()
        {
            var (distHard, distSoft) = CalculateDistributionPenalty();

            return (
                distHard
                + RoomUnavailablePenalty()
                + RoomCapacityPenalty()
                + ClassCapacityPenalty()
                + CountClassConflicts(),
                TimePenalty()
                + RoomPenalty()
                + StudentPenalty()
                + distSoft
                );
        }

        internal void PrintStats()
        {
            var (h, s) = CalculatePenalty();
            Console.WriteLine("========================================");
            Console.WriteLine($"=== Hard Penalty ({HardPenalty}) ===");
            Console.WriteLine($"Class conflicts: {CountClassConflicts()}");
            Console.WriteLine($"Class capacity penalty: {ClassCapacityPenalty()}");
            Console.WriteLine($"Room capacity penalty: {RoomCapacityPenalty()}");
            Console.WriteLine($"Room unavailable penalty: {RoomUnavailablePenalty()}");
            Console.WriteLine("=== Constraints ===");
            Console.WriteLine($"Failures: Hard: {FailedHardConstraints()}, Soft: {FailedSoftConstraints()}");
            PrintFailedConstraints();
            Console.WriteLine($"=== Soft Penalty {SoftPenalty} / {NormalizedSoftPenalty} ===");
            Console.WriteLine($"Time penalty: {TimePenalty()}");
            Console.WriteLine($"Room penalty: {RoomPenalty()}");
            Console.WriteLine($"Dist penalty: {DistributionPenalty()}");
            Console.WriteLine($"Student penalty: {StudentPenalty()}");
            Console.WriteLine("=== Total Penalty ===");
            Console.WriteLine($"Instance penalty: Hard: {HardPenalty}, Soft: {SoftPenalty}, Search: {SearchPenalty}");
            Console.WriteLine($"Computed penalty: Hard: {h}, Soft: {s}, Normalized: {h + s / (s + 1d)}");
            Console.WriteLine("========================================");
        }

        internal int RoomUnavailablePenalty()
        {
            var penalty = 0;
            var classes = Problem.Classes.Select(c => (ClassStates[c.Id], GetTime(c.Id), GetRoom(c.Id))).ToArray();
            for (var i = 0; i < classes.Length; i++)
            {
                var (ci, ti, ri) = classes[i];
                if (ri != null)
                {
                    var room = Problem.Rooms[ri.Id];
                    foreach (var schedule in room.UnavailableSchedules)
                    {
                        if (schedule.Overlaps(ti))
                        {
                            penalty++;
                            Debug.Assert(ci.RoomUnavailablePenalty > 0);
                            break;
                        }
                    }
                }
            }

            return penalty;
        }

        public HashSet<int> RoomUnavailableClasses()
        {
            var conflicts = new HashSet<int>();
            var classes = Problem.Classes.Select(c => (ClassStates[c.Id], GetTime(c.Id), GetRoom(c.Id))).ToArray();
            for (var i = 0; i < classes.Length; i++)
            {
                var (ci, ti, ri) = classes[i];
                if (ri != null)
                {
                    var room = Problem.Rooms[ri.Id];
                    foreach (var schedule in room.UnavailableSchedules)
                    {
                        if (schedule.Overlaps(ti))
                        {
                            conflicts.Add(i);
                            Debug.Assert(ci.RoomUnavailablePenalty > 0);
                            break;
                        }
                    }
                }
            }

            return conflicts;
        }

        internal int RoomCapacityPenalty()
        {
            var penalty = 0;
            var classes = Problem.Classes.Select(c => (ClassStates[c.Id], GetTime(c.Id), GetRoom(c.Id))).ToArray();
            for (var i = 0; i < classes.Length; i++)
            {
                var (ci, ti, ri) = classes[i];
                if (ri != null)
                {
                    if (ci.Attendees > ri.Capacity)
                    {
                        var val = CapacityOverflowBase + (ci.Attendees - ri.Capacity) / CapacityOverflowRate;
                        penalty += val;
                        Debug.Assert(ci.RoomCapacityPenalty == val);
                    }
                }
            }

            return penalty;
        }

        internal int ClassCapacityPenalty()
        {
            var penalty = 0;
            var classes = Problem.Classes.Select(c => (ClassStates[c.Id], GetTime(c.Id), GetRoom(c.Id))).ToArray();
            for (var i = 0; i < classes.Length; i++)
            {
                var (ci, ti, ri) = classes[i];
                if (ci.Attendees > Problem.Classes[i].Capacity)
                {
                    var val = CapacityOverflowBase + (ci.Attendees - Problem.Classes[i].Capacity) / CapacityOverflowRate;
                    Debug.Assert(val == ci.ClassCapacityPenalty);
                    penalty += val;
                }
            }

            return penalty;
        }

        internal int CountClassConflicts()
        {
            var conflicts = 0;
            var classes = Problem.Classes.Select(c => (ClassStates[c.Id], GetTime(c.Id), GetRoom(c.Id))).ToArray();
            for (var i = 0; i < classes.Length; i++)
            {
                var (ci, ti, ri) = classes[i];
                if (ri == null)
                {
                    continue;
                }

                for (var j = i + 1; j < classes.Length; j++)
                {
                    var (cj, tj, rj) = classes[j];
                    if (rj == null)
                    {
                        continue;
                    }

                    if (ri.Id != rj.Id)
                    {
                        continue;
                    }

                    var travelTime = Problem.TravelTimes[ri.Id, rj.Id];
                    if (ti.Overlaps(tj, travelTime))
                    {
                        conflicts++;
                    }
                }
            }

            return conflicts;
        }

        public Dictionary<int, ClassConflicts> SoftViolatingClasses()
        {
            var result = new Dictionary<int, ClassConflicts>();
            void AddOrIncrement(int cls, ConstraintType type)
            {
                if (!result.TryGetValue(cls, out var state))
                {
                    state = new ClassConflicts(0, 0);
                }

                switch (type)
                {
                    case ConstraintType.Common:
                        state = state.Increment(1, 1);
                        break;
                    case ConstraintType.Time:
                        state = state.Increment(1, 0);
                        break;
                    case ConstraintType.Room:
                        state = state.Increment(1, 1);
                        break;
                    default:
                        return;
                }

                result[cls] = state;
            }

            GetFailedSoftConstraints().ForEach(constraint =>
            {
                constraint
                    .EvaluateConflictingClasses(Problem, this)
                    .ForEach(cls => AddOrIncrement(cls, constraint.Type));
            });

            return result;
        }

        public Dictionary<int, ClassConflicts> ViolatingClasses()
        {
            var result = new Dictionary<int, ClassConflicts>();
            void AddOrIncrement(int cls, ConstraintType type)
            {
                if (!result.TryGetValue(cls, out var state))
                {
                    state = new ClassConflicts(0, 0);
                }

                switch (type)
                {
                    case ConstraintType.Common:
                        state = state.Increment(1, 1);
                        break;
                    case ConstraintType.Time:
                        state = state.Increment(1, 0);
                        break;
                    case ConstraintType.Room:
                        state = state.Increment(1, 1);
                        break;
                    default:
                        return;
                }

                result[cls] = state;
            }

            ConflictingClasses().ForEach(cls => AddOrIncrement(cls, ConstraintType.Common));
            RoomUnavailableClasses().ForEach(cls => AddOrIncrement(cls, ConstraintType.Common));
            GetFailedHardConstraints().ForEach(constraint =>
            {
                constraint
                    .EvaluateConflictingClasses(Problem, this)
                    .ForEach(cls => AddOrIncrement(cls, constraint.Type));
            });

            return result;
        }

        public HashSet<int> ConflictingClasses()
        {
            var conflicts = new HashSet<int>();
            var classes = Problem.Classes.Select(c => (ClassStates[c.Id], GetTime(c.Id), GetRoom(c.Id))).ToArray();
            for (var i = 0; i < classes.Length; i++)
            {
                var (ci, ti, ri) = classes[i];
                if (ri == null)
                {
                    continue;
                }

                for (var j = i + 1; j < classes.Length; j++)
                {
                    var (cj, tj, rj) = classes[j];
                    if (rj == null)
                    {
                        continue;
                    }

                    if (ri.Id != rj.Id)
                    {
                        continue;
                    }

                    var travelTime = Problem.TravelTimes[ri.Id, rj.Id];
                    if (ti.Overlaps(tj, travelTime))
                    {
                        conflicts.Add(i);
                        conflicts.Add(j);
                    }
                }
            }

            return conflicts;
        }

        public Dictionary<int, int> StudentConflictingClasses()
        {
            var result = new Dictionary<int, int>();
            var conflicts = 0;
            for (var i = 0; i < StudentStates.Length; i++)
            {
                var conflictingClasses = new HashSet<int>();
                var studentData = Problem.Students[i];
                var studentState = StudentStates[i];
                var classesSoFar = new List<(int id, Schedule schedule, int room)>();
                for (var j = 0; j < studentState.EnrollmentStates.Length; j++)
                {
                    var enrollmentState = studentState.EnrollmentStates[j];
                    var course = Problem
                        .Courses[studentData.Courses[j]]
                        .Configurations[enrollmentState.ConfigIndex];

                    for (var k = 0; k < enrollmentState.Subparts.Length; k++)
                    {
                        var classIndex = enrollmentState.Subparts[k];
                        var classObject = course.Subparts[k].Classes[classIndex];
                        var classData = Problem.Classes[classObject.Id];
                        var classState = ClassStates[classObject.Id];
                        var schedule = classData.PossibleSchedules[classState.Time];
                        var room = classState.Room >= 0 ? classData.PossibleRooms[classState.Room].Id : -1;

                        foreach (var (id, prevSchedule, prevRoom) in classesSoFar)
                        {
                            var travelTime = room >= 0 && prevRoom >= 0 ? Problem.TravelTimes[room, prevRoom] : 0;
                            if (schedule.Overlaps(prevSchedule, travelTime))
                            {
                                conflicts++;
                                conflictingClasses.Add(id);
                            }
                        }

                        classesSoFar.Add((classObject.Id, schedule, room));
                    }
                }

                foreach (var id in conflictingClasses)
                {
                    if (result.ContainsKey(id))
                    {
                        result[id]++;
                    }
                    else
                    {
                        result[id] = 1;
                    }
                }
            }

            return result;
        }

        public int StudentPenalty()
        {
            var conflicts = 0;
            for (var i = 0; i < StudentStates.Length; i++)
            {
                var studentData = Problem.Students[i];
                var studentState = StudentStates[i];
                var classesSoFar = new List<(int id, Schedule schedule, int room)>();
                for (var j = 0; j < studentState.EnrollmentStates.Length; j++)
                {
                    var enrollmentState = studentState.EnrollmentStates[j];
                    var course = Problem
                        .Courses[studentData.Courses[j]]
                        .Configurations[enrollmentState.ConfigIndex];

                    for (var k = 0; k < enrollmentState.Subparts.Length; k++)
                    {
                        var classIndex = enrollmentState.Subparts[k];
                        var classObject = course.Subparts[k].Classes[classIndex];
                        var classData = Problem.Classes[classObject.Id];
                        var classState = ClassStates[classObject.Id];
                        var schedule = classData.PossibleSchedules[classState.Time];
                        var room = classState.Room >= 0 ? classData.PossibleRooms[classState.Room].Id : -1;

                        foreach (var (_, prevSchedule, prevRoom) in classesSoFar)
                        {
                            var travelTime = room >= 0 && prevRoom >= 0 ? Problem.TravelTimes[room, prevRoom] : 0;
                            if (schedule.Overlaps(prevSchedule, travelTime))
                            {
                                conflicts++;
                            }
                        }

                        classesSoFar.Add((classObject.Id, schedule, room));
                    }
                }
            }

            return conflicts * Problem.StudentPenalty;
        }

        public List<IConstraint> GetFailedSoftConstraints()
        {
            var result = new List<IConstraint>();
            foreach (var constraint in Problem.Constraints.Where(c => !c.Required))
            {
                if (constraint.Evaluate(Problem, this).softPenalty > 0)
                {
                    result.Add(constraint);
                }
            }

            return result;
        }

        public List<IConstraint> GetFailedHardConstraints()
        {
            var result = new List<IConstraint>();
            foreach (var constraint in Problem.Constraints.Where(c => c.Required))
            {
                if (constraint.Evaluate(Problem, this).hardPenalty > 0)
                {
                    result.Add(constraint);
                }
            }

            return result;
        }

        public int FailedHardConstraints()
        {
            int count = 0;
            foreach (var constraint in Problem.Constraints.Where(c => c.Required))
            {
                if (constraint.Evaluate(Problem, this).hardPenalty > 0d)
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
                if (constraint.Evaluate(Problem, this).softPenalty > 0)
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
                var state = ClassStates[i];
                if (state.Room != -1)
                {
                    roomPenalty += @class.PossibleRooms[state.Room].Penalty;
                }
            }

            return roomPenalty * Problem.RoomPenalty;
        }

        public int RoomPenaltyMin()
        {
            var roomPenalty = int.MaxValue;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = ClassStates[i];
                if (state.Room != -1)
                {
                    roomPenalty = Math.Min(roomPenalty, @class.PossibleRooms[state.Room].Penalty);
                }
            }

            return roomPenalty * Problem.RoomPenalty;
        }

        public int RoomPenaltyMax()
        {
            var roomPenalty = int.MinValue;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = ClassStates[i];
                if (state.Room != -1)
                {
                    roomPenalty = Math.Max(roomPenalty, @class.PossibleRooms[state.Room].Penalty);
                }
            }

            return roomPenalty * Problem.RoomPenalty;
        }

        public int TimePenalty()
        {
            var timePenalty = 0;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = ClassStates[i];
                timePenalty += @class.PossibleSchedules[state.Time].Penalty;
            }

            return timePenalty * Problem.TimePenalty;
        }

        public int TimePenaltyMin()
        {
            var timePenalty = int.MaxValue;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = ClassStates[i];
                timePenalty = Math.Min(timePenalty, @class.PossibleSchedules[state.Time].Penalty);
            }

            return timePenalty * Problem.TimePenalty;
        }

        public int TimePenaltyMax()
        {
            var timePenalty = int.MinValue;
            for (var i = 0; i < Problem.Classes.Length; i++)
            {
                var @class = Problem.Classes[i];
                var state = ClassStates[i];
                timePenalty = Math.Max(timePenalty, @class.PossibleSchedules[state.Time].Penalty);
            }

            return timePenalty * Problem.TimePenalty;
        }

        public int DistributionPenalty()
        {
            return CalculateDistributionPenalty().softPenalty * Problem.DistributionPenalty;
        }

        private void PrintFailedConstraints()
        {
            foreach (var constraint in Problem.Constraints)
            {
                var (h, _) = constraint.Evaluate(Problem, this);
                if (h > 0)
                {
                    Console.WriteLine($"{constraint.GetType().Name}->{h} [{string.Join(",", constraint.Classes.Select(c => c + 1))}]");
                }
            }
        }

        private (int hardPenalty, int softPenalty) CalculateDistributionPenalty()
        {
            var hard = 0;
            var soft = 0;
            foreach (var constraint in Problem.Constraints)
            {
                var (h, s) = constraint.Evaluate(Problem, this);
                hard += h;
                soft += s;
            }

            return (hard, soft);
        }

        public IEnumerable<double> HardConstraintStates()
        {
            var count = Problem.HardConstraints.Length;
            for (var i = 0; i < count; i++)
            {
                yield return NormalizedHardConstraintPenalty(i);
            }
        }

        public double HardConstraintSquaredSum()
        {
            var sum = 0d;
            var count = Problem.HardConstraints.Length;
            for (var i = 0; i < count; i++)
            {
                sum += Math.Pow(NormalizedHardConstraintPenalty(i), 2);
            }

            return sum;
        }

        public double NormalizedHardConstraintPenalty(int constraintIndex)
        {
            var id = Problem.HardConstraints[constraintIndex].Id;
            return ConstraintStates[id].Normalized;
        }

        public ScheduleAssignment GetTime(int @class)
        {
            return Problem.Classes[@class].PossibleSchedules[ClassStates[@class].Time];
        }

        public int GetTimeIndex(int @class)
        {
            return ClassStates[@class].Time;
        }

        public Room GetRoom(int @class)
        {
            var state = ClassStates[@class];
            if (state.Room < 0)
            {
                return null;
            }

            return Problem.Rooms[Problem.Classes[@class].PossibleRooms[state.Room].Id];
        }

        public int GetRoomIndex(int @class)
        {
            return ClassStates[@class].Room;
        }

        public IEnumerable<int> GetClassStudents(int @class)
        {
            for (var i = 0; i < StudentStates.Length; i++)
            {
                if (HasClass(i, @class))
                {
                    yield return i;
                }
            }
        }

        public IEnumerable<int> GetStudentClasses(int student)
        {
            for (var i = 0; i < ClassStates.Length; i++)
            {
                if (HasClass(student, i))
                {
                    yield return i;
                }
            }
        }

        public ConstraintState GetConstraintState(int constraint)
        {
            return ConstraintStates[constraint];
        }

        public int GetRoomId(int @class)
        {
            var room = ClassStates[@class].Room;
            if (room < 0)
            {
                return -1;
            }

            return Problem.Classes[@class].PossibleRooms[room].Id;
        }

        internal Solution WithVariable(int @class, int value, VariableType type)
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
            var classStates = ClassStates;
            if (@class < 0 || @class >= classStates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            var problem = Problem;
            var classes = problem.Classes;
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
            var oldRoom = problem.Rooms[oldRoomAssignment.Id];
            var hardPenalty = HardPenalty
                              - state.RoomCapacityPenalty
                              - state.RoomUnavailablePenalty;
            var softPenalty = SoftPenalty
                              - oldRoomAssignment.Penalty * problem.RoomPenalty;

            var self = With(new ClassOverride(@class, room, state.Time));

            // ReSharper disable once LocalVariableHidesMember
            var constraintStates = ConstraintStates;

            // Eval Room constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var roomConstraints = classData.RoomConstraints;
            for (var i = 0; i < roomConstraints.Length; i++)
            {
                var roomConstraint = roomConstraints[i];
                var current = constraintStates[roomConstraint.Id];
                var (roomHardPenalty, roomSoftPenalty) = roomConstraint.Evaluate(problem, self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (roomHardPenalty != current.HardPenalty || roomSoftPenalty != current.SoftPenalty)
                {
                    var normalized = (roomConstraint.Required ? (double)roomHardPenalty : roomSoftPenalty) /
                                     roomConstraint.WorstCase;
                    hardPenalty += roomHardPenalty - current.HardPenalty;
                    softPenalty += problem.DistributionPenalty * (roomSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(roomConstraint.Id,
                            new ConstraintState(roomHardPenalty, roomSoftPenalty, normalized)));
                }
            }

            // Eval Common constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var commonConstraints = classData.CommonConstraints;
            for (var i = 0; i < commonConstraints.Length; i++)
            {
                var commonConstraint = commonConstraints[i];
                var current = constraintStates[commonConstraint.Id];
                var (commonHardPenalty, commonSoftPenalty) = commonConstraint.Evaluate(problem, self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (commonHardPenalty != current.HardPenalty || commonSoftPenalty != current.SoftPenalty)
                {
                    var normalized = (commonConstraint.Required ? (double)commonHardPenalty : commonSoftPenalty) /
                                     commonConstraint.WorstCase;
                    hardPenalty += commonHardPenalty - current.HardPenalty;
                    softPenalty += problem.DistributionPenalty * (commonSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(commonConstraint.Id,
                            new ConstraintState(commonHardPenalty, commonSoftPenalty, normalized)));
                }
            }

            // Eval new room penalty
            softPenalty += classData.PossibleRooms[room].Penalty * problem.RoomPenalty;

            // Eval new room capacity
            var newRoom = problem.Rooms[classData.PossibleRooms[room].Id];
            var roomCapacityPenalty = state.Attendees > newRoom.Capacity
                ? CapacityOverflowBase + (state.Attendees - newRoom.Capacity) / CapacityOverflowRate
                : 0;

            hardPenalty += roomCapacityPenalty;

            // Eval new room availability at time
            var roomUnavailablePenalty = 0;
            var schedule = classData.PossibleSchedules[state.Time];
            var unavailableSchedules = newRoom.UnavailableSchedules;
            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < unavailableSchedules.Length; i++)
            {
                var unavailableSchedule = unavailableSchedules[i];
                if (schedule.Overlaps(unavailableSchedule))
                {
                    roomUnavailablePenalty = 1;
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
            var courses = problem.Courses;
            var possibleStudents = courses[classData.CourseId].PossibleStudents;
            var studentStates = StudentStates;
            var students = problem.Students;
            var travelTimes = problem.TravelTimes;
            var studentPenalty = problem.StudentPenalty;
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
                problem,
                hardPenalty,
                softPenalty,
                ClassConflicts + classConflicts,
                RoomsUnavailable + roomUnavailablePenalty - state.RoomUnavailablePenalty,
                classStates.With(new Override<ClassState>(@class, newClassState)),
                studentStates.With(studentOverrides),
                constraintStates);
        }

        public Solution WithTime(int @class, int time)
        {
            var classStates = ClassStates;
            if (@class < 0 || @class >= classStates.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            var problem = Problem;
            var classes = problem.Classes;
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
                              - oldSchedule.Penalty * problem.TimePenalty;

            var self = With(new ClassOverride(@class, state.Room, time));

            // ReSharper disable once LocalVariableHidesMember
            var constraintStates = ConstraintStates;

            // Eval Time constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var timeConstraints = classData.TimeConstraints;
            for (var i = 0; i < timeConstraints.Length; i++)
            {
                var timeConstraint = timeConstraints[i];
                var current = constraintStates[timeConstraint.Id];
                var (timeHardPenalty, timeSoftPenalty) = timeConstraint.Evaluate(problem, self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (timeHardPenalty != current.HardPenalty || timeSoftPenalty != current.SoftPenalty)
                {
                    var normalized = (timeConstraint.Required ? (double)timeHardPenalty : timeSoftPenalty) /
                                     timeConstraint.WorstCase;
                    hardPenalty += timeHardPenalty - current.HardPenalty;
                    softPenalty += problem.DistributionPenalty * (timeSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(timeConstraint.Id,
                            new ConstraintState(timeHardPenalty, timeSoftPenalty, normalized)));
                }
            }

            // Eval Common constraints of C
            // ReSharper disable once ForCanBeConvertedToForeach
            var commonConstraints = classData.CommonConstraints;
            for (var i = 0; i < commonConstraints.Length; i++)
            {
                var commonConstraint = commonConstraints[i];
                var current = constraintStates[commonConstraint.Id];
                var (commonHardPenalty, commonSoftPenalty) = commonConstraint.Evaluate(problem, self);
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (commonHardPenalty != current.HardPenalty || commonSoftPenalty != current.SoftPenalty)
                {
                    var normalized = (commonConstraint.Required ? (double)commonHardPenalty : commonSoftPenalty) /
                                     commonConstraint.WorstCase;
                    hardPenalty += commonHardPenalty - current.HardPenalty;
                    softPenalty += problem.DistributionPenalty * (commonSoftPenalty - current.SoftPenalty);
                    constraintStates = constraintStates.With(
                        new Override<ConstraintState>(commonConstraint.Id,
                            new ConstraintState(commonHardPenalty, commonSoftPenalty, normalized)));
                }
            }

            // Eval new time penalty
            var newSchedule = classData.PossibleSchedules[time];
            softPenalty += newSchedule.Penalty * problem.TimePenalty;

            // Eval room availability at new time
            var roomUnavailablePenalty = 0;
            var roomId = -1;
            var classConflicts = 0;
            if (state.Room >= 0)
            {
                roomId = classData.PossibleRooms[state.Room].Id;
                var room = problem.Rooms[roomId];
                var unavailableSchedules = room.UnavailableSchedules;
                // ReSharper disable once LoopCanBeConvertedToQuery
                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < unavailableSchedules.Length; i++)
                {
                    var unavailableSchedule = unavailableSchedules[i];
                    if (newSchedule.Overlaps(unavailableSchedule))
                    {
                        roomUnavailablePenalty = 1;
                        break;
                    }
                }

                hardPenalty += roomUnavailablePenalty;

                // Cleanup clashes in previous room
                // Eval clashes with other classes
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
            var courses = problem.Courses;
            var possibleStudents = courses[classData.CourseId].PossibleStudents;
            var studentStates = StudentStates;
            var students = problem.Students;
            var travelTimes = problem.TravelTimes;
            var studentPenalty = problem.StudentPenalty;
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
                problem,
                hardPenalty,
                softPenalty,
                ClassConflicts + classConflicts,
                RoomsUnavailable + roomUnavailablePenalty - state.RoomUnavailablePenalty,
                classStates.With(new Override<ClassState>(@class, newClassState)),
                studentStates.With(studentOverrides),
                constraintStates);
        }

        public Solution WithEnrollment(int student, int @class)
        {
            var studentStates = StudentStates;
            var studentState = studentStates[student];
            var problem = Problem;
            var studentData = problem.Students[student];
            var enrollmentConfigurations = studentData.EnrollmentConfigurations;
            if (!enrollmentConfigurations.TryGetValue(@class, out var newConfig))
            {
                throw new ArgumentOutOfRangeException(nameof(@class));
            }

            var classes = problem.Classes;
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
                newState = new EnrollmentState(newConfigIndex, (int[])problem.Courses[courseId].Configurations[newConfigIndex].Baseline.Clone());
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
            var classStates = ClassStates;
            var courses = problem.Courses;
            var rooms = problem.Rooms;
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
                    int oldRoomCapacityPenalty;
                    if (oldRoom >= 0)
                    {
                        oldRoomId = oldClass.PossibleRooms[oldClassState.Room].Id;
                        var oldRoomCapacity = rooms[oldRoomId].Capacity;
                        oldRoomCapacityPenalty = oldAttendees > oldRoomCapacity
                            ? CapacityOverflowBase + (oldAttendees - oldRoomCapacity) / CapacityOverflowRate
                            : 0;
                        hardPenalty += oldRoomCapacityPenalty;
                    }
                    else
                    {
                        oldRoomCapacityPenalty = 0;
                    }

                    oldSchedule = oldClass.PossibleSchedules[oldClassState.Time];
                    var oldClassCapacity = oldClass.Capacity;
                    hardPenalty -= oldClassState.RoomCapacityPenalty;
                    hardPenalty -= oldClassState.ClassCapacityPenalty;
                    var oldClassCapacityPenalty = oldAttendees > oldClassCapacity
                        ? CapacityOverflowBase + (oldAttendees - oldClassCapacity) / CapacityOverflowRate
                        : 0;
                    classUpdates.Add(new Override<ClassState>(oldClassId,
                        oldClassState.WithAttendees(oldAttendees, oldClassCapacityPenalty, oldRoomCapacityPenalty)));
                    hardPenalty += oldClassCapacityPenalty;

                    // Enrolled class
                    var newClass = subpart.Classes[newClassIndex];
                    var newClassId = newClass.Id;
                    var newClassState = classStates[newClassId];
                    var newRoom = newClassState.Room;
                    var newAttendees = newClassState.Attendees + 1;
                    int newRoomCapacityPenalty;
                    if (newRoom >= 0)
                    {
                        newRoomId = newClass.PossibleRooms[newClassState.Room].Id;
                        var newRoomCapacity = rooms[newRoomId].Capacity;
                        newRoomCapacityPenalty = newAttendees > newRoomCapacity
                            ? CapacityOverflowBase + (newAttendees - newRoomCapacity) / CapacityOverflowRate
                            : 0;
                        hardPenalty += newRoomCapacityPenalty;
                    }
                    else
                    {
                        newRoomCapacityPenalty = 0;
                    }

                    newSchedule = newClass.PossibleSchedules[newClassState.Time];
                    var newClassCapacity = newClass.Capacity;
                    hardPenalty -= newClassState.RoomCapacityPenalty;
                    hardPenalty -= newClassState.ClassCapacityPenalty;
                    var newClassCapacityPenalty = newAttendees > newClassCapacity
                        ? CapacityOverflowBase + (newAttendees - newClassCapacity) / CapacityOverflowRate
                        : 0;
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
                    //Console.WriteLine($"Unenrolled {oldClassId}");
                    var oldClassState = classStates[oldClassId];
                    var oldRoom = oldClassState.Room;
                    var oldAttendees = oldClassState.Attendees - 1;
                    int oldRoomCapacityPenalty;
                    if (oldRoom >= 0)
                    {
                        oldRoomId = oldClass.PossibleRooms[oldClassState.Room].Id;
                        var oldRoomCapacity = rooms[oldRoomId].Capacity;
                        oldRoomCapacityPenalty = oldAttendees > oldRoomCapacity
                            ? CapacityOverflowBase + (oldAttendees - oldRoomCapacity) / CapacityOverflowRate
                            : 0;
                        hardPenalty += oldRoomCapacityPenalty;
                    }
                    else
                    {
                        oldRoomCapacityPenalty = 0;
                    }

                    oldSchedule = oldClass.PossibleSchedules[oldClassState.Time];
                    var oldClassCapacity = oldClass.Capacity;
                    hardPenalty -= oldClassState.RoomCapacityPenalty;
                    hardPenalty -= oldClassState.ClassCapacityPenalty;
                    var oldClassCapacityPenalty = oldAttendees > oldClassCapacity
                        ? CapacityOverflowBase + (oldAttendees - oldClassCapacity) / CapacityOverflowRate
                        : 0;
                    classUpdates.Add(new Override<ClassState>(oldClassId,
                        oldClassState.WithAttendees(oldAttendees, oldClassCapacityPenalty, oldRoomCapacityPenalty)));
                    hardPenalty += oldClassCapacityPenalty;
                }

                for (var i = 0; i < newSubpartObjects.Length; i++)
                {
                    var subpart = newSubpartObjects[i];
                    var newClassIndex = newSubparts[i];

                    // Enrolled class
                    var newClass = subpart.Classes[newClassIndex];
                    var newClassId = newClass.Id;
                    //Console.WriteLine($"Enrolled {newClassId}");
                    var newClassState = classStates[newClassId];
                    var newRoom = newClassState.Room;
                    var newAttendees = newClassState.Attendees + 1;
                    int newRoomCapacityPenalty;
                    if (newRoom >= 0)
                    {
                        newRoomId = newClass.PossibleRooms[newClassState.Room].Id;
                        var newRoomCapacity = rooms[newRoomId].Capacity;
                        newRoomCapacityPenalty = newAttendees > newRoomCapacity
                            ? CapacityOverflowBase + (newAttendees - newRoomCapacity) / CapacityOverflowRate
                            : 0;
                        hardPenalty += newRoomCapacityPenalty;
                    }
                    else
                    {
                        newRoomCapacityPenalty = 0;
                    }

                    newSchedule = newClass.PossibleSchedules[newClassState.Time];
                    var newClassCapacity = newClass.Capacity;
                    hardPenalty -= newClassState.RoomCapacityPenalty;
                    hardPenalty -= newClassState.ClassCapacityPenalty;
                    var newClassCapacityPenalty = newAttendees > newClassCapacity
                        ? CapacityOverflowBase + (newAttendees - newClassCapacity) / CapacityOverflowRate
                        : 0;
                    classUpdates.Add(new Override<ClassState>(newClassId,
                        newClassState.WithAttendees(newAttendees, newClassCapacityPenalty, newRoomCapacityPenalty)));
                    hardPenalty += newClassCapacityPenalty;
                }
            }

            var travelTimes = problem.TravelTimes;

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
                softPenalty += (conflictingPairs - currentConflictingPairs) * problem.StudentPenalty;
            }

            return new Solution(
                problem,
                hardPenalty,
                softPenalty,
                ClassConflicts,
                RoomsUnavailable,
                classStates.With(classUpdates),
                studentStates.With(new Override<StudentState>(student, new StudentState(enrollmentStates, conflictingPairs))),
                ConstraintStates);
        }

        internal bool HasClass(int student, int @class)
        {
            var state = StudentStates[student];
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
    }
}
