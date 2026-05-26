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
        public Text roomCodeText;
        public Text bridgeStatusText;

        [Header("Server")]
        public string serverBaseUrl = "http://localhost:3000";
        public float pollIntervalSeconds = 0.08f;
        public bool autoStartLocalServer = true;
        public string nodeExecutable = "node";
        public string fallbackNodeExecutable = @"C:\Users\kumar\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe";

        [Header("Unity Motion Classification")]
        public float accelerationShotThreshold = 13f;
        public float rotationShotThreshold = 120f;
        public float smashAccelerationThreshold = 24f;
        public float shotCooldownSeconds = 0.45f;

        private string roomCode;
        private string phoneBaseUrl;
        private int lastEventId;
        private readonly float[] lastShotTimes = { -10f, -10f };
        private Process serverProcess;

        private void Start()
        {
            StartCoroutine(EnsureServerThenCreateRoom());
        }

        private IEnumerator EnsureServerThenCreateRoom()
        {
            phoneBaseUrl = $"http://{GetLocalIPv4Address()}:3000";
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
            lastEventId = 0;
            if (roomCodeText != null)
            {
                roomCodeText.text = $"Room: {roomCode} | Phone: {phoneBaseUrl}/controller.html";
            }

            SetBridgeStatus($"Open phone URL and join {roomCode}");
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
                SetBridgeStatus("Starting local server...");
            }
            catch (Exception exception)
            {
                SetBridgeStatus($"Could not start server: {exception.Message}");
                Debug.LogException(exception);
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

                gameManager.ApplyShot(new ShotEvent(
                    unityEvent.playerId == "p2" ? 1 : 0,
                    ParseShotType(unityEvent.shotType),
                    unityEvent.power,
                    unityEvent.direction,
                    unityEvent.spin
                ));
                SetBridgeStatus($"Received {unityEvent.playerId} {unityEvent.shotType}");
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

            gameManager.ApplyPhoneMotion(playerIndex, acceleration, rotationRate, orientation);

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
            if (accelerationMagnitude >= smashAccelerationThreshold && acceleration.z < -6f)
            {
                shotType = ShotType.Smash;
            }
            else if (orientation.x < -18f && acceleration.z > 3f)
            {
                shotType = ShotType.Lob;
            }

            float power = Mathf.Clamp01((accelerationMagnitude - 7f) / 22f + rotationMagnitude / 520f);
            float direction = Mathf.Clamp(orientation.z / 55f + acceleration.x / 28f, -1f, 1f);
            float spin = Mathf.Clamp(rotationRate.z / 360f, -1f, 1f);

            gameManager.ApplyShot(new ShotEvent(playerIndex, shotType, Mathf.Max(0.25f, power), direction, spin));
            lastShotTimes[playerIndex] = Time.time;
            SetBridgeStatus($"Motion swing P{playerIndex + 1}: {shotType}");
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
        }
    }
}
