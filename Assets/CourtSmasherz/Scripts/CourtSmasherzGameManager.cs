using System.Collections;
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
        public PickleballPlayerController playerOneController;
        public PickleballPlayerController playerTwoController;

        [Header("Gameplay")]
        public int matchPoint = 7;
        public bool enableKeyboardTestShots = false;

        private int playerOneScore;
        private int playerTwoScore;
        private bool inputLocked = true;
        private int servingPlayerIndex = 0;
        private const float ServeResetDelaySeconds = 2f;
        private Coroutine serveResetRoutine;
        private global::RacketForceApplier[] racketForceAppliers;

        public GamePhase CurrentPhase { get; private set; } = GamePhase.Menu;
        public bool IsPlaying => CurrentPhase == GamePhase.Playing;

        private void Start()
        {
            if (ballController != null)
            {
                ballController.OnPointScored += AwardPoint;
                ballController.OnSideOut += SideOutToPlayer;
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
                ballController.OnSideOut -= SideOutToPlayer;
                ballController.OnStatusChanged -= SetStatus;
            }
        }

        public void ApplyShot(ShotEvent shot)
        {
            // Currently out of use function
            if (inputLocked || CurrentPhase != GamePhase.Playing)
            {
                return;
            }

            if (ballController == null)
            {
                return;
            }
        }

        public void ApplyFallbackShot(ShotEvent shot)
        {
            if (inputLocked || CurrentPhase != GamePhase.Playing)
            {
                return;
            }

            global::RacketForceApplier racketForceApplier = FindRacketForceApplier(shot.PlayerIndex);
            if (racketForceApplier != null)
            {
                racketForceApplier.SimulateShot(shot);
            }
        }

        private global::RacketForceApplier FindRacketForceApplier(int playerIndex)
        {
            if (racketForceAppliers == null || racketForceAppliers.Length == 0)
            {
                racketForceAppliers = FindObjectsByType<global::RacketForceApplier>(FindObjectsSortMode.None);
            }

            for (int i = 0; i < racketForceAppliers.Length; i++)
            {
                if (racketForceAppliers[i] != null && racketForceAppliers[i].playerIndex == playerIndex)
                {
                    return racketForceAppliers[i];
                }
            }

            racketForceAppliers = FindObjectsByType<global::RacketForceApplier>(FindObjectsSortMode.None);
            for (int i = 0; i < racketForceAppliers.Length; i++)
            {
                if (racketForceAppliers[i] != null && racketForceAppliers[i].playerIndex == playerIndex)
                {
                    return racketForceAppliers[i];
                }
            }

            return null;
        }

        public void ResetMatch()
        {
            CancelServeResetDelay();
            playerOneScore = 0;
            playerTwoScore = 0;
            servingPlayerIndex = 0;

            CurrentPhase = GamePhase.Playing;
            SetInputLocked(false);

            if (ballController != null)
            {
                RespawnPlayersForServe();
                ballController.SpawnForServe(servingPlayerIndex);
            }

            SetStatus(enableKeyboardTestShots
                ? "Match started.\nP1 serving.\nKeyboard test shots are enabled."
                : "Match started.\nP1 serving.");

            UpdateHud();
        }

        public void ShowMenu()
        {
            CancelServeResetDelay();
            playerOneScore = 0;
            playerTwoScore = 0;
            CurrentPhase = GamePhase.Menu;
            SetInputLocked(true);
            if (ballController != null)
            {
                RespawnPlayersForServe();
                ballController.SpawnForServe(0);
            }
            SetStatus("");
            UpdateHud();
        }

        public void WaitForSwings()
        {
            CancelServeResetDelay();
            CurrentPhase = GamePhase.WaitingForSwings;
            SetInputLocked(true);
            if (ballController != null)
            {
                RespawnPlayersForServe();
                ballController.SpawnForServe(0);
            }
            SetStatus("");
            UpdateHud();
        }

        public void BeginMatch()
        {
            CancelServeResetDelay();
            if (bridge != null)
            {
                bridge.ResetRacquetNeutralRotations();
            }

            playerOneScore = 0;
            playerTwoScore = 0;
            servingPlayerIndex = 0;

            CurrentPhase = GamePhase.Playing;
            SetInputLocked(false);

            if (ballController != null)
            {
                RespawnPlayersForServe();
                ballController.SpawnForServe(servingPlayerIndex);
            }

            SetStatus("Both phones ready.\nMatch started.\nP1 serving.");
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

            servingPlayerIndex = playerIndex;

            SetStatus($"P{playerIndex + 1} scores and continues serving");

            if (HasWinner(playerIndex))
            {
                CurrentPhase = GamePhase.Finished;
                SetInputLocked(true);
                SetStatus($"P{playerIndex + 1} wins.\nPress R to restart.");
            }
            else
            {
                StartServeResetDelay();
            }

            UpdateHud();
        }

        private void SideOutToPlayer(int newServerIndex)
        {
            servingPlayerIndex = newServerIndex;

            SetStatus($"Side out. P{newServerIndex + 1} now serves");
            StartServeResetDelay();

            UpdateHud();
        }

        private void StartServeResetDelay()
        {
            SetInputLocked(true);
            CancelServeResetDelay();
            serveResetRoutine = StartCoroutine(ServeResetAfterDelay());
        }

        private IEnumerator ServeResetAfterDelay()
        {
            yield return new WaitForSeconds(ServeResetDelaySeconds);
            serveResetRoutine = null;

            if (CurrentPhase != GamePhase.Playing)
            {
                yield break;
            }

            if (ballController != null)
            {
                RespawnPlayersForServe();
                ballController.SpawnForServe(servingPlayerIndex);
            }

            SetInputLocked(false);
            UpdateHud();
        }

        private void CancelServeResetDelay()
        {
            if (serveResetRoutine == null)
            {
                return;
            }

            StopCoroutine(serveResetRoutine);
            serveResetRoutine = null;
        }

        private void RespawnPlayersForServe()
        {
            if (playerOneController != null)
            {
                playerOneController.RespawnAt(new Vector3(-6f, 0f, 0f));
            }

            if (playerTwoController != null)
            {
                playerTwoController.RespawnAt(new Vector3(6f, 0f, 0f));
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
