using System;
using System.Collections.Generic;
using UnityEngine;

namespace XrealLabel
{
    /// <summary>
    /// アプリ本体。「原点設定 → 頭を向けて確定 → Beam Pro で文字入力」の流れでラベルを置く（仕様書 F-01〜F-09）。
    /// ラベルは「原点θ0からの相対yaw」と「pitch」で保持し、描画位置は 原点の向き × 相対角 × 固定距離 で求める。
    /// </summary>
    public class LabelApp : MonoBehaviour
    {
        enum State
        {
            /// <summary>起動時の安全表示</summary>
            SafetyNotice,
            /// <summary>原点未設定（F-01）。ラベルは置けない</summary>
            OriginUnset,
            /// <summary>通常（配置・ホバー・編集・削除）</summary>
            Ready,
            /// <summary>Beam Pro で文字入力中（F-02 配置 / F-04 編集）</summary>
            AwaitingText,
        }

        [Header("表示（暫定値）")]
        [SerializeField] float m_LabelDistance = 2.0f;
        [SerializeField] int m_MaxLabels = 20;
        [Tooltip("視線とラベルの角度差がこれ以内で、かつ画角外なら矢印を出す（度）")]
        [SerializeField] float m_ArrowThreshold = 40f;
        [SerializeField] LabelTextStyle m_TextStyle = LabelTextStyle.BlackKnockout;

        [Header("文字入力の候補（ソフトキーボードの代替）")]
        [SerializeField] string[] m_Presets = { "机", "ドア", "窓", "モニター", "棚", "時計", "椅子", "照明", "コンセント" };

        [Header("XR Origin（未設定ならシーンから探す）")]
        [SerializeField] Camera m_Camera;
        [SerializeField] Transform m_CameraOffset;

        Transform m_LabelRoot;
        GlassesHud m_Hud;
        PhoneUI m_Phone;
        InputRouter m_Input;
        RecenterDetector m_Recenter;
        EditorHeadSimulator m_Simulator;
        OperationLogger m_Log;

        readonly SessionInfo m_Session = new SessionInfo();
        readonly List<LabelData> m_Labels = new List<LabelData>();
        readonly Dictionary<int, LabelView> m_Views = new Dictionary<int, LabelView>();
        readonly List<Vector3> m_LabelPositions = new List<Vector3>();
        int m_NextId = 1;

        State m_State = State.SafetyNotice;
        /// <summary>ラベルを置く基準の頭の位置（原点設定時のカメラ位置）。3DoFなので実質固定。</summary>
        Vector3 m_Anchor;
        int? m_HoverId;

        // 文字入力中の対象
        LabelView m_PendingView;
        float m_PendingYawRel;
        float m_PendingPitch;
        int? m_EditingId;

        void Start()
        {
            if (m_Camera == null || m_CameraOffset == null)
            {
                var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
                if (origin != null)
                {
                    if (m_Camera == null) m_Camera = origin.Camera;
                    if (m_CameraOffset == null && origin.CameraFloorOffsetObject != null)
                        m_CameraOffset = origin.CameraFloorOffsetObject.transform;
                }
                if (m_Camera == null) m_Camera = Camera.main;
                if (m_CameraOffset == null) m_CameraOffset = m_Camera.transform.parent;
            }

            // Beam Pro の画面がスリープするとアプリごと止まるので、実行中は画面を消さない（連続使用30分の想定）
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            m_LabelRoot = new GameObject("Labels").transform;
            m_Hud = GlassesHud.Create(m_Camera);
            m_Phone = PhoneUI.Create(m_Presets);
            m_Input = gameObject.AddComponent<InputRouter>();
            m_Recenter = gameObject.AddComponent<RecenterDetector>();
            m_Recenter.Init(m_Camera.transform);
            m_Log = new OperationLogger();

#if UNITY_EDITOR
            m_Simulator = gameObject.AddComponent<EditorHeadSimulator>();
            m_Simulator.Init(m_CameraOffset);
#endif

            m_Input.Confirm += OnConfirm;
            m_Input.KeyPressed += OnKeyPressed;
            m_Recenter.RecenterDetected += OnSystemRecenterDetected;
            m_Phone.ConfirmPressed += m_Input.ConfirmFromPhoneButton;
            m_Phone.EditPressed += OnEditPressed;
            m_Phone.DeletePressed += OnDeletePressed;
            m_Phone.ClearAllPressed += OnClearAllPressed;
            m_Phone.RecenterPressed += OnRecenterPressed;
            m_Phone.TextSubmitted += OnTextSubmitted;
            m_Phone.TextCancelled += OnTextCancelled;
            m_Phone.StyleTogglePressed += OnStyleToggle;

            m_Log.SaveSession(m_Session);
            m_Phone.SetHover(null);
            EnterState(State.SafetyNotice);
            ApplyStyleLabel();
        }

