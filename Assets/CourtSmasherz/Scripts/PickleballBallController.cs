using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CourtSmasherz
{
    public class PickleballBallController : MonoBehaviour
    {
        [Header("References")]
        public Transform playerOneRacquet;
        public Transform playerTwoRacquet;
        public CourtSmasherzGameManager gameManger;

        [Header("Court Bounds")]
        public float leftOutX = -9.25f;
        public float rightOutX = 9.25f;
        public float minZ = -4.4f;
        public float maxZ = 4.4f;

        [Header("Hit Detection")]
        public float hitStrengthMultiplier = 1.0f;

        private Vector3 ballVelocity;

        public Action<int> OnPointScored;
        public Action<string> OnStatusChanged;

        [Header("Ball Settings")]
        public float ballGravity = 9.81f;
        public float ballRadius{ get; private set; } = 0.03775f; // Note that this does not change the actual ball size
        public Vector3 gravity => ballGravity * Vector3.down;

        [Header("Debug")]
        public Vector3 p1BallSpawnLocation = new Vector3(-5.4f, 3f, -0.378f);
        public Vector3 p2BallSpawnLocation = new Vector3(5.4f, 3f, 0.378f);

        private Rigidbody rb;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            UpdateBall();
        }

        private void FixedUpdate()
        {
            rb.AddForce(gravity, ForceMode.Acceleration);
        }

        public bool ApplyShot(ShotEvent shot)
        {
            // Delete this function later
            return true;
        }

        public void ResetBall(int direction, bool serve)
        {
            transform.position = new Vector3(0f, 0.55f, 0f);

            ballVelocity = serve
                ? new Vector3(direction * 6.5f, 0f, UnityEngine.Random.value > 0.5f ? 1.8f : -1.8f)
                : Vector3.zero;
        }

        private void UpdateBall()
        {
            // if (gameManger.CurrentPhase != CourtSmasherzGameManager.GamePhase.Playing)
            // {
            //     return;
            // }
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                transform.position = p1BallSpawnLocation;
                rb.linearVelocity = Vector3.zero;
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                transform.position = p2BallSpawnLocation;
                rb.linearVelocity = Vector3.zero;
            }
        }
    }
}