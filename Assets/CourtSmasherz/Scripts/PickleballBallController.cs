using System;
using UnityEngine;

namespace CourtSmasherz
{
    public class PickleballBallController : MonoBehaviour
    {
        [Header("References")]
        public Transform playerOneRacquet;
        public Transform playerTwoRacquet;

        [Header("Court Bounds")]
        public float leftOutX = -9.25f;
        public float rightOutX = 9.25f;
        public float minZ = -4.4f;
        public float maxZ = 4.4f;

        [Header("Hit Detection")]
        public float hitWindowX = 1.55f;
        public float hitWindowZ = 1.45f;

        private Vector3 ballVelocity;

        public Vector3 Velocity => ballVelocity;

        public Action<int> OnPointScored;
        public Action<string> OnStatusChanged;

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
            // transform.position += ballVelocity * Time.deltaTime;
            // transform.Rotate(Vector3.forward, ballVelocity.magnitude * 80f * Time.deltaTime, Space.World);

            // if (transform.position.z < minZ || transform.position.z > maxZ)
            // {
            //     transform.position = new Vector3(
            //         transform.position.x,
            //         transform.position.y,
            //         Mathf.Clamp(transform.position.z, minZ, maxZ)
            //     );

            //     ballVelocity.z *= -0.9f;
            //     OnStatusChanged?.Invoke("Ball bounced off sideline");
            // }

            // if (transform.position.x < leftOutX)
            // {
            //     OnPointScored?.Invoke(1);
            // }
            // else if (transform.position.x > rightOutX)
            // {
            //     OnPointScored?.Invoke(0);
            // }
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
            if (other.CompareTag("P1_Racket"))
            {
                racket = "P1 RACKET INFO";
            }
            if (other.CompareTag("P2_Racket"))
            {
                racket = "P2 RACKET INFO";
            }
            Debug.Log($"{racket}: {collided_racket.phoneAccelerationMagnitude}");
        }
    }
}