        void OnDestroy()
        {
            m_Log?.Dispose();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) m_Log?.SaveSession(m_Session);
        }

        // ================= 状態 =================

        void EnterState(State state)
        {
            m_State = state;
            switch (state)
            {
                case State.SafetyNotice:
                    m_Hud.SetGuide($"安全のため、座って、または立ち止まって\n使ってください\n{ButtonNames.ConfirmGlassesButton}で開始");
                    m_Phone.SetStatus($"眼鏡の{ButtonNames.ConfirmGlassesButton}か、下の青いボタンで開始");
                    m_Phone.SetConfirmLabel("開始");
                    m_Phone.SetSafetyVisible(true);
                    break;
                case State.OriginUnset:
                    m_Hud.SetGuide($"基準にしたい方向（例：正面の机）を向いて\n{ButtonNames.ConfirmGlassesButton}を押してください");
                    m_Phone.SetStatus($"原点未設定：基準にしたい方向を向いて、{ButtonNames.ConfirmGlassesButton}か青いボタン");
                    m_Phone.SetConfirmLabel("原点を設定");
                    m_Phone.SetSafetyVisible(true);
                    break;
                case State.Ready:
                    m_Hud.SetGuide("");
                    m_Phone.SetStatus($"物体の方を向いて、{ButtonNames.ConfirmGlassesButton}か青いボタンでラベルを置く");
                    m_Phone.SetConfirmLabel("ここにラベルを置く");
                    m_Phone.SetSafetyVisible(false);
                    break;
                case State.AwaitingText:
                    m_Hud.SetGuide("Beam Pro で文字を入力してください");
                    m_Phone.SetStatus(m_EditingId.HasValue ? "ラベルを編集中" : "ラベルの文字を入力中");
                    break;
            }
            m_Phone.SetConfirmInteractable(state != State.AwaitingText);
            RefreshCounts();
        }

        void RefreshCounts()
        {
            m_Phone.SetCount(m_Labels.Count, m_MaxLabels);
            m_Phone.SetClearInteractable(m_Labels.Count > 0);
        }

        // ================= 毎フレーム =================

        void LateUpdate()
        {
            if (m_Camera == null) return;
#if UNITY_EDITOR
            FitEditorGlassesView();
#endif
            var head = m_Camera.transform.position;

            // ラベルの位置と向きを更新（原点の向き × 相対角 × 固定距離）
            m_LabelPositions.Clear();
            foreach (var label in m_Labels)
            {
                if (!m_Views.TryGetValue(label.id, out var view)) continue;
                view.Place(m_Anchor, LabelDirection(label.yawRel, label.pitch), label.distance);
                view.Face(head);
                m_LabelPositions.Add(view.transform.position);
            }
            if (m_PendingView != null)
                m_PendingView.Place(m_Anchor, LabelDirection(m_PendingYawRel, m_PendingPitch), m_LabelDistance);

            UpdateHover();
            m_Hud.UpdateArrows(m_LabelPositions, m_ArrowThreshold);
        }

#if UNITY_EDITOR
        /// <summary>
        /// エディタでは Game ビューの左側を眼鏡ビュー、右側を Beam Pro 画面として使う。
        /// 眼鏡ビューは XREAL One と同じ 16:9（縦画角 25.6° ≒ 対角 50°）に揃え、画角外の判定を実機に近づける。
        /// </summary>
        void FitEditorGlassesView()
        {
            if (m_Camera.targetTexture != null) return;
            float areaW = Screen.width * (1f - PhoneUI.EditorPanelFraction);
            float areaH = Screen.height;
            if (areaW <= 0 || areaH <= 0) return;
            float w = Mathf.Min(areaW, areaH * 16f / 9f);
            float h = w * 9f / 16f;
            m_Camera.rect = new Rect((areaW - w) * 0.5f / Screen.width, (areaH - h) * 0.5f / Screen.height, w / Screen.width, h / Screen.height);
        }
#endif

