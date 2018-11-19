namespace Timetabling.Common.SolutionModel
{
    public class ClassState
    {
        public ClassState(
            int room,
            int time,
            int attendees,
            double classCapacityPenalty,
            double roomCapacityPenalty,
            double roomUnavailablePenalty)
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

        public readonly double ClassCapacityPenalty;

        public readonly double RoomCapacityPenalty;

        public readonly double RoomUnavailablePenalty;

        public ClassState WithAttendees(int attendees, double classCapacityPenalty, double roomCapacityPenalty)
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