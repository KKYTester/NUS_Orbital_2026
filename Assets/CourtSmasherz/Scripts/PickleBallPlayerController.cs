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
        public float autoMoveSpeed = 1.0f; // should me in ms^-1
        public float predictedPointTolerance = 0.2f;
        public float maxBallHittableHeight = 2.0f;
        public float minBallHittableHeight  = 0.1f;
        public float minZ = -4.0f;
        public float maxZ = 4.0f;
        public float minX = -6.6f;
        public float maxX = 6.6f;

        [Header("References")]
        public Transform racketTransform;
        public Transform ball;
        public Rigidbody ballRb;
        public PickleballBallController ballController;
        public BallBounceTrajectory trajectory;
        public CourtSmasherzGameManager gameManager;

        private Vector3 targetPos;

        public void Start()
        {
            if (ball == null || gameManager == null || trajectory == null || racketTransform == null)
            {
                Debug.Log("Missing attachment");
                return;
            }
            targetPos = transform.position;
        }
        public void Update()
        {
            if (gameManager.CurrentPhase != CourtSmasherzGameManager.GamePhase.Playing)
            {
                return;
            }

            if (playerIndex == 0 || playerIndex == 1)
            {
                Vector3 P1TargetBuff;
                if (trajectory.IsHit && FindMovementPoint(out P1TargetBuff, racketTransform.position))
                {
                    Vector3 offset = racketTransform.position - transform.position;

                    targetPos = new Vector3(
                        P1TargetBuff.x - offset.x,
                        transform.position.y,
                        P1TargetBuff.z - offset.z
                    );

                    targetPos.z = Mathf.Clamp(targetPos.z, minZ, maxZ);
                    targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);

                    trajectory.IsHit = false;
                }

                transform.position = Vector3.MoveTowards(transform.position,
                    targetPos, autoMoveSpeed * Time.deltaTime);
            }

            transform.rotation = Quaternion.Euler(0f, facingY, 0f);
        }

        private bool FindMovementPoint(out Vector3 target, Vector3 currPos)
        {
            target = transform.position;

            if (trajectory.BallPredictedPoints == null)
                return false;

            // Check that ball is moving towards the player before moving
            if (playerIndex == 0 && ballRb.linearVelocity.x > 0f)
                return false;

            if (playerIndex == 1 && ballRb.linearVelocity.x < 0f)
                return false;

            Vector2 currPosXZ = new Vector2(currPos.x, currPos.z);

            for (int i = 0; i < trajectory.BallPredictedPoints.Count; i++)
            {
                Vector3 predictedPoint = trajectory.BallPredictedPoints[i];

                // Check side first
                if (playerIndex == 0 && predictedPoint.x >= -4.5f)
                {
                    continue;
                } else if (playerIndex == 1 && predictedPoint.x <= 4.5f)
                {
                    continue;
                }

                // Check height
                if (!withinHittableHeight(predictedPoint.y))
                    continue;

                Vector2 ballPosXZ = new Vector2(predictedPoint.x, predictedPoint.z);
                float horizontalDist = Vector2.Distance(currPosXZ, ballPosXZ);

                float timeUntilPoint = trajectory.timeStep * i;
                float reachableRadius = autoMoveSpeed * timeUntilPoint;

                if (horizontalDist <= reachableRadius + predictedPointTolerance)
                {
                    target = predictedPoint;
                    return true;
                }
            }

            return false;
        }

        private bool withinHittableHeight(float ballHeight)
        {
            return ballHeight < maxBallHittableHeight && ballHeight > minBallHittableHeight;
        }

        public void RespawnAt(Vector3 respawnPosition)
        {
            transform.position = respawnPosition;
            targetPos = respawnPosition;

            trajectory.ClearPrediction();
        }
    }
}