        Vector3 LabelDirection(float yawRel, float pitch) => HeadMath.Direction(m_Session.originYaw + yawRel, pitch);

        /// <summary>F-03：レティクル（視線の中心）がラベルに重なっているときハイライトする。</summary>
        void UpdateHover()
        {
            int? hover = null;
            if (m_State == State.Ready)
            {
                var ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
                float best = float.MaxValue;
                foreach (var kv in m_Views)
                {
                    if (kv.Value.Raycast(ray, out float d) && d < best)
                    {
                        best = d;
                        hover = kv.Key;
                    }
                }
            }
            else if (m_State == State.AwaitingText && m_EditingId.HasValue)
            {
                hover = m_EditingId; // 編集中のラベルは強調したままにする
            }

            if (hover == m_HoverId) return;
            m_HoverId = hover;
            foreach (var kv in m_Views)
                kv.Value.SetHover(kv.Key == hover);
            var data = hover.HasValue ? m_Labels.Find(l => l.id == hover.Value) : null;
            m_Phone.SetHover(m_State == State.Ready ? data?.text : null);
        }

        void GetHead(out float yaw, out float pitch, out float roll)
        {
            var t = m_Camera.transform;
            yaw = HeadMath.Yaw(t.forward);
            pitch = HeadMath.Pitch(t.forward);
            roll = HeadMath.Roll(t.rotation);
        }

        void Log(string evt, int? labelId = null, string detail = "")
        {
            GetHead(out float yaw, out float pitch, out float roll);
            m_Log.Log(evt, labelId, yaw, pitch, roll, detail);
        }

        // ================= 入力 =================

        void OnKeyPressed(string source, string displayName)
        {
            m_Phone.SetLastInput(displayName);
            Log(OperationLogger.KeyPress, null, source);
        }

        void OnConfirm(string source)
        {
            if (m_Phone.IsDialogOpen) return;
            switch (m_State)
            {
                case State.SafetyNotice:
                    EnterState(State.OriginUnset);
                    break;
                case State.OriginUnset:
                    SetOrigin();
                    break;
                case State.Ready:
                    BeginPlace(source);
                    break;
                case State.AwaitingText:
                    break; // 入力中は無視
            }
        }

        /// <summary>F-01：今の頭の yaw を原点θ0にする。</summary>
        void SetOrigin()
        {
            GetHead(out float yaw, out _, out _);
            m_Session.originSet = true;
            m_Session.originYaw = yaw;
            m_Session.originSetAt = DateTime.Now;
            m_Anchor = m_Camera.transform.position;
            Log(OperationLogger.OriginSet, null, $"originYaw={yaw:F3}");
            m_Log.SaveSession(m_Session);
            m_Hud.ShowToast("原点を設定しました", 2f);
            EnterState(State.Ready);
        }

