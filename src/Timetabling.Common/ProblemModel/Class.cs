using System.Collections.Generic;
using System.Linq;

namespace Timetabling.Common.ProblemModel
{
    public class Class
    {
        public Class(
            int id,
            int courseId,
            RoomAssignment[] possibleRooms,
            ScheduleAssignment[] possibleSchedules)
        {
            Id = id;
            CourseId = courseId;
            PossibleRooms = possibleRooms;
            PossibleSchedules = possibleSchedules;
        }

        public int Id { get; }

        public int CourseId { get; }

        public RoomAssignment[] PossibleRooms { get; }

        public ScheduleAssignment[] PossibleSchedules { get; }
    }

    public class ClassData : Class
    {
        public ClassData(
            int id,
            int courseId,
            RoomAssignment[] possibleRooms,
            ScheduleAssignment[] possibleSchedules,
            IEnumerable<IConstraint> commonConstraints,
            IEnumerable<IConstraint> timeConstraints,
            IEnumerable<IConstraint> roomConstraints)
            : base(id, courseId, possibleRooms, possibleSchedules)
        {
            CommonConstraints = commonConstraints.ToArray();
            TimeConstraints = timeConstraints.ToArray();
            RoomConstraints = roomConstraints.ToArray();
        }

        public IConstraint[] CommonConstraints { get; }

        public IConstraint[] TimeConstraints { get; }

        public IConstraint[] RoomConstraints { get; }
    }
}