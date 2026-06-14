using System;
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
        private Quaternion initialLocalRotation;
        private Vector3 initialVector3;
        private bool calibratedWithScreenFacingForward;

        public Transform HitTransform => transform;

        /*
            Following is for the racket force appliers to use
            get; -> Others scripts can "get"/read the variable
            private set; -> Only this script can set the variable
        */
        public float phoneAccelerationMagnitude{ get; private set; }
        public Vector3 phoneAccelerationDirection{ get; private set;}
        public Vector2 phoneAccelerationPitchYaw{ get; private set;}

        private const float irl_g = 9.81f; // in ms^-2

        private void Awake()
        {
            initialLocalRotation = transform.localRotation;
            initialVector3 = initialLocalRotation * Vector3.forward;
        }

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

            Quaternion rawPhoneRotation = CreateRawPhoneRotation(
                orientation,
                hasQuaternion,
                phoneQuaternion,
                calibratedWithScreenFacingForward
            );

            if (!hasPhoneNeutralRotation)
            {
                SetNeutralRotation(rawPhoneRotation, transform.localRotation);
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

            // For ball controller script (PickleballBallController.cs) to use:
            phoneAccelerationMagnitude = phoneAccelerationMagnitudeCaculator(acceleration.z, rawPhoneRotation);
            phoneAccelerationDirection = transform.rotation * Vector3.forward;
            phoneAccelerationPitchYaw = phonePitchYawCalculator(phoneAccelerationDirection);
        }

        public void SetNeutralFromPhoneMotion(
            Vector3 acceleration,
            Vector3 rotationRate,
            Vector3 orientation,
            bool hasQuaternion,
            Quaternion phoneQuaternion,
            bool screenFacingForward = false
        )
        {
            calibratedWithScreenFacingForward = screenFacingForward;
            Quaternion rawPhoneRotation = CreateRawPhoneRotation(
                orientation,
                hasQuaternion,
                phoneQuaternion,
                calibratedWithScreenFacingForward
            );
            SetNeutralRotation(rawPhoneRotation, initialLocalRotation);
        }

        public void ResetNeutralRotation()
        {
            hasPhoneNeutralRotation = false;
            transform.localRotation = initialLocalRotation;
        }

        private Quaternion CreateRawPhoneRotation(
            Vector3 orientation,
            bool hasQuaternion,
            Quaternion phoneQuaternion,
            bool screenFacingForward
        )
        {
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

            if (screenFacingForward)
            {
                rawPhoneRotation *= Quaternion.Euler(0f, 180f, 0f);
            }

            return rawPhoneRotation;
        }

        private void SetNeutralRotation(Quaternion rawPhoneRotation, Quaternion neutralRacquetRotation)
        {
            phoneNeutralRotation = rawPhoneRotation;
            racquetNeutralRotation = neutralRacquetRotation;
            hasPhoneNeutralRotation = true;
            transform.localRotation = neutralRacquetRotation;
        }

        // Calculation of phone acceleration with gravity compensation
        private float phoneAccelerationMagnitudeCaculator(float rawAccelerationMagnitude, Quaternion rawPhoneRotation)
        {
            // Vector of phone orientation with respect to real world frame
            // irl east = +ve x-axis; irl north = +ve y-axis; irl down = +ve z-axis
            // Back of phone is where this vector is pointing
            Vector3 phoneVector = rawPhoneRotation * Vector3.forward;

            // Following's theta is the angle between phoneVector and z-axis
            float cos_theta = Vector3.Dot(phoneVector, Vector3.forward)
            / phoneVector.magnitude; /*magnitude of Vector3.foward omitted because its just 1*/
            // Magnitude of the component of accelerationVector that is affected by gravity
            float gComponent = irl_g * cos_theta;

            float gravityCompensatedAccelerationMagnitude = rawAccelerationMagnitude - gComponent;
            return gravityCompensatedAccelerationMagnitude;
        }

        private Vector2 phonePitchYawCalculator(Vector3 currDirection)
        {
            Vector3 xzProjection = new Vector3(currDirection.x, 0, currDirection.z);
            Vector2 pitchYaw = new Vector2 (Vector3.Angle(currDirection, xzProjection), Vector3.SignedAngle(xzProjection, initialVector3, Vector3.up)); 
            return pitchYaw;
        }        
    }
}
