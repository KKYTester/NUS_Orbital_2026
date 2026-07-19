using CourtSmasherz;
using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Gameplay")]
    public int playerIndex = 0; // 0 = P1, 1 = P2

    [Header("Visualiser Setting")]
    public float autoMoveSpeed = 1.0f;

    private Vector3 ballDestination;

    private float boxSizeX;
    private Vector3 backDelimitStartPos;
    private const float DetectedShotTypeMaxAgeSeconds = 1.25f;

    private void Start()
    {
        BoxCollider hitBox = GetComponent<BoxCollider>();
        hitBox.isTrigger = true;
        boxSizeX = hitBox.size.x;
        backDelimiter.SetParent(null, true); // Keep back delimiter position the same, so only front should move with player
        backDelimitStartPos = backDelimiter.position;
    }

    float overrideStrength = 0;

    void Update()
    {
        if (destinationVisualiser == null)
        {
            return;
        }

        // Move back delimiter closer to the net as player moves closer to the net
        if (Mathf.Abs(transform.position.x) > 6.10f)
        {
            backDelimiter.position = backDelimitStartPos;
        } else
        {
            float backX = map(Mathf.Abs(transform.position.x), 4.5f, 6.10f, 2.0f, Mathf.Abs(backDelimitStartPos.x));
            if (backDelimitStartPos.x < 0)
            {
                backX *= -1.0f;
            }
            backDelimiter.position = new Vector3(backX, backDelimiter.position.y, backDelimiter.position.z);
        }
        
        Vector3 destination = new Vector3(ballDestination.x, 0, ballDestination.z);

        destinationVisualiser.position = Vector3.MoveTowards(destinationVisualiser.position, destination, autoMoveSpeed * Time.deltaTime);
    
        // For testing
        // if (!isHitAlready && Keyboard.current.qKey.wasPressedThisFrame)
        // {
        //     overrideStrength = 10;
        // } else if (!isHitAlready && Keyboard.current.wKey.wasPressedThisFrame)
        // {
        //     overrideStrength= -10;
        // }
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

        // For testing
        // if (Mathf.Abs(overrideStrength) > 0.001)
        // {
        //     strength = overrideStrength;
        //     overrideStrength = 0;
        // }

        if (Mathf.Abs(strength) < minPhoneAcceleration)
        {
            return;
        }
        Vector3 boxFront = transform.position + transform.forward * boxSizeX/2.0f;
        float distFromColliderFront = Mathf.Abs(other.transform.position.x - boxFront.x);
        // Find how much of the capsule's radius has been covered
        float ratio = distFromColliderFront / boxSizeX;
        Debug.Log(ratio);
        float shotYaw;
        if (shotYawDecider(ratio, out shotYaw) == false)
        {
            return; // miss
        }   

        ShotType collisionShotType = GetPhysicalCollisionShotType(strength);

        if (collisionShotType == ShotType.Backhand)
        {
            // Backhand, so reverse direction
            shotYaw *= -1;
        }

        strength = Mathf.Abs(strength) * GetShotStrengthMultiplier(collisionShotType);
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
        
        float flightTime = GetShotFlightTime(collisionShotType);
        Vector3 velo = CalculateVelocityToTarget(other.transform.position, ballDestination, flightTime, ball.gravity);
        Rigidbody ballRb = other.GetComponent<Rigidbody>();
        ballRb.linearVelocity = velo;

        ball.RegisterHit(playerIndex);

        destinationVisualiser.position = new Vector3(other.transform.position.x, 0, other.transform.position.z);
        isHitAlready = true; // Prevent double hits while ball hasnt exited collider
    }

    public bool SimulateShot(ShotEvent shot)
    {
        if (ball == null || frontDelimiter == null || backDelimiter == null)
        {
            return false;
        }

        Rigidbody ballRb = ball.GetComponent<Rigidbody>();
        if (ballRb == null)
        {
            return false;
        }

        if (boxSizeX <= 0f)
        {
            BoxCollider hitBox = GetComponent<BoxCollider>();
            if (hitBox == null)
            {
                return false;
            }

            hitBox.isTrigger = true;
            boxSizeX = hitBox.size.x;
        }

        bool isService = ball.CanFallbackServe(playerIndex);
        if (!isService && !CanSimulateFallbackShot())
        {
            return false;
        }

        float power = Mathf.Clamp01(shot.Power <= 0f ? 0.75f : shot.Power);
        float strength = Mathf.Lerp(minPhoneAcceleration + 0.5f, 12f, power);
        float ratio = GetManualHitRatio(shot);
        float shotYaw;
        if (shotYawDecider(ratio, out shotYaw) == false)
        {
            return false;
        }

        if (shot.ShotType == ShotType.Backhand)
        {
            shotYaw *= -1f;
        }

        strength *= GetShotStrengthMultiplier(shot.ShotType);

        if (isService)
        {
            Vector3 contactPosition = transform.position + transform.forward * (boxSizeX * 0.25f);
            contactPosition.y = Mathf.Max(ball.transform.position.y, ball.ballRadius + 0.35f);
            ball.transform.position = contactPosition;
        }

        Vector3 shotVector = Quaternion.AngleAxis(shotYaw, Vector3.up) * transform.forward;
        Vector3 currPos = transform.position;
        float distance = getDistance(shotVector, currPos, strength, frontDelimiter.position.x, backDelimiter.position.x);
        ballDestination = shotVector * distance;
        ballDestination.x += currPos.x;
        ballDestination.z += currPos.z;
        ballDestination.y = ball.ballRadius;

        float flightTime = GetShotFlightTime(shot.ShotType);
        ballRb.isKinematic = false;
        ballRb.linearVelocity = CalculateVelocityToTarget(ball.transform.position, ballDestination, flightTime, ball.gravity);
        ballRb.angularVelocity = new Vector3(shot.Spin * 12f, shot.Direction * 10f, -shot.Spin * 18f);

        ball.RegisterHit(playerIndex);

        if (destinationVisualiser != null)
        {
            destinationVisualiser.position = new Vector3(ball.transform.position.x, 0, ball.transform.position.z);
        }

        isHitAlready = true;
        return true;
    }

    private bool CanSimulateFallbackShot()
    {
        BoxCollider hitBox = GetComponent<BoxCollider>();
        if (hitBox == null)
        {
            return false;
        }

        Bounds fallbackWindow = hitBox.bounds;
        float reachPadding = Mathf.Max(ball.ballRadius * 2f, 0.25f);
        fallbackWindow.Expand(new Vector3(reachPadding, reachPadding, reachPadding));
        return fallbackWindow.Contains(ball.transform.position);
    }

    private float GetManualHitRatio(ShotEvent shot)
    {
        float manualDirection = GetFallbackShotDirection(shot);

        return Mathf.Lerp(0.25f, 0.75f, (manualDirection + 1f) * 0.5f);
    }

    private float GetFallbackShotDirection(ShotEvent shot)
    {
        if (shot.ShotType == ShotType.Forehand || shot.ShotType == ShotType.Backhand)
        {
            return playerIndex == 0 ? -0.45f : 0.45f;
        }

        return Mathf.Clamp(shot.Direction, -1f, 1f);
    }

    private ShotType GetPhysicalCollisionShotType(float strength)
    {
        if (racket != null && racket.HasRecentDetectedShotType(DetectedShotTypeMaxAgeSeconds))
        {
            return racket.DetectedShotType;
        }

        return strength < 0f ? ShotType.Backhand : ShotType.Forehand;
    }

    private float GetShotStrengthMultiplier(ShotType shotType)
    {
        if (shotType == ShotType.Lob)
        {
            return 0.85f;
        }

        if (shotType == ShotType.Smash)
        {
            return 1.25f;
        }

        return 1f;
    }

    private float GetShotFlightTime(ShotType shotType)
    {
        if (shotType == ShotType.Lob)
        {
            return 1.65f;
        }

        if (shotType == ShotType.Smash)
        {
            return 0.75f;
        }

        return 1.2f;
    }

    private float map(float input, float inputMin, float inputMax, float outputMin, float outputMax)
    {
        input = Mathf.Clamp(input, inputMin, inputMax);
        float outInRatio = (outputMax - outputMin) / (inputMax - inputMin);
        return (input - inputMin) * outInRatio + outputMin;
    }

    // Returns shot yaw angle in degrees, taking front of hitbox as 0. To the left is -ve, to the right is +ve 
    private bool shotYawDecider(float ratio, out float shotYaw)
    {
        if (ratio < 0.1)
        {
            // Early = Crosscourt hit
            shotYaw = map(ratio, 0.0f, 0.1f, -20, -15);
            return true;
        } else if (ratio < 0.4)
        {
            // Slightly early = Normal(?) hit
            shotYaw = map(ratio, 0.1f, 0.4f, -15, -5);
            return true;
        } else if (ratio < 0.6)
        {
            // Perfect = Straight down the line
            shotYaw = map(ratio, 0.4f, 0.6f, -2.5f, 2.5f);
            return true;
        } else if (ratio < 0.8)
        {
            // Late = hit away from racket direction
            shotYaw = map(ratio, 0.6f, 0.8f, 10, 20);
            return true;
        }
        shotYaw = 0;
        // Miss
        return false;
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
