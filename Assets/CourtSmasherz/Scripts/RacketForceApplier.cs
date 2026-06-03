using CourtSmasherz;
using UnityEngine;

/*
    Apply this script on the GameObject with the hitbox that you want
    for the racket hits
*/

public class RacketForceApplier : MonoBehaviour
{
    public ForceMode forceModeSelector = ForceMode.Impulse;
    public float hitStrengthMultiplier = 1.0f;
    public Vector3 direction;

    public float yMulti = 0.1f;
    public float zMulti = 0.5f; 
    public PickleballRacquetController racket;

    private void Start()
    {
        Collider hitBox = GetComponent<Collider>();
        hitBox.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (racket == null)
        {
            Debug.Log("No racket selected");
            return;
        }
        if (other.CompareTag("Ball"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            Vector3 forceDirection = racket.phoneAccelerationDirection;
            float magnitude = racket.phoneAccelerationMagnitude;
            
            // Reduce up/down and left/right components
            // Currently x is the length of the court, so will reduce y and z components
            Vector3 modifiedForceDirection =new Vector3(forceDirection.x, forceDirection.y * yMulti, forceDirection.z * zMulti).normalized;
            Debug.Log($"{magnitude} || {forceDirection} || {modifiedForceDirection}");
            rb.AddForce(hitStrengthMultiplier * magnitude * modifiedForceDirection, forceModeSelector);
        }
    }
}