        /// <summary>F-02：レティクルの方向・固定距離に位置を仮決めし、Beam Pro で文字入力を始める。</summary>
        void BeginPlace(string source)
        {
            if (m_Labels.Count >= m_MaxLabels)
            {
                // F-09：上限
                m_Phone.ShowToast($"ラベルは最大{m_MaxLabels}個です。削除してから置いてください");
                m_Hud.ShowToast($"ラベルは最大{m_MaxLabels}個です", 2f);
#if UNITY_ANDROID && !UNITY_EDITOR
                Handheld.Vibrate();
#endif
                return;
            }
            GetHead(out float yaw, out float pitch, out _);
            m_PendingYawRel = HeadMath.NormalizeAngle(yaw - m_Session.originYaw);
            m_PendingPitch = pitch;
            m_EditingId = null;

            m_PendingView = LabelView.Create(m_LabelRoot, 0);
            m_PendingView.SetStyle(m_TextStyle);
            m_PendingView.SetPending(true);
            m_PendingView.SetHover(true);
            m_PendingView.SetText("入力中…");
            m_PendingView.Place(m_Anchor, LabelDirection(m_PendingYawRel, m_PendingPitch), m_LabelDistance);

            EnterState(State.AwaitingText);
            // Beam Pro の画面から確定したときだけキーボードをすぐ開く（眼鏡の Xボタンからだと入力フォーカスが眼鏡側にある）
            m_Phone.OpenInput("ラベルの文字を入力", "", source.StartsWith("BeamPro"));
        }

        void OnTextSubmitted(string text)
        {
            if (m_State != State.AwaitingText) return;
            text = text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            if (m_EditingId.HasValue)
            {
                // F-04：編集
                var label = m_Labels.Find(l => l.id == m_EditingId.Value);
                if (label != null)
                {
                    label.text = text;
                    label.updatedAt = DateTime.Now;
                    m_Views[label.id].SetText(text);
                    Log(OperationLogger.Edit, label.id, text);
                }
            }
            else
            {
                // F-02：生成
                var now = DateTime.Now;
                var label = new LabelData
                {
                    id = m_NextId++,
                    text = text,
                    yawRel = m_PendingYawRel,
                    pitch = m_PendingPitch,
                    distance = m_LabelDistance,
                    createdAt = now,
                    updatedAt = now,
                };
                m_Labels.Add(label);
                var view = LabelView.Create(m_LabelRoot, label.id);
                view.SetStyle(m_TextStyle);
                view.SetText(text);
                m_Views.Add(label.id, view);
                Log(OperationLogger.Place, label.id, $"{text} yawRel={label.yawRel:F3} pitch={label.pitch:F3}");
            }
            EndTextInput();
        }

        void OnTextCancelled()
        {
            if (m_State != State.AwaitingText) return;
            EndTextInput();
        }

        void EndTextInput()
        {
            if (m_PendingView != null)
            {
                Destroy(m_PendingView.gameObject);
                m_PendingView = null;
            }
            m_EditingId = null;
            m_HoverId = null;
            foreach (var v in m_Views.Values) v.SetHover(false);
            m_Phone.CloseInput();
            EnterState(m_Session.originSet ? State.Ready : State.OriginUnset);
        }

        void OnEditPressed()
        {
            if (m_State != State.Ready || !m_HoverId.HasValue) return;
            var label = m_Labels.Find(l => l.id == m_HoverId.Value);
            if (label == null) return;
            m_EditingId = label.id;
            EnterState(State.AwaitingText);
            m_Phone.OpenInput("ラベルを編集", label.text, true);
        }

        /// <summary>F-05：ホバー中のラベルを削除する。</summary>
        void OnDeletePressed()
        {
            if (m_State != State.Ready || !m_HoverId.HasValue) return;
            int id = m_HoverId.Value;
            Log(OperationLogger.Delete, id, m_Labels.Find(l => l.id == id)?.text ?? "");
            RemoveLabel(id);
            m_HoverId = null;
            m_Phone.SetHover(null);
            RefreshCounts();
        }

