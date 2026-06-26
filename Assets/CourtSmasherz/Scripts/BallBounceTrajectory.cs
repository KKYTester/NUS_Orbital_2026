using System.Collections.Generic;
using CourtSmasherz;
using UnityEngine;

public class BallBounceTrajectory : MonoBehaviour
{
    [Header("References")]
    public PickleballBallController ballController;
    public Rigidbody ballRb;
    public LineRenderer lineRenderer;

    public int steps = 1000;
    public float timeStep = 0.03f;
    public float effectiveBounceMultiplier = 0.5f;
    public float bounceTolerance = 0.05f; // in ms^-1
    public LayerMask collisionMask;

    // Track no. of bounces after racket hit
    private int bounceCount = 0;
    private const int maxBounceCount = 1;
    private bool CanDraw = false;

    // Variables for player movement controller to determine point to move to
    public List<Vector3> BallPredictedPoints{ get; private set;}
    [HideInInspector]
    public bool IsHit = false;

    void Update()
    {
        DrawTrajectory();
    }

    void DrawTrajectory()
    {
        Vector3 position = ballRb.position;
        Vector3 velocity = ballRb.linearVelocity;

        List<Vector3> points = new List<Vector3>();
        
        for (int i = 0; i < steps; i++)
        {
            if (bounceCount > maxBounceCount)
            {
                break;
            }
            if (IsValidPoint(position))
            {
                points.Add(position);
            }
            Vector3 nextVelocity = velocity + ballController.gravity * timeStep;
            Vector3 nextPosition = position + nextVelocity * timeStep;

            Vector3 direction = nextPosition - position;
            float distance = direction.magnitude;

            if (distance > 0.0001f &&
                Physics.SphereCast(position, ballController.ballRadius, direction.normalized, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                bounceCount++;
                position = hit.point + hit.normal * ballController.ballRadius;

                // Multiply vertical velocity by bounce multiplier
                // (shortcut as compared to using normal because floor normal is always parallel to y axis)
                // also reflects the velocity off the floor
                velocity.y *= -effectiveBounceMultiplier;

                // Remove small bounces
                if (velocity.magnitude < bounceTolerance)
                {
                    velocity = Vector3.zero;
                }
            }
            else
            {
                position = nextPosition;
                velocity = nextVelocity;
            }
        }
        if (CanDraw)
        {
            BallPredictedPoints = new List<Vector3>(points);
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
            CanDraw = false;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("P1_Racket") || other.CompareTag("P2_Racket"))
        {
            // Reset bounces no. tracker when hit by racket
            bounceCount = 0;
            CanDraw = true;
            IsHit = true; // For player controller to reset after movement
        }
    }

    // Helper function for checking that a point is not infinity
    private bool IsValidPoint(Vector3 point)
    {
        return float.IsFinite(point.x) && float.IsFinite(point.y) && float.IsFinite(point.z);
    }

    public void ClearPrediction()
    {
        bounceCount = 0;
        CanDraw = false;
        IsHit = false;

        if (BallPredictedPoints != null)
        {
            BallPredictedPoints.Clear();
        }

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }
}