namespace Timetabling.Internal
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
    }
}
