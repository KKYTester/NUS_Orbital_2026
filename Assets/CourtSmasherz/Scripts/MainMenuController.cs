using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace CourtSmasherz
{
    public class MainMenuController : MonoBehaviour
    {
        public CanvasGroup menuGroup;
        public RawImage qrImage;
        public Text roomCodeText;
        public Text phoneUrlText;
        public Text readyStatusText;
        public Button startButton;
        public PhoneMotionHttpBridge bridge;
        private bool hasJoinInfo;
        private Coroutine qrLoadRoutine;

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(StartReadinessCheck);
            }
        }

        public void SetJoinInfo(string roomCode, string phoneUrl)
        {
            hasJoinInfo = !string.IsNullOrEmpty(roomCode) && !string.IsNullOrEmpty(phoneUrl);
            if (roomCodeText != null)
            {
                roomCodeText.text = string.IsNullOrEmpty(roomCode) ? "Room: creating..." : $"Room: {roomCode}";
            }

            if (phoneUrlText != null)
            {
                phoneUrlText.text = string.IsNullOrEmpty(phoneUrl) ? "Phone URL: starting server..." : phoneUrl;
            }

            if (qrImage != null && !string.IsNullOrEmpty(phoneUrl))
            {
                qrImage.texture = SimpleQrCodeGenerator.Generate(phoneUrl, 8);
                if (qrLoadRoutine != null)
                {
                    StopCoroutine(qrLoadRoutine);
                }

                qrLoadRoutine = StartCoroutine(LoadQrFromService(phoneUrl));
            }

            if (startButton != null)
            {
                startButton.interactable = hasJoinInfo;
            }
        }

        public void ShowMenu()
        {
            SetVisible(true);
            if (startButton != null)
            {
                startButton.gameObject.SetActive(true);
                startButton.interactable = hasJoinInfo;
            }

            if (readyStatusText != null)
            {
                readyStatusText.text = "Join both phones, then press Start.";
            }
        }

        public void ShowWaitingForSwings(bool p1Ready, bool p2Ready)
        {
            SetVisible(true);
            if (startButton != null)
            {
                startButton.gameObject.SetActive(false);
            }

            if (readyStatusText != null)
            {
                string p1Status = p1Ready ? "ready" : "waiting";
                string p2Status = p2Ready ? "ready" : "waiting";
                readyStatusText.text = $"P1 {p1Status}  |  P2 {p2Status}";
            }
        }

        public void HideMenu()
        {
            SetVisible(false);
        }

        private void StartReadinessCheck()
        {
            if (bridge != null)
            {
                bridge.StartReadinessCheck();
            }
        }

        private void SetVisible(bool visible)
        {
            if (menuGroup == null)
            {
                return;
            }

            menuGroup.alpha = visible ? 1f : 0f;
            menuGroup.interactable = visible;
            menuGroup.blocksRaycasts = visible;
        }

        private IEnumerator LoadQrFromService(string phoneUrl)
        {
            string qrUrl = "https://api.qrserver.com/v1/create-qr-code/?size=360x360&margin=16&data=" +
                UnityWebRequest.EscapeURL(phoneUrl);

            using UnityWebRequest request = UnityWebRequestTexture.GetTexture(qrUrl);
            request.timeout = 8;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && qrImage != null)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                texture.filterMode = FilterMode.Point;
                qrImage.texture = texture;
            }
        }
    }
}
