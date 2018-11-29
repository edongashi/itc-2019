using System;
using System.Collections.Generic;
using System.Linq;
using Timetabling.Common.Utils;

namespace Timetabling.Common.ProblemModel
{
    public class Class
    {
        public Class(
            int id,
            int parentId,
            int courseId,
            int capacity,
            RoomAssignment[] possibleRooms,
            ScheduleAssignment[] possibleSchedules)
        {
            Id = id;
            ParentId = parentId;
            CourseId = courseId;
            Capacity = capacity;
            PossibleRooms = possibleRooms;
            PossibleSchedules = possibleSchedules;
        }

        public readonly int Id;

        public readonly int ParentId;

        public readonly int CourseId;

        public readonly int Capacity;

        public readonly RoomAssignment[] PossibleRooms;

        public readonly ScheduleAssignment[] PossibleSchedules;

        public int FindRoom(int id)
        {
            return PossibleRooms.IndexOf(r => r.Id == id);
        }

        public int FindSchedule(int start, uint days, uint weeks)
        {
            return PossibleSchedules.IndexOf(s => s.Start == start && s.Days == days && s.Weeks == weeks);
        }
    }

    public class ClassData : Class
    {
        public ClassData(
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
            : base(id, parentId, courseId, capacity, possibleRooms, possibleSchedules)
        {
            CommonConstraints = commonConstraints.ToArray();
            TimeConstraints = timeConstraints.ToArray();
            RoomConstraints = roomConstraints.ToArray();
            Children = new HashSet<int>(children ?? Enumerable.Empty<int>());
            HasChildren = Children.Count > 0;
        }

        public readonly IConstraint[] CommonConstraints;

        public readonly IConstraint[] TimeConstraints;

        public readonly IConstraint[] RoomConstraints;

        public readonly HashSet<int> Children;

        public readonly bool HasChildren;
    }
}