using System;
using UnityEngine;
using UnityEngine.UI;

namespace XrealLabel
{
    /// <summary>
    /// Beam Pro 画面のUI（Screen Space Overlay。XRの眼鏡には描画されず、端末の画面にだけ出る）。
    /// 1080×2340 の縦長レイアウトで作り、実機では画面全体、エディタでは Game ビューの右側に縮小して置く。
    /// SDK の XREALVirtualController より手前（sortingOrder 100）に描画する。
    /// </summary>
    public class PhoneUI : MonoBehaviour
    {
        const float k_W = 1080f;
        const float k_H = 2340f;
        /// <summary>エディタで Game ビューの右側に使う幅の割合（残りは眼鏡ビュー）。</summary>
        public const float EditorPanelFraction = 0.36f;

        static readonly Color k_Bg = new Color(0.10f, 0.11f, 0.13f, 1f);
        static readonly Color k_Card = new Color(0.17f, 0.18f, 0.21f, 1f);
        static readonly Color k_Primary = new Color(0.22f, 0.47f, 0.95f, 1f);
        static readonly Color k_Danger = new Color(0.85f, 0.27f, 0.27f, 1f);
        static readonly Color k_Neutral = new Color(0.30f, 0.32f, 0.37f, 1f);
        static readonly Color k_TextMain = new Color(0.95f, 0.95f, 0.97f, 1f);
        static readonly Color k_TextSub = new Color(0.70f, 0.72f, 0.78f, 1f);

        public event Action ConfirmPressed;
        public event Action EditPressed;
        public event Action DeletePressed;
        public event Action ClearAllPressed;
        public event Action RecenterPressed;
        public event Action<string> TextSubmitted;
        public event Action TextCancelled;
        public event Action StyleTogglePressed;

        RectTransform m_Phone;
        Text m_Status;
        Text m_Count;
        Text m_LastKey;
        Text m_Safety;
        Button m_ConfirmButton;
        Text m_ConfirmLabel;
        Text m_HoverText;
        Button m_EditButton;
        Button m_DeleteButton;
        Button m_ClearButton;
        Text m_StyleLabel;

        RectTransform m_InputPanel;
        Text m_InputTitle;
        InputField m_Input;

        RectTransform m_Dialog;
        Text m_DialogText;
        Action m_DialogOk;

        RectTransform m_ToastRoot;
        Text m_Toast;
        float m_ToastUntil;

        Vector2Int m_LastScreen;

        public bool IsDialogOpen => m_Dialog.gameObject.activeSelf;

        public static PhoneUI Create(string[] presets)
        {
            var go = new GameObject("PhoneUI");
            var ui = go.AddComponent<PhoneUI>();
            ui.Build(presets);
            return ui;
        }

        void Build(string[] presets)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;
            gameObject.AddComponent<GraphicRaycaster>();

            m_Phone = UiFactory.CreateRect("Phone", transform);
            m_Phone.sizeDelta = new Vector2(k_W, k_H);
            var bg = m_Phone.gameObject.AddComponent<Image>();
            bg.color = k_Bg;

            float y = 60f;
            // ---- ヘッダー ----
            var title = UiFactory.CreateText("Title", m_Phone, "3DoF ラベル配置", 52, k_TextMain, TextAnchor.MiddleLeft);
            UiFactory.PlaceTop(title.rectTransform, y, 70, 60);
            m_Count = UiFactory.CreateText("Count", m_Phone, "", 44, k_TextSub, TextAnchor.MiddleRight);
            UiFactory.PlaceTop(m_Count.rectTransform, y, 70, 60);
            y += 90;
            m_Status = UiFactory.CreateText("Status", m_Phone, "", 42, k_TextMain, TextAnchor.UpperLeft);
            UiFactory.PlaceTop(m_Status.rectTransform, y, 120, 60);
            y += 130;
            m_Safety = UiFactory.CreateText("Safety", m_Phone, "⚠ 座って、または立ち止まって使ってください", 40, new Color(1f, 0.82f, 0.35f), TextAnchor.MiddleLeft);
            UiFactory.PlaceTop(m_Safety.rectTransform, y, 60, 60);
            y += 90;

