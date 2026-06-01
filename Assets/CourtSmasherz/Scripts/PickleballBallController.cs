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

        public Vector3 Velocity => ballVelocity;

        public Action<int> OnPointScored;
        public Action<string> OnStatusChanged;

        private Rigidbody rb;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            UpdateBall();
        }

        public bool ApplyShot(ShotEvent shot)
        {
            // Transform racquet = shot.PlayerIndex == 0 ?     playerOneRacquet : playerTwoRacquet;

            // if (racquet == null)
            // {
            //     return false;
            // }

            // bool nearPaddleX = Mathf.Abs(transform.position.x - racquet.position.x) <= hitWindowX;
            // bool nearPaddleZ = Mathf.Abs(transform.position.z - racquet.position.z) <= hitWindowZ;

            // if (!nearPaddleX || !nearPaddleZ)
            // {
            //     OnStatusChanged?.Invoke($"P{shot.PlayerIndex + 1} mistimed {shot.ShotType}");
            //     return false;
            // }

            // float side = shot.PlayerIndex == 0 ? 1f : -1f;
            // float clampedPower = Mathf.Clamp01(shot.Power);
            // float speed = Mathf.Lerp(7f, 14f, clampedPower);
            // float zCurve = Mathf.Clamp(shot.Direction + shot.Spin * 0.35f, -1f, 1f) * 4.2f;

            // if (shot.ShotType == ShotType.Lob)
            // {
            //     zCurve *= 0.45f;
            //     speed *= 0.78f;
            // }
            // else if (shot.ShotType == ShotType.Smash)
            // {
            //     speed *= 1.25f;
            //     zCurve *= 0.65f;
            // }

            // transform.position = new Vector3(
            //     racquet.position.x + side * 0.9f,
            //     0.55f,
            //     racquet.position.z
            // );

            // ballVelocity = new Vector3(side * speed, 0f, zCurve);

            // OnStatusChanged?.Invoke(
            //     $"P{shot.PlayerIndex + 1} {shot.ShotType} ({Mathf.RoundToInt(clampedPower * 100f)}%)"
            // );

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
            if (gameManger.CurrentPhase != CourtSmasherzGameManager.GamePhase.Playing)
            {
                return;
            }
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                transform.position = new Vector3(-5.4f, 3f, 0f);
                rb.linearVelocity = Vector3.zero;
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                transform.position = new Vector3(5.4f, 3f, 0f);
                rb.linearVelocity = Vector3.zero;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"Collided with {other.tag}");

            PickleballRacquetController collided_racket = other.GetComponentInParent<PickleballRacquetController>();
            if (collided_racket == null)
            {
                Debug.Log("No RacketController");
                return;
            }
            string racket = null;
            if (other.CompareTag("P1_Racket") || other.CompareTag("P2_Racket"))
            {
                float strength = collided_racket.phoneAccelerationMagnitude;
                Vector3 direction = collided_racket.phoneAccelerationDirection;
                rb.AddForce(direction * strength * hitStrengthMultiplier, ForceMode.Impulse);
                Debug.Log($"{racket}: {strength} || {direction}");
            }   
        }
    }
}