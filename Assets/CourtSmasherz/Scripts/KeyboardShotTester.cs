using UnityEngine;

namespace CourtSmasherz
{
    public class KeyboardShotTester : MonoBehaviour
    {
        [TextArea]
        public string controls =
            "P1: A forehand, S backhand, D lob, F smash\n" +
            "P2: J forehand, K backhand, L lob, ; smash\n" +
            "R restarts after match end";
    }
}