            // ---- 確定（原点設定・配置）----
            m_ConfirmButton = CreateButton("Confirm", m_Phone, "確定", k_Primary, 64, () => ConfirmPressed?.Invoke());
            UiFactory.PlaceTop((RectTransform)m_ConfirmButton.transform, y, 420, 60);
            m_ConfirmLabel = m_ConfirmButton.GetComponentInChildren<Text>();
            var confirmHint = UiFactory.CreateText("ConfirmHint", m_ConfirmButton.transform, "眼鏡のXボタンと同じ操作", 34, new Color(1, 1, 1, 0.75f));
            UiFactory.PlaceTop(confirmHint.rectTransform, 320, 70);
            y += 450;

            // ---- ホバー中のラベル（F-04 編集 / F-05 削除）----
            var hoverCard = UiFactory.CreateImage("HoverCard", m_Phone, UiFactory.RoundedFill, k_Card, sliced: true);
            UiFactory.PlaceTop(hoverCard.rectTransform, y, 330, 60);
            m_HoverText = UiFactory.CreateText("HoverText", hoverCard.rectTransform, "", 42, k_TextMain, TextAnchor.MiddleLeft);
            UiFactory.PlaceTop(m_HoverText.rectTransform, 30, 100, 40);
            m_EditButton = CreateButton("Edit", hoverCard.rectTransform, "編集", k_Neutral, 48, () => EditPressed?.Invoke());
            SetAnchors((RectTransform)m_EditButton.transform, 0f, 0.5f, 30, 170, 40, 15);
            m_DeleteButton = CreateButton("Delete", hoverCard.rectTransform, "削除", k_Danger, 48, () => DeletePressed?.Invoke());
            SetAnchors((RectTransform)m_DeleteButton.transform, 0.5f, 1f, 30, 170, 15, 40);
            y += 360;

            // ---- 全消去（F-06）/ リセンター（F-07）----
            m_ClearButton = CreateButton("ClearAll", m_Phone, "全消去", k_Neutral, 46, () => ClearAllPressed?.Invoke());
            SetAnchorsTop((RectTransform)m_ClearButton.transform, 0f, 0.5f, y, 170, 60, 15);
            var recenter = CreateButton("Recenter", m_Phone, "リセンター", k_Neutral, 46, () => RecenterPressed?.Invoke());
            SetAnchorsTop((RectTransform)recenter.transform, 0.5f, 1f, y, 170, 15, 60);
            y += 200;

            // ---- 文字色の切り替え（要実機確認：黒の抜き文字 / 白文字＋縁取り）----
            var style = CreateButton("Style", m_Phone, "", k_Card, 40, () => StyleTogglePressed?.Invoke());
            UiFactory.PlaceTop((RectTransform)style.transform, y, 130, 60);
            m_StyleLabel = style.GetComponentInChildren<Text>();
            y += 160;

            m_LastKey = UiFactory.CreateText("LastKey", m_Phone, "最後の入力: —", 34, k_TextSub, TextAnchor.MiddleLeft);
            UiFactory.PlaceTop(m_LastKey.rectTransform, y, 60, 60);

            BuildInputPanel(presets);
            BuildDialog();
            BuildToast();
            EnsureEventSystem();
        }

