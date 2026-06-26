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

        [Header("Court Settings")]
        public float netX = 0f;
        public string outOfBoundsTag = "outOfBounds";

        [Header("Ball Settings")]
        public float ballGravity = 9.81f;
        public float ballRadius { get; private set; } = 0.1f; // Note that this does not change the actual ball size
        public Vector3 gravity => ballGravity * Vector3.down;

        [Header("Bounce Detection")]
        public float bounceDebounceTime = 0.08f;

        [Header("Debug")]
        public Vector3 p1BallSpawnLocation =>
            new Vector3(playerOneRacquet.position.x, playerOneRacquet.position.y + 1f, playerOneRacquet.position.z);

        public Vector3 p2BallSpawnLocation =>
            new Vector3(playerTwoRacquet.position.x, playerTwoRacquet.position.y + 1f, playerTwoRacquet.position.z);

        public Action<int> OnPointScored;
        public Action<int> OnSideOut;
        public Action<string> OnStatusChanged;

        private Rigidbody rb;

        private int servingPlayerIndex = 0;
        private int lastHitterIndex = -1;
        private int bounceCountAfterHit = 0;

        private bool rallyEnded = false;
        private float lastBounceTime = -999f;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            // UpdateBallDebugControls();
        }

        private void FixedUpdate()
        {
            rb.AddForce(gravity, ForceMode.Acceleration);
        }

        public void SpawnForServe(int serverIndex)
        {
            servingPlayerIndex = serverIndex;

            rallyEnded = false;
            lastHitterIndex = -1;
            bounceCountAfterHit = 0;
            lastBounceTime = -999f;

            transform.position = serverIndex == 0 ? p1BallSpawnLocation : p2BallSpawnLocation;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            OnStatusChanged?.Invoke($"P{serverIndex + 1} serving");
        }

        public void RegisterHit(int hitterIndex)
        {
            if (rallyEnded)
            {
                return;
            }

            lastHitterIndex = hitterIndex;
            bounceCountAfterHit = 0;

            OnStatusChanged?.Invoke($"P{hitterIndex + 1} hit");
        }

        private void OnCollisionEnter(Collision collision)
        {
            bool landedOut = collision.collider.CompareTag(outOfBoundsTag);

            // Treat any non-racket collision as a bounce.
            // Your racket currently uses trigger logic, so it should not arrive here.
            RegisterBounce(landedOut);
        }

        private void OnTriggerEnter(Collider other)
        {
            // This allows your out-of-bounds zones to be trigger colliders too.
            if (other.CompareTag(outOfBoundsTag))
            {
                RegisterBounce(true);
            }
        }

        private void RegisterBounce(bool landedOut)
        {
            if (rallyEnded)
            {
                return;
            }

            // Ignore bounces before any player has hit the ball.
            // This lets the serve ball simply drop/spawn without causing a fault.
            if (lastHitterIndex < 0)
            {
                return;
            }

            // Prevent one physical bounce from being counted multiple times
            // because of overlapping colliders or rapid collision callbacks.
            if (Time.time - lastBounceTime < bounceDebounceTime)
            {
                return;
            }

            lastBounceTime = Time.time;

            // First bounce after a hit must land in the opponent's side and not out.
            if (bounceCountAfterHit == 0)
            {
                if (landedOut || !LandedOnOpponentSide(lastHitterIndex))
                {
                    // Hitter made an invalid shot.
                    EndRally(OtherPlayer(lastHitterIndex));
                    return;
                }

                bounceCountAfterHit = 1;
                OnStatusChanged?.Invoke("First bounce valid");
                return;
            }

            // Second bounce before opponent hits.
            // Regardless of where the second bounce lands, the hitter wins the rally.
            if (bounceCountAfterHit == 1)
            {
                EndRally(lastHitterIndex);
            }
        }

        private bool LandedOnOpponentSide(int hitterIndex)
        {
            float ballX = transform.position.x;

            if (hitterIndex == 0)
            {
                // P1 hits toward P2's side.
                return ballX > netX;
            }

            // P2 hits toward P1's side.
            return ballX < netX;
        }

        private int OtherPlayer(int playerIndex)
        {
            return playerIndex == 0 ? 1 : 0;
        }

        private void EndRally(int rallyWinnerIndex)
        {
            rallyEnded = true;

            if (rallyWinnerIndex == servingPlayerIndex)
            {
                OnPointScored?.Invoke(rallyWinnerIndex);
            }
            else
            {
                OnSideOut?.Invoke(rallyWinnerIndex);
            }
        }

        private void UpdateBallDebugControls()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                SpawnForServe(0);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                SpawnForServe(1);
            }
        }
    }
}