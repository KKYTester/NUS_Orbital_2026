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
        public Text scoreText;
        public Text statusText;
        public PhoneMotionHttpBridge bridge;
        public PickleballBallController ballController;

        [Header("Gameplay")]
        public int matchPoint = 7;
        public bool enableKeyboardTestShots = false;

        private int playerOneScore;
        private int playerTwoScore;
        private bool inputLocked = true;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Menu;
        public bool IsPlaying => CurrentPhase == GamePhase.Playing;

        private void Start()
        {
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
            if (bridge != null)
            {
                bridge.ResetRacquetNeutralRotations();
            }
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