        void BuildInputPanel(string[] presets)
        {
            // 文字入力（F-02 配置 / F-04 編集）。上半分に出してソフトキーボードと重ならないようにする。
            m_InputPanel = UiFactory.CreateRect("InputPanel", m_Phone);
            UiFactory.Stretch(m_InputPanel);
            var dim = m_InputPanel.gameObject.AddComponent<Image>();
            dim.color = new Color(0.06f, 0.07f, 0.08f, 0.97f);

            m_InputTitle = UiFactory.CreateText("InputTitle", m_InputPanel, "ラベルの文字を入力", 52, k_TextMain, TextAnchor.MiddleLeft);
            UiFactory.PlaceTop(m_InputTitle.rectTransform, 80, 80, 60);

            var fieldBg = UiFactory.CreateImage("Field", m_InputPanel, UiFactory.RoundedFill, Color.white, sliced: true);
            fieldBg.raycastTarget = true;
            UiFactory.PlaceTop(fieldBg.rectTransform, 190, 170, 60);
            var fieldText = UiFactory.CreateText("Text", fieldBg.rectTransform, "", 56, Color.black, TextAnchor.MiddleLeft);
            fieldText.supportRichText = false;
            UiFactory.Stretch(fieldText.rectTransform, 30);
            var placeholder = UiFactory.CreateText("Placeholder", fieldBg.rectTransform, "ここをタップして入力（例：机）", 48, new Color(0, 0, 0, 0.35f), TextAnchor.MiddleLeft);
            UiFactory.Stretch(placeholder.rectTransform, 30);
            m_Input = fieldBg.gameObject.AddComponent<InputField>();
            m_Input.targetGraphic = fieldBg;
            m_Input.textComponent = fieldText;
            m_Input.placeholder = placeholder;
            m_Input.lineType = InputField.LineType.SingleLine;
            m_Input.characterLimit = 40;
            m_Input.onEndEdit.AddListener(OnEndEdit);
            fieldBg.gameObject.AddComponent<KeyboardReopener>();

            var ok = CreateButton("OK", m_InputPanel, "OK", k_Primary, 52, SubmitInput);
            SetAnchorsTop((RectTransform)ok.transform, 0.5f, 1f, 400, 170, 15, 60);
            var cancel = CreateButton("Cancel", m_InputPanel, "キャンセル", k_Neutral, 48, () => TextCancelled?.Invoke());
            SetAnchorsTop((RectTransform)cancel.transform, 0f, 0.5f, 400, 170, 60, 15);

            // プリセット候補（ソフトキーボードが使えない場合の代替案）。タップでそのまま確定する。
            var presetTitle = UiFactory.CreateText("PresetTitle", m_InputPanel, "候補から選ぶ", 40, k_TextSub, TextAnchor.MiddleLeft);
            UiFactory.PlaceTop(presetTitle.rectTransform, 620, 60, 60);
            const int columns = 3;
            const float cellH = 150f, gap = 24f, top = 700f;
            for (int i = 0; i < presets.Length; i++)
            {
                string preset = presets[i];
                var b = CreateButton($"Preset_{i}", m_InputPanel, preset, k_Card, 44, () => TextSubmitted?.Invoke(preset));
                var rt = (RectTransform)b.transform;
                int col = i % columns, row = i / columns;
                float x0 = col / (float)columns, x1 = (col + 1) / (float)columns;
                SetAnchorsTop(rt, x0, x1, top + row * (cellH + gap), cellH, col == 0 ? 60 : gap / 2, col == columns - 1 ? 60 : gap / 2);
            }
            m_InputPanel.gameObject.SetActive(false);
        }

        void BuildDialog()
        {
            m_Dialog = UiFactory.CreateRect("Dialog", m_Phone);
            UiFactory.Stretch(m_Dialog);
            var dim = m_Dialog.gameObject.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.7f);