        /// <summary>F-06：全消去（確認ダイアログあり）。</summary>
        void OnClearAllPressed()
        {
            if (m_Labels.Count == 0) return;
            m_Phone.ShowDialog("すべてのラベルを消去しますか？", () =>
            {
                Log(OperationLogger.ClearAll, null, $"count={m_Labels.Count}");
                CancelTextInputIfAny();
                RemoveAllLabels();
                RefreshCounts();
                m_Phone.ShowToast("すべてのラベルを消去しました");
            });
        }

        /// <summary>F-07：リセンター。確認のうえ全ラベルを削除し、原点を未設定に戻す。</summary>
        void OnRecenterPressed()
        {
            m_Phone.ShowDialog("ラベルがすべてリセットされますが、よろしいですか？", () =>
            {
                // ドリフト計測用：リセンター直前の headYaw と θ0 の差を detail に残す
                GetHead(out float yaw, out _, out _);
                string drift = m_Session.originSet ? $"originYaw={m_Session.originYaw:F3} diff={HeadMath.NormalizeAngle(yaw - m_Session.originYaw):F3}" : "origin_unset";
                Log(OperationLogger.Recenter, null, drift);

                m_Recenter.Suppress(1.0f);
                bool sdk = RecenterDetector.TryRecenter(m_CameraOffset, m_Camera.transform);
                if (m_Simulator != null) m_Simulator.SyncFromOffset();
                Debug.Log($"[LabelApp] リセンター実行（XRInputSubsystem.TryRecenter: {(sdk ? "成功" : "非対応→カメラオフセットで補正")}）");

                ResetSession();
                m_Phone.ShowToast("リセンターしました。原点を設定し直してください");
            });
        }

        /// <summary>システム側のリセンターを検知したとき：事前確認できないので、全ラベルを削除して通知する。</summary>
        void OnSystemRecenterDetected(string reason)
        {
            Log(OperationLogger.RecenterDetected, null, reason);
            bool hadSomething = m_Session.originSet || m_Labels.Count > 0;
            if (!hadSomething)
            {
                // 消すものがないときは画面（安全表示・原点設定）をそのままにして、回数だけ数える
                m_Session.recenterCount++;
                m_Log.SaveSession(m_Session);
                return;
            }
            m_Phone.CloseDialog();
            ResetSession();
            const string msg = "リセンターされたためラベルをリセットしました";
            m_Phone.ShowToast(msg, 4f);
            m_Hud.ShowToast(msg, 4f);
        }

        void ResetSession()
        {
            CancelTextInputIfAny();
            RemoveAllLabels();
            m_Session.originSet = false;
            m_Session.recenterCount++;
            m_Log.SaveSession(m_Session);
            EnterState(State.OriginUnset);
        }

        void CancelTextInputIfAny()
        {
            if (m_State == State.AwaitingText)
                EndTextInput();
        }

        void RemoveLabel(int id)
        {
            m_Labels.RemoveAll(l => l.id == id);
            if (m_Views.TryGetValue(id, out var view))
            {
                Destroy(view.gameObject);
                m_Views.Remove(id);
            }
        }

        void RemoveAllLabels()
        {
            foreach (var v in m_Views.Values) Destroy(v.gameObject);
            m_Views.Clear();
            m_Labels.Clear();
            m_HoverId = null;
            m_Phone.SetHover(null);
        }

        void OnStyleToggle()
        {
            m_TextStyle = m_TextStyle == LabelTextStyle.BlackKnockout ? LabelTextStyle.WhiteOutlined : LabelTextStyle.BlackKnockout;
            foreach (var v in m_Views.Values) v.SetStyle(m_TextStyle);
            if (m_PendingView != null) m_PendingView.SetStyle(m_TextStyle);
            ApplyStyleLabel();
        }

        void ApplyStyleLabel()
        {
            m_Phone.SetStyleLabel(m_TextStyle == LabelTextStyle.BlackKnockout
                ? "文字色：黒（抜き文字）  → タップで切替"
                : "文字色：白＋縁取り  → タップで切替");
        }
    }
}
