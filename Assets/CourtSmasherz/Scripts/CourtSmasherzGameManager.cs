using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

namespace CourtSmasherz
{
    public class CourtSmasherzGameManager : MonoBehaviour
    {
        public enum GamePhase
        {
            Menu,
            WaitingForSwings,
            Playing,
            Finished
        }

        [Header("Scene References")]
        public Transform ball;
        public Transform playerOneRoot;
        public Transform playerTwoRoot;
        public Transform playerOneRacquet;
        public Transform playerTwoRacquet;
        public Text scoreText;
        public Text statusText;
        public PhoneMotionHttpBridge bridge;
        public PickleballBallController ballController;

        [Header("Court")]
        public float minZ = -4.4f;
        public float maxZ = 4.4f;
        public float playerOneX = -7.3f;
        public float playerTwoX = 7.3f;

        [Header("Gameplay")]
        public int matchPoint = 7;
        public float autoMoveSpeed = 8f;
        public bool enableKeyboardTestShots = false;

        [Header("Phone Racquet Rotation Mapping")]
        public bool usePhoneRacquetRotation = true;

        public bool invertPhonePitch = true;
        public bool invertPhoneYaw = true;
        public bool invertPhoneRoll = false;
        public Vector3 phoneRotationOffsetEuler = Vector3.zero;

        [Range(0f, 1f)]
        public float phoneRotationSmoothing = 0.25f;
        private Quaternion[] phoneNeutralRotations = new Quaternion[2];
        private bool[] hasPhoneNeutralRotation = new bool[2];
        private Quaternion[] racquetNeutralRotations = new Quaternion[2];
        private bool[] hasRacquetNeutralRotation = new bool[2];

        private Quaternion playerOneRacquetBaseRotation;
        private Quaternion playerTwoRacquetBaseRotation;

        private Quaternion[] phoneCalibrationOffsets = new Quaternion[2];
        private bool[] phoneCalibrated = new bool[2];

        private int playerOneScore;
        private int playerTwoScore;
        private bool inputLocked = true;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Menu;
        public bool IsPlaying => CurrentPhase == GamePhase.Playing;

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

            if (ballController != null)
            {
                ballController.OnPointScored += AwardPoint;
                ballController.OnStatusChanged += SetStatus;
            }

            ShowMenu();
        }

        private void Update()
        {
            if (CurrentPhase == GamePhase.Finished)
            {
                if (WasPressed(Keyboard.current?.rKey))
                {
                    if (bridge != null)
                    {
                        bridge.ReturnToJoinMenu();
                    }
                    else
                    {
                        ShowMenu();
                    }
                }

                return;
            }

            if (CurrentPhase != GamePhase.Playing)
            {
                UpdateHud();
                return;
            }

            UpdateAutomaticPlayerMovement();
            if (enableKeyboardTestShots)
            {
                UpdateKeyboardTestShots();
            }
            UpdateHud();
        }

        private void OnDestroy()
        {
            if (ballController != null)
            {
                ballController.OnPointScored -= AwardPoint;
                ballController.OnStatusChanged -= SetStatus;
            }
        }

        public void ApplyShot(ShotEvent shot)
        {
            if (inputLocked || CurrentPhase != GamePhase.Playing)
            {
                return;
            }

            if (ballController == null)
            {
                return;
            }

            ballController.ApplyShot(shot);
        }

