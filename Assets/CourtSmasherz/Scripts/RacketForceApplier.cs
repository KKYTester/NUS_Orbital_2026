using CourtSmasherz;
using UnityEngine;

/*
    Apply this script on the GameObject with the hitbox that you want
    for the racket hits
*/

public class RacketForceApplier : MonoBehaviour
{
    [Header("Force Controls")]
    public ForceMode forceModeSelector = ForceMode.Impulse;
    public float hitStrengthMultiplier = 1.0f;
    public Vector3 direction;

    public float yMulti = 0.1f;
    public float zMulti = 0.5f;
    public float minPhoneAcceleration = 5.0f;

    [Header("References")]
    public PickleballRacquetController racket;
    public PickleballBallController ball;
    public Transform destinationVisualiser;
    public Transform frontDelimiter;
    public Transform backDelimiter;

    [Header("Visualiser Setting")]
    public float autoMoveSpeed = 1.0f;

    private Vector3 ballDestination;

    private float boxSizeX;

    private void Start()
    {
        BoxCollider hitBox = GetComponent<BoxCollider>();
        hitBox.isTrigger = true;
        boxSizeX = hitBox.size.x;
    }

    void Update()
    {
        if (destinationVisualiser == null)
        {
            return;
        }
        
        Vector3 destination = new Vector3(ballDestination.x, 0, ballDestination.z);

        destinationVisualiser.position = Vector3.MoveTowards(destinationVisualiser.position, destination, autoMoveSpeed * Time.deltaTime);
    }

    bool isHitAlready;

    private void OnTriggerEnter(Collider other)
    {
        if (racket == null)
        {
            Debug.Log("No racket selected");
            return;
        }
        if (!other.CompareTag("Ball"))
        {
            return;
        }

        isHitAlready = false;
    }

    private void OnTriggerStay(Collider other)
    {
        if (racket == null)
        {
            Debug.Log("No racket selected");
            return;
        }

        if (!other.CompareTag("Ball") || isHitAlready)
        {
            return;
        }

        float strength = racket.phoneAccelerationMagnitude;

        if (Mathf.Abs(strength) < minPhoneAcceleration)
        {
            return;
        }
        Vector3 boxFront = transform.position + transform.forward * boxSizeX/2.0f;
        float distFromColliderFront = other.transform.position.x - boxFront.x;
        Debug.Log($"boxFront: {boxFront} | ballX: {other.transform.position.x}");
        // Find how much of the capsule's radius has been covered
        float ratio = distFromColliderFront / boxSizeX;
        float shotYaw = shotYawDecider(ratio);
        if (shotYaw == 180)
        {
            return; // miss
        }
        if (strength < 0)
        {
            // Backhand, so reverse direction
            shotYaw *= -1;
            strength *= -1; // Keep strength positive for force calculation later
        }
        Vector3 shotVector = Quaternion.AngleAxis(shotYaw, Vector3.up) * transform.forward;
        Vector3 currPos = transform.position;
        float distance = getDistance(shotVector, currPos, strength, frontDelimiter.position.x, backDelimiter.position.x);
        // Calculate offset first
        ballDestination = shotVector * distance;
        // add curr position values to offset to get proper destination
        ballDestination.x += currPos.x;
        ballDestination.z += currPos.z;
        // add ball radius
        ballDestination.y = ball.ballRadius;
        
        Vector3 velo = CalculateVelocityToTarget(other.transform.position, ballDestination, 1.2f, ball.gravity);
        Rigidbody ballRb = other.GetComponent<Rigidbody>();
        ballRb.linearVelocity = velo;
        destinationVisualiser.position = new Vector3(other.transform.position.x, 0, other.transform.position.z);
        isHitAlready = true; // Prevent double hits while ball hasnt exited colllider
    }

    private float map(float input, float inputMin, float inputMax, float outputMin, float outputMax)
    {
        input = Mathf.Clamp(input, inputMin, inputMax);
        float outInRatio = (outputMax - outputMin) / (inputMax - inputMin);
        return (input - inputMin) * outInRatio + outputMin;
    }

    // Returns shot yaw angle in degrees, taking front of hitbox as 0. To the left is -ve, to the right is +ve 
    private float shotYawDecider(float ratio)
    {
        if (ratio < 0.1)
        {
            // Early = Crosscourt hit
            return map(ratio, 0.0f, 0.1f, -30, -25);
        } else if (ratio < 0.4)
        {
            // Slightly early = Normal(?) hit
            return map(ratio, 0.1f, 0.4f, -20, -10);
        } else if (ratio < 0.6)
        {
            // Perfect = Straight down the line
            return map(ratio, 0.4f, 0.6f, -2.5f, 2.5f);
        } else if (ratio < 0.8)
        {
            // Late = hit away from racket direction
            return map(ratio, 0.6f, 0.8f, 10, 20);
        }
        // Miss
        return 180.0f;
    }

    private float getDistance(Vector3 shotDirection, Vector3 currPos, float strength, float frontX, float backX)
    {
        float front_t = (frontX - currPos.x) / shotDirection.x;
        Vector2 frontIntersection = new Vector2(currPos.x + shotDirection.x * front_t, currPos.z + shotDirection.z * front_t);
        float back_t = (backX - currPos.x) / shotDirection.x;
        Vector2 backIntersection = new Vector2(currPos.x + shotDirection.x * back_t, currPos.z + shotDirection.z * back_t);
        Vector2 currPosXZ = new Vector2(currPos.x, currPos.z);
        float frontMag = Vector2.Distance(currPosXZ, frontIntersection);
        float backMag = Vector2.Distance(currPosXZ, backIntersection);
        // Debug.Log($"{strength} | back: {backMag};{backX} | front: {frontMag};{frontX} | shot: {shotDirection}.x | curr: {currPos.x}");
        if (frontMag > backMag)
        {
            strength = Mathf.Clamp(strength, backMag, frontMag);
        } else
        {
            strength = Mathf.Clamp(strength, frontMag, backMag);
        }
        
        return strength;
    }

    private Vector3 CalculateVelocityToTarget(Vector3 start, Vector3 target, float time, Vector3 gravity)
    {
        Vector3 displacement = target - start;

        Vector3 velocity = (displacement - 0.5f * gravity * time * time) / time;

        return velocity;
    }
}
