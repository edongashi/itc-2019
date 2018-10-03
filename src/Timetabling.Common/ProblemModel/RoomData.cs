namespace Timetabling.Common.ProblemModel
{
    public class RoomData : Room
    {
        public RoomData(
            int id,
            int capacity,
            Schedule[] unavailableSchedules,
            TravelTime[] travelTimes,
            int[] possibleClasses)
            : base(id, capacity, unavailableSchedules, travelTimes)
        {
            PossibleClasses = possibleClasses;
        }

        public readonly int[] PossibleClasses;
    }
}