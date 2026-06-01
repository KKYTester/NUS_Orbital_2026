using System.Collections.Generic;
using UnityEngine;

public class BallBounceTrajectory : MonoBehaviour
{
    public Rigidbody ballRb;
    public LineRenderer lineRenderer;

    public int steps = 1000;
    public float timeStep = 0.03f;
    public float effectiveBounceMultiplier = 0.5f;
    public float ballRadius = 0.03775f;
    public float bounceTolerance = 0.05f; // in ms^-1
    public LayerMask collisionMask;

    // Track no. of bounces after racket hit
    int bounceCount = 0;
    private int maxBounceCount = 1;

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
            points.Add(position);

            Vector3 nextVelocity = velocity + Physics.gravity * timeStep;
            Vector3 nextPosition = position + nextVelocity * timeStep;

            Vector3 direction = nextPosition - position;
            float distance = direction.magnitude;

            if (Physics.SphereCast(position, ballRadius, direction.normalized, out RaycastHit hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
            {
                bounceCount++;
                Debug.Log(bounceCount);
                position = hit.point + hit.normal * ballRadius;

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
            lineRenderer.positionCount = points.Count;
            lineRenderer.SetPositions(points.ToArray());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("P1_Racket") || other.CompareTag("P2_Racket"))
        {
            // Reset bounces no. tracker when hit by racket
            bounceCount = 0;
            Debug.Log(bounceCount);
        }
    }
}