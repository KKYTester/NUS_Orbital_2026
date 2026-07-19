using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace CourtSmasherz
{
    public class PhoneMotionHttpBridge : MonoBehaviour
    {
        [Header("References")]
        public CourtSmasherzGameManager gameManager;
        public MainMenuController mainMenu;
        public Text roomCodeText;
        public Text bridgeStatusText;
        public Text playerOneMotionStatusText;
        public Text playerTwoMotionStatusText;
        public PickleballRacquetController playerOneRacquetController;
        public PickleballRacquetController playerTwoRacquetController;

        [Header("Server")]
        public string serverBaseUrl = "https://nus-orbital-2026.onrender.com";
        public float pollIntervalSeconds = 0.08f;
        public bool autoStartLocalServer = false;
        public string nodeExecutable = "node";
        public string fallbackNodeExecutable = @"C:\Users\kumar\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe";

        [Header("Unity Motion Classification")]
        public float accelerationShotThreshold = 15f;
        public float rotationShotThreshold = 150f;
        public float smashAccelerationThreshold = 15f;
        public float shotCooldownSeconds = 0.45f;

        private string roomCode;
        private string phoneBaseUrl;
        private string phoneJoinUrl;
        private int lastEventId;
        private readonly float[] lastShotTimes = { -10f, -10f };
        private Process serverProcess;
        private bool readinessCheckActive;
        private bool p1Ready;
        private bool p2Ready;
        private readonly Coroutine[] playerStatusClearRoutines = new Coroutine[2];
        /*        
        Following variable is for Unity to remember if it was the one that started a local server.
        Later when we move Node js to an online host and we set autoStartLocalServer to false,
        this helps us keep track and ensure we do not accidentally kill the online server process       
        */
        private bool startedLocalServerFromUnity;

        private void Start()
        {
            if (gameManager != null)
            {
                gameManager.ShowMenu();
            }

            SetJoinHudVisible(true);
            SetPlayerMotionStatus(0, string.Empty, false);
            SetPlayerMotionStatus(1, string.Empty, false);

            if (mainMenu != null)
            {
                mainMenu.SetJoinInfo(string.Empty, string.Empty);
                mainMenu.HideMenu();
            }

            StartCoroutine(EnsureServerThenCreateRoom());
        }

        private void OnApplicationQuit()
        {
            StopLocalNodeServer();
        }

        private void OnDisable()
        {
            StopLocalNodeServer();
        }

        public void StartReadinessCheck()
        {
            readinessCheckActive = true;
            p1Ready = false;
            p2Ready = false;

            if (gameManager != null)
            {
                gameManager.WaitForSwings();
            }

            SetJoinHudVisible(true);
            SetPlayerMotionStatus(0, string.Empty, false);
            SetPlayerMotionStatus(1, string.Empty, false);

            if (mainMenu != null)
            {
                mainMenu.ShowWaitingForSwings(false, false);
            }

            SetBridgeStatus("");
        }

        // For restarting after a match
        public void ReturnToJoinMenu()
        {
            readinessCheckActive = false;
            p1Ready = false;
            p2Ready = false;

            if (gameManager != null)
            {
                gameManager.ShowMenu();
            }

            SetJoinHudVisible(true);

            SetPlayerMotionStatus(0, string.Empty, false);
            SetPlayerMotionStatus(1, string.Empty, false);

            if (mainMenu != null)
            {
                mainMenu.ShowMenu();
            }

            SetBridgeStatus("Join both phones, then press Start.");
        }

        private IEnumerator EnsureServerThenCreateRoom()
        {
            // Temporarily keep this BaseUrl as backup before /unity/create-room replies
            phoneBaseUrl = autoStartLocalServer
                ? $"http://{GetLocalIPv4Address()}:3000"
                : serverBaseUrl.TrimEnd('/');
            if (roomCodeText != null)
            {
                roomCodeText.text = $"Phone URL: {phoneBaseUrl}/controller.html";
            }

            yield return CheckServerHealth((isHealthy) =>
            {
                if (!isHealthy && autoStartLocalServer)
                {
                    StartLocalNodeServer();
                }
            });

            if (autoStartLocalServer)
            {
                float deadline = Time.time + 4f;
                bool healthy = false;
                while (Time.time < deadline && !healthy)
                {
                    yield return CheckServerHealth((isHealthy) => healthy = isHealthy);
                    if (!healthy)
                    {
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }

            yield return CreateRoomThenPoll();
        }

        private IEnumerator CreateRoomThenPoll()
        {
            SetBridgeStatus("Creating phone room...");
            using UnityWebRequest request = UnityWebRequest.Get($"{serverBaseUrl}/unity/create-room");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetBridgeStatus($"Room failed: {request.error}");
                yield break;
            }

            CreateRoomResponse response = JsonUtility.FromJson<CreateRoomResponse>(request.downloadHandler.text);
            roomCode = response.roomCode;
            // Check for valid "Base" url
            if (!string.IsNullOrEmpty(response.phoneBaseUrl))
            {
                phoneBaseUrl = response.phoneBaseUrl;
            }
            // If we already have a valid direct JoinUrl, just use that
            if (!string.IsNullOrEmpty(response.phoneJoinUrl))
            {
                phoneJoinUrl = response.phoneJoinUrl;
            }
            else
            {
                // Reconstruct join url from BaseUrl
                phoneJoinUrl = $"";
            }
            lastEventId = 0;

            if (roomCodeText != null)
            {
                roomCodeText.text = $"";
            }

            if (mainMenu != null)
            {
                mainMenu.SetJoinInfo(roomCode, phoneJoinUrl);
            }

            SetBridgeStatus($"");
            StartCoroutine(PollEvents());
        }

        private IEnumerator CheckServerHealth(Action<bool> callback)
        {
            using UnityWebRequest request = UnityWebRequest.Get($"{serverBaseUrl}/health");
            request.timeout = 1;
            yield return request.SendWebRequest();
            callback(request.result == UnityWebRequest.Result.Success);
        }

        private void StartLocalNodeServer()
        {
            string projectRoot = GetWorkspaceRoot();
            string serverScript = Path.Combine(projectRoot, "server", "index.js");
            if (!File.Exists(serverScript))
            {
                SetBridgeStatus($"Server script missing: {serverScript}");
                return;
            }

            string executable = ResolveNodeExecutable();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "server/index.js",
                WorkingDirectory = projectRoot,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            try
            {
                serverProcess = Process.Start(startInfo);
                startedLocalServerFromUnity = serverProcess != null;

                SetBridgeStatus("Starting local server...");
                Debug.Log("Started local Node server from Unity.");
            }
            catch (Exception exception)
            {
                startedLocalServerFromUnity = false;
                serverProcess = null;

                SetBridgeStatus($"Could not start server: {exception.Message}");
                Debug.LogException(exception);
            }
        }

        private void StopLocalNodeServer()
        {
            if (!startedLocalServerFromUnity)
            {
                return;
            }

            if (serverProcess == null)
            {
                startedLocalServerFromUnity = false;
                return;
            }

            try
            {
                if (!serverProcess.HasExited)
                {
                    serverProcess.Kill();
                    serverProcess.WaitForExit(2000);
                    Debug.Log("Stopped local Node server started by Unity.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Failed to stop local Node server: " + exception.Message);
            }
            finally
            {
                serverProcess.Dispose();
                serverProcess = null;
                startedLocalServerFromUnity = false;
            }
        }

        private string ResolveNodeExecutable()
        {
            if (!string.IsNullOrWhiteSpace(fallbackNodeExecutable) && File.Exists(fallbackNodeExecutable))
            {
                return fallbackNodeExecutable;
            }

            return nodeExecutable;
        }

        private string GetWorkspaceRoot()
        {
            DirectoryInfo projectDirectory = Directory.GetParent(Application.dataPath);
            if (projectDirectory == null)
            {
                return Application.dataPath;
            }

            string projectRootServer = Path.Combine(projectDirectory.FullName, "server", "index.js");
            if (File.Exists(projectRootServer))
            {
                return projectDirectory.FullName;
            }

            DirectoryInfo workspaceDirectory = projectDirectory.Parent;
            if (workspaceDirectory != null)
            {
                string workspaceServer = Path.Combine(workspaceDirectory.FullName, "server", "index.js");
                if (File.Exists(workspaceServer))
                {
                    return workspaceDirectory.FullName;
                }
            }

            return projectDirectory.FullName;
        }

        private IEnumerator PollEvents()
        {
            while (true)
            {
                if (!string.IsNullOrEmpty(roomCode))
                {
                    string url = $"{serverBaseUrl}/unity/events?roomCode={roomCode}&after={lastEventId}";
                    using UnityWebRequest request = UnityWebRequest.Get(url);
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        ApplyEvents(request.downloadHandler.text);
                    }
                    else
                    {
                        SetBridgeStatus($"Polling failed: {request.error}");
                    }
                }

                yield return new WaitForSeconds(pollIntervalSeconds);
            }
        }

        private void ApplyEvents(string json)
        {
            UnityEventsResponse response = JsonUtility.FromJson<UnityEventsResponse>(json);
            if (response?.events == null || response.events.Length == 0)
            {
                return;
            }

            foreach (UnityShotEvent unityEvent in response.events)
            {
                lastEventId = Mathf.Max(lastEventId, unityEvent.id);
                if (gameManager == null)
                {
                    continue;
                }

                if (unityEvent.eventType == "sensor")
                {
                    ApplySensorEvent(unityEvent);
                    continue;
                }

                int playerIndex = unityEvent.playerId == "p2" ? 1 : 0;

                if (unityEvent.eventType == "calibrate")
                {
                    ApplyCalibrateEvent(playerIndex, unityEvent);
                    SetPlayerMotionStatus(playerIndex, "Neutral paddle calibrated", true);
                    continue;
                }

                bool isManualFallback = string.Equals(unityEvent.source, "manual", StringComparison.OrdinalIgnoreCase);
                bool isMotionSwing = string.Equals(unityEvent.source, "motion", StringComparison.OrdinalIgnoreCase);

                if (readinessCheckActive && (isMotionSwing || isManualFallback))
                {
                    MarkPlayerReady(playerIndex);
                    SetBridgeStatus($"Ready input from P{playerIndex + 1}");
                    continue;
                }
                if (!gameManager.IsPlaying)
                {
                    continue;
                }

                ShotType shotType = ParseShotType(unityEvent.shotType);
                ShotEvent shot = new ShotEvent(
                    playerIndex,
                    shotType,
                    unityEvent.power,
                    unityEvent.direction,
                    unityEvent.spin
                );

                if (isManualFallback)
                {
                    gameManager.ApplyFallbackShot(shot);
                    SetPlayerMotionStatus(playerIndex, $"Manual fallback detected: {shotType}", true);
                }
                else
                {
                    gameManager.ApplyShot(shot);
                    SetPlayerMotionStatus(playerIndex, $"Motion swing detected: {shotType}", true);
                }
            }
        }

        private void ApplySensorEvent(UnityShotEvent unityEvent)
        {
            if (gameManager == null)
            {
                return;
            }

            int playerIndex = unityEvent.playerId == "p2" ? 1 : 0;
            Vector3 acceleration = new Vector3(unityEvent.accelX, unityEvent.accelY, unityEvent.accelZ);
            Vector3 rotationRate = new Vector3(unityEvent.rotationAlpha, unityEvent.rotationBeta, unityEvent.rotationGamma);
            Vector3 orientation = new Vector3(unityEvent.orientationBeta, unityEvent.orientationAlpha, unityEvent.orientationGamma);

            Quaternion phoneQuaternion = Quaternion.identity;

            if (unityEvent.hasQuaternion)
            {
                phoneQuaternion = new Quaternion(
                    unityEvent.quaternionX,
                    unityEvent.quaternionY,
                    unityEvent.quaternionZ,
                    unityEvent.quaternionW
                );
            }

            PickleballRacquetController racquetController =
                playerIndex == 0 ? playerOneRacquetController : playerTwoRacquetController;

            if (gameManager != null && (gameManager.CurrentPhase == CourtSmasherzGameManager.GamePhase.Playing ||
                gameManager.CurrentPhase == CourtSmasherzGameManager.GamePhase.Finished) && racquetController != null)
            {
                racquetController.ApplyPhoneMotion(
                    acceleration,
                    rotationRate,
                    orientation,
                    unityEvent.hasQuaternion,
                    phoneQuaternion
                );
            }

            float accelerationMagnitude = acceleration.magnitude;
            float rotationMagnitude = rotationRate.magnitude;
            if (Time.time - lastShotTimes[playerIndex] < shotCooldownSeconds)
            {
                return;
            }

            if (accelerationMagnitude < accelerationShotThreshold && rotationMagnitude < rotationShotThreshold)
            {
                return;
            }

          ShotType shotType = acceleration.x >= 0f ? ShotType.Forehand : ShotType.Backhand;
            if (accelerationMagnitude >= smashAccelerationThreshold && acceleration.z < 0f )
            {
                shotType = ShotType.Smash;
            }
            else if (orientation.x > -5f && orientation.x < 70f  && acceleration.z > 5f)
            {
                shotType = ShotType.Lob;
            }

            if (racquetController != null)
            {
                racquetController.SetDetectedShotType(shotType);
            }

            float power = Mathf.Clamp01((accelerationMagnitude - 7f) / 22f + rotationMagnitude / 520f);
            float direction = Mathf.Clamp(orientation.z / 55f + acceleration.x / 28f, -1f, 1f);
            float spin = Mathf.Clamp(rotationRate.z / 360f, -1f, 1f);

            lastShotTimes[playerIndex] = Time.time;

            if (readinessCheckActive)
            {
                MarkPlayerReady(playerIndex);
                SetBridgeStatus($"Ready swing from P{playerIndex + 1}: {shotType}");
                return;
            }

            if (gameManager.IsPlaying)
            {
                gameManager.ApplyShot(new ShotEvent(playerIndex, shotType, Mathf.Max(0.25f, power), direction, spin));
                SetPlayerMotionStatus(playerIndex, $"Motion swing detected: {shotType}", true);
            }
        }

        private void ApplyCalibrateEvent(int playerIndex, UnityShotEvent unityEvent)
        {
            PickleballRacquetController racquetController =
                playerIndex == 0 ? playerOneRacquetController : playerTwoRacquetController;

            if (racquetController == null)
            {
                return;
            }

            Vector3 acceleration = new Vector3(unityEvent.accelX, unityEvent.accelY, unityEvent.accelZ);
            Vector3 rotationRate = new Vector3(unityEvent.rotationAlpha, unityEvent.rotationBeta, unityEvent.rotationGamma);
            Vector3 orientation = new Vector3(unityEvent.orientationBeta, unityEvent.orientationAlpha, unityEvent.orientationGamma);

            Quaternion phoneQuaternion = Quaternion.identity;
            if (unityEvent.hasQuaternion)
            {
                phoneQuaternion = new Quaternion(
                    unityEvent.quaternionX,
                    unityEvent.quaternionY,
                    unityEvent.quaternionZ,
                    unityEvent.quaternionW
                );
            }

            racquetController.SetNeutralFromPhoneMotion(
                acceleration,
                rotationRate,
                orientation,
                unityEvent.hasQuaternion,
                phoneQuaternion,
                unityEvent.screenFacingForward
            );
        }

        public void ResetRacquetNeutralRotations()
        {
            if (playerOneRacquetController != null)
            {
                playerOneRacquetController.ResetNeutralRotation();
            }

            if (playerTwoRacquetController != null)
            {
                playerTwoRacquetController.ResetNeutralRotation();
            }
        }

        private void MarkPlayerReady(int playerIndex)
        {
            if (playerIndex == 0)
            {
                p1Ready = true;
            }
            else
            {
                p2Ready = true;
            }

            if (mainMenu != null)
            {
                mainMenu.ShowWaitingForSwings(p1Ready, p2Ready);
            }

            if (p1Ready && p2Ready)
            {
                readinessCheckActive = false;
                if (mainMenu != null)
                {
                    mainMenu.HideMenu();
                }

                if (gameManager != null)
                {
                    gameManager.BeginMatch();
                }

                SetJoinHudVisible(false);
                SetPlayerMotionStatus(0, string.Empty, false);
                SetPlayerMotionStatus(1, string.Empty, false);
            }
        }

        private void SetJoinHudVisible(bool visible)
        {
            if (roomCodeText != null)
            {
                roomCodeText.gameObject.SetActive(visible);
            }

            if (bridgeStatusText != null)
            {
                bridgeStatusText.gameObject.SetActive(visible);
            }
        }

        private void SetPlayerMotionStatus(int playerIndex, string message, bool autoClear)
        {
            Text target = playerIndex == 0 ? playerOneMotionStatusText : playerTwoMotionStatusText;
            if (target == null)
            {
                return;
            }

            if (playerStatusClearRoutines[playerIndex] != null)
            {
                StopCoroutine(playerStatusClearRoutines[playerIndex]);
                playerStatusClearRoutines[playerIndex] = null;
            }

            target.text = string.IsNullOrEmpty(message) ? string.Empty : $"P{playerIndex + 1}: {message}";
            target.gameObject.SetActive(!string.IsNullOrEmpty(message));
            if (autoClear && !string.IsNullOrEmpty(message))
            {
                playerStatusClearRoutines[playerIndex] = StartCoroutine(ClearPlayerMotionStatusAfterDelay(playerIndex, 1.6f));
            }
        }

        private IEnumerator ClearPlayerMotionStatusAfterDelay(int playerIndex, float delay)
        {
            yield return new WaitForSeconds(delay);
            Text target = playerIndex == 0 ? playerOneMotionStatusText : playerTwoMotionStatusText;
            if (target != null)
            {
                target.text = string.Empty;
                target.gameObject.SetActive(false);
            }

            playerStatusClearRoutines[playerIndex] = null;
        }

        private ShotType ParseShotType(string shotType)
        {
            switch ((shotType ?? string.Empty).ToLowerInvariant())
            {
                case "backhand":
                    return ShotType.Backhand;
                case "lob":
                    return ShotType.Lob;
                case "smash":
                    return ShotType.Smash;
                default:
                    return ShotType.Forehand;
            }
        }

        private void SetBridgeStatus(string message)
        {
            if (bridgeStatusText != null)
            {
                bridgeStatusText.text = message;
            }
        }

        private string GetLocalIPv4Address()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                NetworkInterfaceType type = networkInterface.NetworkInterfaceType;
                if (type != NetworkInterfaceType.Wireless80211 && type != NetworkInterfaceType.Ethernet)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation address in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                    {
                        return address.Address.ToString();
                    }
                }
            }

            return "127.0.0.1";
        }

        [Serializable]
        private class CreateRoomResponse
        {
            public string roomCode;
            public string phoneBaseUrl;
            public string phoneJoinUrl;
        }

        [Serializable]
        private class UnityEventsResponse
        {
            public UnityShotEvent[] events;
        }

        [Serializable]
        private class UnityShotEvent
        {
            public int id;
            public string eventType;
            public string playerId;
            public string shotType;
            public string source;
            public bool screenFacingForward;
            public float power;
            public float direction;
            public float spin;
            public long timestamp;
            public float accelX;
            public float accelY;
            public float accelZ;
            public float rotationAlpha;
            public float rotationBeta;
            public float rotationGamma;
            public float orientationAlpha;
            public float orientationBeta;
            public float orientationGamma;
            public bool hasQuaternion;
            public float quaternionX;
            public float quaternionY;
            public float quaternionZ;
            public float quaternionW;
        }
    }
}
