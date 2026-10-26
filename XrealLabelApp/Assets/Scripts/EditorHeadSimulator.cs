using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace XrealLabel
{
    /// <summary>
    /// エディタ（XRデバイスなし）で頭の回転を模擬する。
    /// ・右ドラッグ / 矢印キー：頭を回す
    /// ・R キー：システム側のリセンターを模擬（ワールドを 60° 回す）
    /// ・D キー：yaw ドリフトを模擬（0.5° ずつずらす）
    /// XRデバイスが動いているとき（実機）は何もしない。
    /// </summary>
    public class EditorHeadSimulator : MonoBehaviour
    {
        [SerializeField] float m_MouseSensitivity = 0.15f;
        [SerializeField] float m_KeySpeed = 60f;

        Transform m_Offset;
        float m_Yaw;
        float m_Pitch;

        public void Init(Transform cameraOffset) => m_Offset = cameraOffset;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (m_Offset == null || UnityEngine.XR.XRSettings.isDeviceActive) return;

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var d = mouse.delta.ReadValue();
                m_Yaw += d.x * m_MouseSensitivity;
                m_Pitch -= d.y * m_MouseSensitivity;
            }
            var kb = Keyboard.current;
            if (kb != null && !IsTyping())
            {
                float dt = Time.deltaTime * m_KeySpeed;
                if (kb.leftArrowKey.isPressed) m_Yaw -= dt;
                if (kb.rightArrowKey.isPressed) m_Yaw += dt;
                if (kb.upArrowKey.isPressed) m_Pitch -= dt;
                if (kb.downArrowKey.isPressed) m_Pitch += dt;
                if (kb.rKey.wasPressedThisFrame) m_Yaw += 60f;  // システムリセンターの模擬（yaw が跳ぶ）
                if (kb.dKey.wasPressedThisFrame) m_Yaw += 0.5f; // ドリフトの模擬
            }
            m_Pitch = Mathf.Clamp(m_Pitch, -80f, 80f);
            m_Offset.localRotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
#endif
        }

        /// <summary>頭の向きを直接指定する（テスト用）。</summary>
        public void SetHead(float yaw, float pitch)
        {
            m_Yaw = yaw;
            m_Pitch = pitch;
            if (m_Offset != null) m_Offset.localRotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
        }

        /// <summary>アプリからのリセンター後、模擬の回転をカメラオフセットの値に合わせる。</summary>
        public void SyncFromOffset()
        {
            if (m_Offset == null) return;
            var e = m_Offset.localEulerAngles;
            m_Pitch = HeadMath.NormalizeAngle(e.x);
            m_Yaw = HeadMath.NormalizeAngle(e.y);
        }

        static bool IsTyping()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || es.currentSelectedGameObject == null) return false;
            var field = es.currentSelectedGameObject.GetComponent<UnityEngine.UI.InputField>();
            return field != null && field.isFocused;
        }
    }
}
