namespace Timetabling.Internal
{
    public struct TravelTime
    {
        public TravelTime(int roomId, int value)
        {
            RoomId = roomId;
            Value = value;
        }

        public readonly int RoomId;

        public readonly int Value;
    }
}