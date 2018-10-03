namespace Timetabling.Common.SolutionModel
{
    public class ClassState
    {
        public ClassState(
            int room,
            int time,
            int attendees,
            double timeHardPenalty,
            int timeSoftPenalty,
            double commonHardPenalty,
            int commonSoftPenalty,
            double roomHardPenalty,
            int roomSoftPenalty,
            double classCapacityPenalty,
            double roomCapacityPenalty,
            double roomUnavailablePenalty)
        {
            Room = room;
            Time = time;
            Attendees = attendees;
            TimeHardPenalty = timeHardPenalty;
            TimeSoftPenalty = timeSoftPenalty;
            CommonHardPenalty = commonHardPenalty;
            CommonSoftPenalty = commonSoftPenalty;
            RoomHardPenalty = roomHardPenalty;
            RoomSoftPenalty = roomSoftPenalty;
            ClassCapacityPenalty = classCapacityPenalty;
            RoomCapacityPenalty = roomCapacityPenalty;
            RoomUnavailablePenalty = roomUnavailablePenalty;
        }

        public readonly int Room;

        public readonly int Time;

        public readonly int Attendees;

        public readonly double TimeHardPenalty;

        public readonly int TimeSoftPenalty;

        public readonly double CommonHardPenalty;

        public readonly int CommonSoftPenalty;

        public readonly double RoomHardPenalty;

        public readonly int RoomSoftPenalty;

        public readonly double ClassCapacityPenalty;

        public readonly double RoomCapacityPenalty;

        public readonly double RoomUnavailablePenalty;

        public ClassState WithAttendees(int attendees, double classCapacityPenalty, double roomCapacityPenalty)
        {
            return new ClassState(
                Room,
                Time,
                attendees,
                TimeHardPenalty,
                TimeSoftPenalty,
                CommonHardPenalty,
                CommonSoftPenalty,
                RoomHardPenalty,
                RoomSoftPenalty,
                classCapacityPenalty,
                roomCapacityPenalty,
                RoomUnavailablePenalty);
        }
    }
}