namespace CourtSmasherz
{
    public enum ShotType
    {
        Forehand,
        Backhand,
        Lob,
        Smash
    }

    public struct ShotEvent
    {
        public int PlayerIndex;
        public ShotType ShotType;
        public float Power;
        public float Direction;
        public float Spin;

        public ShotEvent(int playerIndex, ShotType shotType, float power, float direction, float spin)
        {
            PlayerIndex = playerIndex;
            ShotType = shotType;
            Power = power;
            Direction = direction;
            Spin = spin;
        }
    }
}
