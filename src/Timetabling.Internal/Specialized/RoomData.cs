namespace Timetabling.Internal.Specialized
{
    public class RoomData : Room
    {
        internal RoomData(
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
