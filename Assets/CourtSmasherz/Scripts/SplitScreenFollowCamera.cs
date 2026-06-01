using UnityEngine;

namespace CourtSmasherz
{
    public class SplitScreenFollowCamera : MonoBehaviour
    {
        public Transform followTarget;
        public Vector3 localOffset = new Vector3(0f, 3.2f, -5.2f);
        public Vector3 rotation = Vector3.zero;
        public float followSmoothing = 8f;

        private Quaternion targetRotation;

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }
            Vector3 desiredPosition = followTarget.TransformPoint(localOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothing);
            transform.localEulerAngles = rotation;
        }
    }
}
