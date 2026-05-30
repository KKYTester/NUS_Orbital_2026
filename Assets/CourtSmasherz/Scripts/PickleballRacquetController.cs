using UnityEngine;

namespace CourtSmasherz
{
    public class PickleballRacquetController : MonoBehaviour
    {
        [Header("Phone Racquet Rotation Mapping")]
        public bool usePhoneRacquetRotation = true;

        public bool invertPhonePitch = true;
        public bool invertPhoneYaw = true;
        public bool invertPhoneRoll = false;

        public Vector3 phoneRotationOffsetEuler = Vector3.zero;

        [Range(0f, 1f)]
        public float phoneRotationSmoothing = 0.25f;

        [Header("Quaternion Mapping")]
        public bool useQuaternionOrientation = true;

        private Quaternion phoneNeutralRotation;
        private Quaternion racquetNeutralRotation;
        private bool hasPhoneNeutralRotation;

        public Transform HitTransform => transform;

        public void ApplyPhoneMotion(
            Vector3 acceleration,
            Vector3 rotationRate,
            Vector3 orientation,
            bool hasQuaternion,
            Quaternion phoneQuaternion
        )
        {
            if (!usePhoneRacquetRotation)
            {
                return;
            }

            Quaternion rawPhoneRotation;

            if (hasQuaternion && useQuaternionOrientation)
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

            if (!hasPhoneNeutralRotation)
            {
                phoneNeutralRotation = rawPhoneRotation;
                racquetNeutralRotation = transform.localRotation;
                hasPhoneNeutralRotation = true;
            }

            Quaternion relativePhoneRotation =
                Quaternion.Inverse(phoneNeutralRotation) * rawPhoneRotation;

            Quaternion offsetRotation = Quaternion.Euler(phoneRotationOffsetEuler);

            Quaternion targetRotation =
                racquetNeutralRotation * offsetRotation * relativePhoneRotation;

            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetRotation,
                phoneRotationSmoothing
            );
        }

        public void ResetNeutralRotation()
        {
            hasPhoneNeutralRotation = false;
        }
    }
}