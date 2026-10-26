using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.XREAL;

namespace XrealLabel
{
    /// <summary>
    /// システム側のリセンター（眼鏡のボタンなど）を検知する。事前に確認ダイアログは挟めないので、検知したら通知する。
    /// 3つの手がかりを併用する（どれが実機で働くかは要実機確認）：
    ///  1. 眼鏡イベント ACTION_TYPE_TRIGGER_RECENTER（SDKでは internal の OnGlassesEventExt に流れるのでリフレクションで購読）
    ///  2. XRInputSubsystem.trackingOriginUpdated
    ///  3. 1フレームで頭の yaw が人間には不可能な量だけ跳んだ
    /// </summary>
    public class RecenterDetector : MonoBehaviour
    {
        [Tooltip("1フレームでこれ以上 yaw が変わったらリセンターとみなす（度）")]
        [SerializeField] float m_YawJumpThreshold = 45f;
        [Tooltip("この秒数より長いフレーム（処理落ち）は yaw の跳びを判定しない")]
        [SerializeField] float m_MaxFrameTime = 0.1f;

        public event Action<string> RecenterDetected;

        Transform m_Head;
        float m_PrevYaw;
        bool m_HasPrev;
        float m_SuppressUntil;
        readonly List<XRInputSubsystem> m_Subsystems = new List<XRInputSubsystem>();
        Delegate m_GlassesEventDelegate;
        EventInfo m_GlassesEvent;

        public void Init(Transform head) => m_Head = head;

        /// <summary>アプリ自身がリセンターするときに、その間の検知を止める。</summary>
        public void Suppress(float seconds)
        {
            m_SuppressUntil = Time.unscaledTime + seconds;
            m_HasPrev = false;
        }

        [Tooltip("起動直後のこの秒数は検知しない（XR初期化時に SDK 自身がリセンターするため）")]
        [SerializeField] float m_StartupGrace = 3f;

        void OnEnable()
        {
            HookGlassesEvent();
            Suppress(m_StartupGrace);
        }

        void OnDisable()
        {
            foreach (var s in m_Subsystems)
                s.trackingOriginUpdated -= OnTrackingOriginUpdated;
            m_Subsystems.Clear();
            if (m_GlassesEvent != null && m_GlassesEventDelegate != null)
                m_GlassesEvent.GetRemoveMethod(true)?.Invoke(null, new object[] { m_GlassesEventDelegate });
        }

        void HookGlassesEvent()
        {
            try
            {
                m_GlassesEvent = typeof(XREALCallbackHandler).GetEvent("OnGlassesEventExt", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (m_GlassesEvent == null)
                {
                    Debug.LogWarning("[LabelApp] OnGlassesEventExt が見つからないため、眼鏡のリセンターイベントは使えません");
                    return;
                }
                XREALGlassesEventCallback cb = OnGlassesEvent;
                m_GlassesEventDelegate = cb;
                m_GlassesEvent.GetAddMethod(true).Invoke(null, new object[] { m_GlassesEventDelegate });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LabelApp] 眼鏡イベントの購読に失敗: {e.Message}");
                m_GlassesEvent = null;
            }
        }

        void OnGlassesEvent(GlassesEventData data)
        {
            // 温度通知は数秒おきに来るので記録しない
            if (data.actionType != XREALActionType.ACTION_TYPE_TEMPERATURE_DATA)
                Debug.Log($"[LabelApp] GlassesEvent {data.actionType} para={data.para} para2={data.para2}");
            if (data.actionType == XREALActionType.ACTION_TYPE_TRIGGER_RECENTER)
                Raise("glasses_event_TRIGGER_RECENTER");
        }

        void OnTrackingOriginUpdated(XRInputSubsystem subsystem) => Raise("trackingOriginUpdated");

        void Update()
        {
            // XRInputSubsystem はXR初期化後に現れるので、見つかり次第購読する
            if (m_Subsystems.Count == 0)
            {
                SubsystemManager.GetSubsystems(m_Subsystems);
                foreach (var s in m_Subsystems)
                    s.trackingOriginUpdated += OnTrackingOriginUpdated;
            }

            if (m_Head == null) return;
            float yaw = HeadMath.Yaw(m_Head.forward);
            // 真上・真下を向いていると yaw が不安定なので判定しない
            bool yawReliable = Mathf.Abs(HeadMath.Pitch(m_Head.forward)) < 70f;
            if (m_HasPrev && yawReliable && Time.unscaledDeltaTime < m_MaxFrameTime)
            {
                float delta = Mathf.Abs(HeadMath.NormalizeAngle(yaw - m_PrevYaw));
                if (delta > m_YawJumpThreshold)
                    Raise($"yaw_jump_{delta:F1}deg");
            }
            m_PrevYaw = yaw;
            m_HasPrev = yawReliable;
        }

        void Raise(string reason)
        {
            if (Time.unscaledTime < m_SuppressUntil) return;
            m_HasPrev = false;
            RecenterDetected?.Invoke(reason);
        }

        /// <summary>
        /// アプリからのリセンター。XRInputSubsystem.TryRecenter() を試し、対応していなければ
        /// XR Origin のカメラオフセットを回して「今向いている方向を yaw=0」にする。
        /// </summary>
        public static bool TryRecenter(Transform cameraOffset, Transform head)
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            bool ok = false;
            foreach (var s in subsystems)
            {
                if (s.running && s.TryRecenter())
                    ok = true;
            }
            if (!ok && cameraOffset != null && head != null)
            {
                float yaw = HeadMath.Yaw(head.forward);
                cameraOffset.rotation = Quaternion.Euler(0, -yaw, 0) * cameraOffset.rotation;
            }
            return ok;
        }
    }
}
