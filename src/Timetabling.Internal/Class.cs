using System.Linq;
using Timetabling.Internal.Utils;

namespace Timetabling.Internal
{
    public class Class
    {
        public Class(
            int id,
            int parentId,
            int capacity,
            RoomAssignment[] possibleRooms,
            ScheduleAssignment[] possibleSchedules)
        {
            Id = id;
            ParentId = parentId;
            Capacity = capacity;
            PossibleRooms = possibleRooms.OrderBy(r => r.Penalty).ToArray();
            PossibleSchedules = possibleSchedules.OrderBy(s => s.Penalty).ToArray();
        }

        public readonly int Id;

        public readonly int ParentId;

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
}
