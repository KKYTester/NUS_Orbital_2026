using UnityEngine;

public class TestHitEmulator : MonoBehaviour
{
    public float magnitude = 10.0f;
    public Vector3 direction;

    public float yMulti = 0.5f;
    public float zMulti = 0.5f; 

    private Transform arrow;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        arrow = GetComponentInChildren<Transform>();
        direction = arrow.rotation.eulerAngles;
    }

    // Update is called once per frame
    void Update()
    {
        arrow.rotation = Quaternion.Euler(direction);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            Vector3 forceDirection = arrow.rotation * Vector3.left;
            
            // Reduce up/down and left/right components
            // Currently x is the length of the court, so will reduce y and z components
            Vector3 modifiedForceDirection =new Vector3(forceDirection.x, forceDirection.y * yMulti, forceDirection.z * zMulti).normalized;
            Debug.Log($"{magnitude} || {forceDirection} || {modifiedForceDirection}");
            rb.AddForce(magnitude * modifiedForceDirection, ForceMode.Impulse);
        }
    }
}
