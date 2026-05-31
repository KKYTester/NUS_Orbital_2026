using UnityEngine;

namespace CourtSmasherz
{
    public class PickleballPlayerController : MonoBehaviour
    {
        [Header("Player Settings")]
        public int playerIndex = 0; // 0 = P1, 1 = P2
        public float homeX = -7.3f;
        public float facingY = 90f;

        [Header("Movement")]
        public float autoMoveSpeed = 8f;
        public float minZ = -4.4f;
        public float maxZ = 4.4f;

        [Header("References")]
        public Transform ball;
        public PickleballBallController ballController;
        public CourtSmasherzGameManager gameManager;

        public void Start()
        {
            if (ball == null || gameManager == null)
            {
                Debug.Log("Missing attachment");
                return;
            }
        }
        public void Update()
        {
            if (gameManager.CurrentPhase != CourtSmasherzGameManager.GamePhase.Playing)
            {
                return;
            }

            float targetZ = Mathf.Clamp(ball.position.z, minZ, maxZ);
            Vector3 target = new Vector3(homeX, transform.position.y, targetZ);
            transform.position = Vector3.Lerp(
                transform.position,
                target,
                Time.deltaTime * autoMoveSpeed
            );

            transform.rotation = Quaternion.Euler(0f, facingY, 0f);
        }
    }
}