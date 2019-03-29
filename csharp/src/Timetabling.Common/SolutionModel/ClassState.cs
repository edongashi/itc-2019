namespace Timetabling.Common.SolutionModel
{
    public class ClassState
    {
        public ClassState(
            int room,
            int time,
            int attendees,
            int classCapacityPenalty,
            int roomCapacityPenalty,
            int roomUnavailablePenalty)
        {
            Room = room;
            Time = time;
            Attendees = attendees;
            ClassCapacityPenalty = classCapacityPenalty;
            RoomCapacityPenalty = roomCapacityPenalty;
            RoomUnavailablePenalty = roomUnavailablePenalty;
        }

        public readonly int Room;

        public readonly int Time;

        public readonly int Attendees;

        public readonly int ClassCapacityPenalty;

        public readonly int RoomCapacityPenalty;

        public readonly int RoomUnavailablePenalty;

        public ClassState WithAttendees(int attendees, int classCapacityPenalty, int roomCapacityPenalty)
        {
            return new ClassState(
                Room,
                Time,
                attendees,
                classCapacityPenalty,
                roomCapacityPenalty,
                RoomUnavailablePenalty);
        }
    }
}