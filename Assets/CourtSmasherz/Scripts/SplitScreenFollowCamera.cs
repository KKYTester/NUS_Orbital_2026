using UnityEngine;

namespace CourtSmasherz
{
    public class SplitScreenFollowCamera : MonoBehaviour
    {
        public Transform followTarget;
        public Transform lookTarget;
        public Vector3 localOffset = new Vector3(0f, 3.2f, -5.2f);
        public float followSmoothing = 8f;
        public float lookSmoothing = 10f;

        private Quaternion targetRotation;

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            Vector3 desiredPosition = followTarget.TransformPoint(localOffset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * followSmoothing);

            Vector3 lookPosition = lookTarget != null
                ? lookTarget.position
                : followTarget.position + followTarget.forward * 6f;
            lookPosition.y = Mathf.Max(lookPosition.y, 0.75f);

            Vector3 lookDirection = lookPosition - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * lookSmoothing);
            }
        }
    }
}
