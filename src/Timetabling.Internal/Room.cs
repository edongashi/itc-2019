namespace Timetabling.Internal
{
    public class Room
    {
        public Room(int id, int capacity, Schedule[] unavailableSchedules, TravelTime[] travelTimes)
        {
            Id = id;
            Capacity = capacity;
            UnavailableSchedules = unavailableSchedules;
            TravelTimes = travelTimes;
        }

        public readonly int Id;

        public readonly int Capacity;

        public readonly Schedule[] UnavailableSchedules;

        public readonly TravelTime[] TravelTimes;
    }
}
