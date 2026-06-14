using UnityEngine;
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
                Debug.Log("QR phone URL: " + phoneUrl);
                qrImage.texture = ZxingQrCodeGenerator.Generate(phoneUrl, 512);
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
                readyStatusText.text = "scan the qr code to join the room.\nPress Start once you are ready!";
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
                readyStatusText.text = $"P1 {p1Status}  |  P2 {p2Status}\nWaiting for P1 and P2 to swing once.";
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
    }
}
