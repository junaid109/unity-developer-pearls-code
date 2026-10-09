using UnityEngine;

namespace Pearls.RacingVR
{
    /// <summary>
    /// Single-owner transform for a grabbable world-space panel. While
    /// held, this component is the ONLY thing allowed to write the
    /// panel's position/rotation -- no physics, no other script. That
    /// exclusivity removes conflicting writes. It does NOT remove tracking
    /// noise, which is why the pose is read late, in LateUpdate.
    /// </summary>
    public class GrabbableVideoPanel : MonoBehaviour
    {
        [Tooltip("Units per second the panel moves toward/away on thumbstick push")]
        public float DepthAdjustSpeed = 0.5f;
        [Tooltip("Degrees per second the panel yaws on thumbstick twist")]
        public float YawAdjustSpeed = 60f;
        [Tooltip("Thumbstick values below this magnitude are ignored (stick drift)")]
        [Range(0f, 0.5f)]
        public float Deadzone = 0.2f;

        private Transform _controller;
        private Vector3 _localOffset;
        private float _yawOffset;
        private bool _isHeld;

        public void BeginGrab(Transform controller)
        {
            _controller = controller;
            // Capture the offset at the moment of grab so the panel doesn't
            // snap to the controller's origin.
            _localOffset = controller.InverseTransformPoint(transform.position);
            _yawOffset = 0f;
            _isHeld = true;
        }

        public void EndGrab() => _isHeld = false;

        // LateUpdate runs after all Update calls, so the controller pose has
        // been refreshed for this frame. If you still see the panel lag the
        // hand on your target device, move this body into a method
        // subscribed to Application.onBeforeRender.
        private void LateUpdate()
        {
            if (!_isHeld) return;

            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
            float depthInput = ApplyDeadzone(stick.y);
            float yawInput = ApplyDeadzone(stick.x);

            _localOffset += Vector3.forward * depthInput * DepthAdjustSpeed * Time.deltaTime;
            _yawOffset += yawInput * YawAdjustSpeed * Time.deltaTime;

            // Sole write to this transform this frame -- no other system
            // is allowed to touch position/rotation while _isHeld is true.
            transform.position = _controller.TransformPoint(_localOffset);
            transform.rotation = _controller.rotation * Quaternion.Euler(0f, _yawOffset, 0f);
        }

        // Ignore small values, then rescale so output still spans 0..1.
        private float ApplyDeadzone(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude < Deadzone) return 0f;
            return Mathf.Sign(value) * ((magnitude - Deadzone) / (1f - Deadzone));
        }
    }
}