            var card = UiFactory.CreateImage("Card", m_Dialog, UiFactory.RoundedFill, k_Card, sliced: true);
            card.rectTransform.sizeDelta = new Vector2(940, 560);
            m_DialogText = UiFactory.CreateText("Message", card.rectTransform, "", 48, k_TextMain);
            UiFactory.PlaceTop(m_DialogText.rectTransform, 50, 260, 50);
            var cancel = CreateButton("Cancel", card.rectTransform, "キャンセル", k_Neutral, 46, CloseDialog);
            SetAnchorsTop((RectTransform)cancel.transform, 0f, 0.5f, 360, 150, 50, 15);
            var ok = CreateButton("OK", card.rectTransform, "OK", k_Danger, 46, () =>
            {
                var action = m_DialogOk;
                CloseDialog();
                action?.Invoke();
            });
            SetAnchorsTop((RectTransform)ok.transform, 0.5f, 1f, 360, 150, 15, 50);
            m_Dialog.gameObject.SetActive(false);
        }

        void BuildToast()
        {
            var bg = UiFactory.CreateImage("Toast", m_Phone, UiFactory.RoundedFill, new Color(0.98f, 0.98f, 1f, 0.97f), sliced: true);
            m_ToastRoot = bg.rectTransform;
            m_ToastRoot.anchorMin = m_ToastRoot.anchorMax = new Vector2(0.5f, 0f);
            m_ToastRoot.pivot = new Vector2(0.5f, 0f);
            m_ToastRoot.anchoredPosition = new Vector2(0, 120);
            m_ToastRoot.sizeDelta = new Vector2(960, 180);
            m_Toast = UiFactory.CreateText("Text", m_ToastRoot, "", 44, Color.black);
            UiFactory.Stretch(m_Toast.rectTransform, 24);
            m_ToastRoot.gameObject.SetActive(false);
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null ||
                FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }

        // ---- 外部から呼ぶ表示更新 ----

        public void SetStatus(string text) => m_Status.text = text;
        public void SetCount(int count, int max) => m_Count.text = $"ラベル {count}/{max}";
        public void SetLastInput(string text) => m_LastKey.text = $"最後の入力: {text}";
        public void SetSafetyVisible(bool visible) => m_Safety.gameObject.SetActive(visible);
        public void SetConfirmLabel(string text) => m_ConfirmLabel.text = text;
        public void SetConfirmInteractable(bool on) => SetEnabled(m_ConfirmButton, on);
        public void SetStyleLabel(string text) => m_StyleLabel.text = text;
        public void SetClearInteractable(bool on) => SetEnabled(m_ClearButton, on);

        /// <summary>ホバー中のラベル名を表示し、編集・削除ボタンの有効/無効を切り替える。null ならホバーなし。</summary>
        public void SetHover(string labelText)
        {
            bool has = labelText != null;
            m_HoverText.text = has ? $"ホバー中：「{labelText}」" : "ホバー中のラベル：なし";
            SetEnabled(m_EditButton, has);
            SetEnabled(m_DeleteButton, has);
        }

        public bool IsInputOpen => m_InputPanel.gameObject.activeSelf;

        /// <param name="focusKeyboard">
        /// true ならすぐ入力欄を有効にしてソフトキーボードを開く。Beam Pro の画面のボタンから開いたときだけ true にする。
        /// 眼鏡の Xボタンから開いたときは入力フォーカスが眼鏡側にあり、キーボードの表示が失敗するため、タップを待つ。
        /// </param>
        public void OpenInput(string title, string initialText, bool focusKeyboard)
        {
            m_InputTitle.text = title;
            m_Input.text = initialText ?? "";
            GuardedButton.BlockClicksBriefly();
            m_InputPanel.gameObject.SetActive(true);
            m_InputPanel.SetAsLastSibling();
            m_Dialog.SetAsLastSibling();
            m_ToastRoot.SetAsLastSibling();
            // フォーカスするとAndroidではソフトキーボードが開く
            if (focusKeyboard)
            {
                m_Input.Select();
                m_Input.ActivateInputField();
            }
        }

        public void CloseInput()
        {
            GuardedButton.BlockClicksBriefly();
            m_Input.DeactivateInputField();
            m_InputPanel.gameObject.SetActive(false);
        }

        public void ShowDialog(string message, Action onOk)
        {
            m_DialogText.text = message;
            m_DialogOk = onOk;
            GuardedButton.BlockClicksBriefly();
            m_Dialog.gameObject.SetActive(true);
            m_Dialog.SetAsLastSibling();
            m_ToastRoot.SetAsLastSibling();
        }

        public void CloseDialog()
        {
            m_DialogOk = null;
            if (m_Dialog.gameObject.activeSelf) GuardedButton.BlockClicksBriefly();
            m_Dialog.gameObject.SetActive(false);
        }

        public void ShowToast(string text, float seconds = 3f)
        {
            m_Toast.text = text;
            m_ToastRoot.gameObject.SetActive(true);
            m_ToastRoot.SetAsLastSibling();
            m_ToastUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (m_ToastRoot.gameObject.activeSelf && Time.unscaledTime > m_ToastUntil)
                m_ToastRoot.gameObject.SetActive(false);

            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen != m_LastScreen)
            {
                m_LastScreen = screen;
                Layout(screen);
            }
        }

        /// <summary>縦長の 1080×2340 の画面を、実機では全面に、エディタでは右側の領域に収める。</summary>
        void Layout(Vector2Int screen)
        {
#if UNITY_EDITOR
            float areaW = screen.x * EditorPanelFraction;
            float areaH = screen.y;
            m_Phone.anchorMin = m_Phone.anchorMax = new Vector2(1f, 0.5f);
            m_Phone.pivot = new Vector2(1f, 0.5f);
#else
            float areaW = screen.x;
            float areaH = screen.y;
            m_Phone.anchorMin = m_Phone.anchorMax = new Vector2(0.5f, 0.5f);
            m_Phone.pivot = new Vector2(0.5f, 0.5f);
#endif
            float scale = Mathf.Min(areaW / k_W, areaH / k_H);
            m_Phone.localScale = new Vector3(scale, scale, 1f);
            m_Phone.anchoredPosition = Vector2.zero;
        }

        /// <summary>画面キャプチャ用：size の領域の中央に収める。RestoreLayout で元に戻す。</summary>
        public void LayoutForCapture(Vector2 size)
        {
            m_Phone.anchorMin = m_Phone.anchorMax = m_Phone.pivot = new Vector2(0.5f, 0.5f);
            float scale = Mathf.Min(size.x / k_W, size.y / k_H);
            m_Phone.localScale = new Vector3(scale, scale, 1f);
            m_Phone.anchoredPosition = Vector2.zero;
        }

        public void RestoreLayout() => m_LastScreen = Vector2Int.zero;

        void OnEndEdit(string text)
        {
            // ソフトキーボードの「完了」/ Enter で確定（キャンセル扱いで閉じた場合は確定しない）
#if UNITY_ANDROID && !UNITY_EDITOR
            if (m_Input.touchScreenKeyboard != null && m_Input.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Done)
                SubmitInput();
#else
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.numpadEnterKey.wasPressedThisFrame))
                SubmitInput();
