using System;
using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Internal.Specialized
{
    public class ClassData : Class
    {
        internal ClassData(
            int id,
            int parentId,
            int courseId,
            int capacity,
            RoomAssignment[] possibleRooms,
            ScheduleAssignment[] possibleSchedules,
            IEnumerable<IConstraint> commonConstraints,
            IEnumerable<IConstraint> timeConstraints,
            IEnumerable<IConstraint> roomConstraints,
            IEnumerable<int> children)
            : base(id, parentId, capacity, possibleRooms, possibleSchedules)
        {
            CourseId = courseId;
            CommonConstraints = commonConstraints.ToArray();
            TimeConstraints = timeConstraints.ToArray();
            RoomConstraints = roomConstraints.ToArray();
            AllConstraints = CommonConstraints
                .Union(TimeConstraints)
                .Union(RoomConstraints)
                .Distinct()
                .ToArray();
            Children = new HashSet<int>(children ?? Enumerable.Empty<int>());
            HasChildren = Children.Count > 0;
            ConstraintCount = AllConstraints.Length;
            MinTimePenalty = possibleSchedules.Min(x => x.Penalty);
            MinRoomPenalty = possibleRooms.Length > 0
                ? possibleRooms.Min(x => x.Penalty)
                : 0;
        }

        public readonly int CourseId;

        public readonly IConstraint[] CommonConstraints;

        public readonly IConstraint[] TimeConstraints;

        public readonly IConstraint[] RoomConstraints;

        public readonly IConstraint[] AllConstraints;

        public readonly int ConstraintCount;

        public readonly HashSet<int> Children;

        public readonly bool HasChildren;

        public readonly int MinTimePenalty;

        public readonly int MinRoomPenalty;

        internal IEnumerable<IConstraint> ConstraintsRelatedTo(ConstraintType type)
        {
            switch (type)
            {
                case ConstraintType.Common:
                    return AllConstraints;
                case ConstraintType.Time:
                    return TimeConstraints.Union(CommonConstraints);
                case ConstraintType.Room:
                    return RoomConstraints.Union(CommonConstraints);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}