using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace CourtSmasherz
{
    public class CourtSmasherzGameManager : MonoBehaviour
    {
        [Header("Scene References")]
        public Transform ball;
        public Transform playerOneRoot;
        public Transform playerTwoRoot;
        public Transform playerOneRacquet;
        public Transform playerTwoRacquet;
        public Text scoreText;
        public Text statusText;

        [Header("Court")]
        public float leftOutX = -9.25f;
        public float rightOutX = 9.25f;
        public float minZ = -4.4f;
        public float maxZ = 4.4f;
        public float playerOneX = -7.3f;
        public float playerTwoX = 7.3f;

        [Header("Gameplay")]
        public int matchPoint = 7;
        public float autoMoveSpeed = 8f;
        public float hitWindowX = 1.55f;
        public float hitWindowZ = 1.45f;
        public bool enableKeyboardTestShots = false;

        private Quaternion playerOneRacquetBaseRotation;
        private Quaternion playerTwoRacquetBaseRotation;

        private Vector3 ballVelocity;
        private int playerOneScore;
        private int playerTwoScore;
        private bool matchFinished;

        private void Start()
        {
            if (playerOneRacquet != null)
            {
                playerOneRacquetBaseRotation = playerOneRacquet.localRotation;
            }

            if (playerTwoRacquet != null)
            {
                playerTwoRacquetBaseRotation = playerTwoRacquet.localRotation;
            }

            ResetMatch();
        }

        private void Update()
        {
            if (matchFinished)
            {
                if (WasPressed(Keyboard.current?.rKey))
                {
                    ResetMatch();
                }
                return;
            }

            UpdateAutomaticPlayerMovement();
            UpdateBall();
            if (enableKeyboardTestShots)
            {
                UpdateKeyboardTestShots();
            }
            UpdateHud();
        }

        public void ApplyShot(ShotEvent shot)
        {
            Transform racquet = shot.PlayerIndex == 0 ? playerOneRacquet : playerTwoRacquet;
            if (racquet == null || ball == null)
            {
                return;
            }

            bool nearPaddleX = Mathf.Abs(ball.position.x - racquet.position.x) <= hitWindowX;
            bool nearPaddleZ = Mathf.Abs(ball.position.z - racquet.position.z) <= hitWindowZ;
            if (!nearPaddleX || !nearPaddleZ)
            {
                SetStatus($"P{shot.PlayerIndex + 1} mistimed {shot.ShotType}");
                return;
            }

            float side = shot.PlayerIndex == 0 ? 1f : -1f;
            float clampedPower = Mathf.Clamp01(shot.Power);
            float speed = Mathf.Lerp(7f, 14f, clampedPower);
            float zCurve = Mathf.Clamp(shot.Direction + shot.Spin * 0.35f, -1f, 1f) * 4.2f;

            if (shot.ShotType == ShotType.Lob)
            {
                zCurve *= 0.45f;
                speed *= 0.78f;
            }
            else if (shot.ShotType == ShotType.Smash)
            {
                speed *= 1.25f;
                zCurve *= 0.65f;
            }

            ball.position = new Vector3(racquet.position.x + side * 0.9f, 0.55f, racquet.position.z);
            ballVelocity = new Vector3(side * speed, 0f, zCurve);
            SetStatus($"P{shot.PlayerIndex + 1} {shot.ShotType} ({Mathf.RoundToInt(clampedPower * 100f)}%)");
        }

        public void ApplyPhoneMotion(int playerIndex, Vector3 acceleration, Vector3 rotationRate, Vector3 orientation)
        {
            Transform racquet = playerIndex == 0 ? playerOneRacquet : playerTwoRacquet;
            if (racquet == null)
            {
                return;
            }

            float side = playerIndex == 0 ? 1f : -1f;
            float tiltZ = Mathf.Clamp(-orientation.z * 0.7f, -50f, 50f);
            float tiltX = Mathf.Clamp(orientation.x * 0.35f, -35f, 35f);
            float twistY = Mathf.Clamp(rotationRate.z * 0.12f, -35f, 35f);
            Quaternion baseRotation = playerIndex == 0 ? playerOneRacquetBaseRotation : playerTwoRacquetBaseRotation;
            racquet.localRotation = baseRotation * Quaternion.Euler(tiltX, side * twistY, tiltZ);
        }

        public void ResetMatch()
        {
            playerOneScore = 0;
            playerTwoScore = 0;
            matchFinished = false;
            ResetBall(1);
            SetStatus(enableKeyboardTestShots
                ? "Match started. Keyboard test shots are enabled."
                : "Match started. Join the Unity room from your phone controller.");
            UpdateHud();
        }

        private void UpdateAutomaticPlayerMovement()
        {
            MovePlayerTowardBall(playerOneRoot, 0);
            MovePlayerTowardBall(playerTwoRoot, 1);
        }

        private void MovePlayerTowardBall(Transform playerRoot, int playerIndex)
        {
            if (playerRoot == null || ball == null)
            {
                return;
            }

            bool ballComingTowardPlayer = playerIndex == 0 ? ballVelocity.x < 0f : ballVelocity.x > 0f;
            bool ballOnPlayerHalf = playerIndex == 0 ? ball.position.x < 0f : ball.position.x > 0f;
            float targetZ = ballComingTowardPlayer || ballOnPlayerHalf
                ? Mathf.Clamp(ball.position.z, minZ, maxZ)
                : Mathf.Clamp(ball.position.z * 0.25f, minZ, maxZ);

            Vector3 target = new Vector3(playerIndex == 0 ? playerOneX : playerTwoX, playerRoot.position.y, targetZ);
            playerRoot.position = Vector3.Lerp(playerRoot.position, target, Time.deltaTime * autoMoveSpeed);
            playerRoot.rotation = Quaternion.Euler(0f, playerIndex == 0 ? 90f : -90f, 0f);
        }

        private void UpdateBall()
        {
            if (ball == null)
            {
                return;
            }

            ball.position += ballVelocity * Time.deltaTime;
            ball.Rotate(Vector3.forward, ballVelocity.magnitude * 80f * Time.deltaTime, Space.World);

            if (ball.position.z < minZ || ball.position.z > maxZ)
            {
                ball.position = new Vector3(ball.position.x, ball.position.y, Mathf.Clamp(ball.position.z, minZ, maxZ));
                ballVelocity.z *= -0.9f;
                SetStatus("Ball bounced off sideline");
            }

            if (ball.position.x < leftOutX)
            {
                AwardPoint(1);
            }
            else if (ball.position.x > rightOutX)
            {
                AwardPoint(0);
            }
        }

        private void AwardPoint(int playerIndex)
        {
            if (playerIndex == 0)
            {
                playerOneScore++;
            }
            else
            {
                playerTwoScore++;
            }

            SetStatus($"P{playerIndex + 1} scores");
            if (HasWinner(playerIndex))
            {
                matchFinished = true;
                SetStatus($"P{playerIndex + 1} wins. Press R to restart.");
            }
            else
            {
                ResetBall(playerIndex == 0 ? 1 : -1);
            }
        }

        private bool HasWinner(int lastScorer)
        {
            int scorer = lastScorer == 0 ? playerOneScore : playerTwoScore;
            int other = lastScorer == 0 ? playerTwoScore : playerOneScore;
            return scorer >= matchPoint && scorer - other >= 2;
        }

        private void ResetBall(int direction)
        {
            if (ball == null)
            {
                return;
            }

            ball.position = new Vector3(0f, 0.55f, 0f);
            ballVelocity = new Vector3(direction * 6.5f, 0f, Random.value > 0.5f ? 1.8f : -1.8f);
        }

        private void UpdateKeyboardTestShots()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (WasPressed(keyboard.aKey)) ApplyShot(new ShotEvent(0, ShotType.Forehand, 0.78f, 0.45f, 0f));
            if (WasPressed(keyboard.sKey)) ApplyShot(new ShotEvent(0, ShotType.Backhand, 0.78f, -0.45f, 0f));
            if (WasPressed(keyboard.dKey)) ApplyShot(new ShotEvent(0, ShotType.Lob, 0.62f, 0f, 0.15f));
            if (WasPressed(keyboard.fKey)) ApplyShot(new ShotEvent(0, ShotType.Smash, 1f, 0f, 0f));

            if (WasPressed(keyboard.jKey)) ApplyShot(new ShotEvent(1, ShotType.Forehand, 0.78f, -0.45f, 0f));
            if (WasPressed(keyboard.kKey)) ApplyShot(new ShotEvent(1, ShotType.Backhand, 0.78f, 0.45f, 0f));
            if (WasPressed(keyboard.lKey)) ApplyShot(new ShotEvent(1, ShotType.Lob, 0.62f, 0f, 0.15f));
            if (WasPressed(keyboard.semicolonKey)) ApplyShot(new ShotEvent(1, ShotType.Smash, 1f, 0f, 0f));
        }

        private bool WasPressed(KeyControl key)
        {
            return key != null && key.wasPressedThisFrame;
        }

        private void UpdateHud()
        {
            if (scoreText != null)
            {
                scoreText.text = $"P1 {playerOneScore}  -  {playerTwoScore} P2";
            }
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
            }
        }
    }
}
