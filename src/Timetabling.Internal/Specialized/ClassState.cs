namespace Timetabling.Internal.Specialized
{
    internal class ClassState
    {
        internal ClassState(
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

        internal readonly int Room;

        internal readonly int Time;

        internal readonly int Attendees;

        internal readonly int ClassCapacityPenalty;

        internal readonly int RoomCapacityPenalty;

        internal readonly int RoomUnavailablePenalty;

        internal ClassState WithAttendees(int attendees, int classCapacityPenalty, int roomCapacityPenalty)
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