        public void ApplyPhoneMotion(int playerIndex, Vector3 acceleration, Vector3 rotationRate, Vector3 orientation,
                                    bool hasQuaternion, Quaternion phoneQuaternion)
        {
            if (!usePhoneRacquetRotation)
            {
                return;
            }

            Transform racquet = playerIndex == 0 ? playerOneRacquet : playerTwoRacquet;

            if (racquet == null)
            {
                return;
            }

            Quaternion rawPhoneRotation;

            if (hasQuaternion)
            {
                    rawPhoneRotation = new Quaternion(
                        phoneQuaternion.x,
                        phoneQuaternion.y,
                        -phoneQuaternion.z,
                        -phoneQuaternion.w
                    );
            }
            else
            {
                float pitch = invertPhonePitch ? -orientation.z : orientation.z;
                float yaw = invertPhoneYaw ? -orientation.x : orientation.x;
                float roll = invertPhoneRoll ? -orientation.y : orientation.y;

                rawPhoneRotation = Quaternion.Euler(pitch, yaw, roll);
            }

            if (!hasPhoneNeutralRotation[playerIndex])
            {
                phoneNeutralRotations[playerIndex] = rawPhoneRotation;
                racquetNeutralRotations[playerIndex] = racquet.localRotation;

                hasPhoneNeutralRotation[playerIndex] = true;
                hasRacquetNeutralRotation[playerIndex] = true;
            }

            Quaternion relativePhoneRotation =
                Quaternion.Inverse(phoneNeutralRotations[playerIndex]) * rawPhoneRotation;

            Quaternion offsetRotation = Quaternion.Euler(phoneRotationOffsetEuler);

            Quaternion targetRotation =
                racquetNeutralRotations[playerIndex] * offsetRotation * relativePhoneRotation;

            racquet.localRotation = Quaternion.Slerp(
                racquet.localRotation,
                targetRotation,
                phoneRotationSmoothing
            );
        }

        public void ResetPhoneNeutralRotations()
        {
            hasPhoneNeutralRotation[0] = false;
            hasPhoneNeutralRotation[1] = false;

            hasRacquetNeutralRotation[0] = false;
            hasRacquetNeutralRotation[1] = false;
        }

        public void ResetMatch()
        {
            playerOneScore = 0;
            playerTwoScore = 0;
            CurrentPhase = GamePhase.Playing;
            SetInputLocked(false);
            if (ballController != null)
            {
                ballController.ResetBall(1, true);
            }
            SetStatus(enableKeyboardTestShots
                ? "Match started. Keyboard test shots are enabled."
                : "Match started. Join the Unity room from your phone controller.");
            UpdateHud();
        }

        public void ShowMenu()
        {
            playerOneScore = 0;
            playerTwoScore = 0;
            CurrentPhase = GamePhase.Menu;
            SetInputLocked(true);
            if (ballController != null)
            {
                ballController.ResetBall(1, false);
            }
            SetStatus("Scan the QR code, join both phones, then press Start.");
            UpdateHud();
        }

        public void WaitForSwings()
        {
            CurrentPhase = GamePhase.WaitingForSwings;
            SetInputLocked(true);
            if (ballController != null)
            {
                ballController.ResetBall(1, false);
            }
            SetStatus("Waiting for P1 and P2 to swing once.");
            UpdateHud();
        }

        public void BeginMatch()
        {
            ResetPhoneNeutralRotations();
            CurrentPhase = GamePhase.Playing;
            SetInputLocked(false);
            if (ballController != null)
            {
                ballController.ResetBall(1, true);
            }
            SetStatus("Both phones ready. Match started.");
            UpdateHud();
        }

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
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

            Vector3 currentBallVelocity = ballController != null ? ballController.Velocity : Vector3.zero;
bool        ballComingTowardPlayer = playerIndex == 0 ? currentBallVelocity.x < 0f : currentBallVelocity.x > 0f;
            bool ballOnPlayerHalf = playerIndex == 0 ? ball.position.x < 0f : ball.position.x > 0f;
            float targetZ = ballComingTowardPlayer || ballOnPlayerHalf
                ? Mathf.Clamp(ball.position.z, minZ, maxZ)
                : Mathf.Clamp(ball.position.z * 0.25f, minZ, maxZ);

            Vector3 target = new Vector3(playerIndex == 0 ? playerOneX : playerTwoX, playerRoot.position.y, targetZ);
            playerRoot.position = Vector3.Lerp(playerRoot.position, target, Time.deltaTime * autoMoveSpeed);
            playerRoot.rotation = Quaternion.Euler(0f, playerIndex == 0 ? 90f : -90f, 0f);
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
                CurrentPhase = GamePhase.Finished;
                SetInputLocked(true);
                SetStatus($"P{playerIndex + 1} wins. Press R to restart.");
            }
            else
            {
                if (ballController != null)
                {
                    ballController.ResetBall(playerIndex == 0 ? 1 : -1, true);   
                }
            }
        }

        private bool HasWinner(int lastScorer)
        {
            int scorer = lastScorer == 0 ? playerOneScore : playerTwoScore;
            int other = lastScorer == 0 ? playerTwoScore : playerOneScore;
            return scorer >= matchPoint && scorer - other >= 2;
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