#endif
        }

        void SubmitInput()
        {
            if (!IsInputOpen) return;
            var text = m_Input.text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                ShowToast("文字を入力してください");
                return;
            }
            TextSubmitted?.Invoke(text);
        }

        // ---- ボタン生成 ----

        /// <summary>押せないボタンは背景と文字を薄くして、押せるボタンと見分けやすくする。</summary>
        static void SetEnabled(Button button, bool on)
        {
            button.interactable = on;
            foreach (var text in button.GetComponentsInChildren<Text>(true))
            {
                var c = text.color;
                c.a = on ? 1f : 0.3f;
                text.color = c;
            }
        }

        static Button CreateButton(string name, Transform parent, string label, Color color, int fontSize, Action onClick)
        {
            var img = UiFactory.CreateImage(name, parent, UiFactory.RoundedFill, Color.white, sliced: true);
            img.raycastTarget = true;
            Button button = img.gameObject.AddComponent<GuardedButton>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color;
            colors.selectedColor = color;
            colors.pressedColor = color * 0.75f;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.25f);
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());
            var text = UiFactory.CreateText("Label", img.rectTransform, label, fontSize, Color.white);
            UiFactory.Stretch(text.rectTransform, 8);
            return button;
        }

        /// <summary>カード内で、上から top の位置に横方向 x0〜x1 の範囲で置く。</summary>
        static void SetAnchors(RectTransform rt, float x0, float x1, float top, float height, float left, float right)
            => SetAnchorsTop(rt, x0, x1, top + 100, height, left, right);

        static void SetAnchorsTop(RectTransform rt, float x0, float x1, float top, float height, float left, float right)
        {
            rt.anchorMin = new Vector2(x0, 1);
            rt.anchorMax = new Vector2(x1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }
    }
}
