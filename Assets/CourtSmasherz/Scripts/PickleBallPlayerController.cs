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
        public float minZ = -4.4f;
        public float maxZ = 4.4f;

        [Header("References")]
        
        public Transform racketTransform;
        public Transform ball;
        public PickleballBallController ballController;
        public BallBounceTrajectory trajectory;
        public CourtSmasherzGameManager gameManager;

        private Vector3 P1Target;

        public void Start()
        {
            if (ball == null || gameManager == null || trajectory == null || racketTransform == null)
            {
                Debug.Log("Missing attachment");
                return;
            }
            P1Target = transform.position;
        }
        public void Update()
        {
            if (gameManager.CurrentPhase != CourtSmasherzGameManager.GamePhase.Playing)
            {
                return;
            }

            if (playerIndex == 0)
            {
                Vector3 P1TargetBuff;
                if (trajectory.IsHit && FindMovementPoint(out P1TargetBuff, racketTransform.position) && P1TargetBuff.x < -1.5f)
                {
                    // racket offset from player pos
                    Vector3 offset = racketTransform.position - transform.position;
                    // offset target position by racket's relative position
                    P1Target = new Vector3(P1TargetBuff.x - offset.x,
                        transform.position.y, P1TargetBuff.z - offset.z);
                    trajectory.IsHit = false;
                }

                transform.position = Vector3.MoveTowards(transform.position,
                    P1Target, autoMoveSpeed * Time.deltaTime);
            }

            transform.rotation = Quaternion.Euler(0f, facingY, 0f);
        }

        private bool FindMovementPoint(out Vector3 target, Vector3 currPos)
        {
            target = transform.position;
            if (trajectory.BallPredictedPoints == null)
            {
                return false;
            }

            float reachableRadius;
            float horizontalDist;
            Vector2 currPosXZ = new Vector2(currPos.x, currPos.z);
            for (int i = 0; i < trajectory.BallPredictedPoints.Count; i++)
            {
                reachableRadius = trajectory.timeStep * autoMoveSpeed * i;
                Vector2 ballPosXZ = new Vector2(trajectory.BallPredictedPoints[i].x, trajectory.BallPredictedPoints[i].z);
                horizontalDist = Vector2.Distance(currPosXZ, ballPosXZ);
                
                if (horizontalDist < reachableRadius + predictedPointTolerance
                    && withinHittableHeight(trajectory.BallPredictedPoints[i].y))
                {
                    target = trajectory.BallPredictedPoints[i];
                    return true;
                }
            }
            return false;
        }

        private bool withinHittableHeight(float ballHeight)
        {
            return ballHeight < maxBallHittableHeight && ballHeight > minBallHittableHeight;
        }

        // private void MoveToBall()
    }
}