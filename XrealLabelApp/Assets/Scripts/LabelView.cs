using UnityEngine;
using UnityEngine.UI;

namespace XrealLabel
{
    public enum LabelTextStyle
    {
        /// <summary>黒文字（OSTでは透明になり、現実が透けて見える「抜き文字」）</summary>
        BlackKnockout,
        /// <summary>白文字＋黒の縁取り</summary>
        WhiteOutlined,
    }

    /// <summary>
    /// 眼鏡側に表示する1個のラベル。ワールド空間Canvasに白50%の角丸パネルと文字を置き、常にユーザーの方を向く。
    /// Canvas の1単位 = 0.5 mm（k_MetersPerUnit）。文字高 5 cm 程度（2 m 先で約1.4°）。
    /// </summary>
    public class LabelView : MonoBehaviour
    {
        const float k_MetersPerUnit = 0.0005f;
        const int k_FontSize = 100;          // 100 単位 = 5 cm
        const float k_Padding = 36f;         // 1.8 cm
        const float k_MaxTextWidth = 1200f;  // 60 cm で折り返す

        static readonly Color k_PanelColor = new Color(1f, 1f, 1f, 0.5f);
        static readonly Color k_PendingPanelColor = new Color(1f, 1f, 1f, 0.25f);
        static readonly Color k_BorderNormal = new Color(1f, 1f, 1f, 0.2f);
        static readonly Color k_BorderHover = new Color(1f, 1f, 1f, 1f);

        RectTransform m_Root;
        Image m_Panel;
        Image m_Border;
        Text m_Text;
        Outline m_Outline;
        bool m_Pending;

        public int LabelId { get; private set; }
        public RectTransform Rect => m_Root;

        public static LabelView Create(Transform parent, int labelId)
        {
            var go = new GameObject($"Label_{labelId}");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<LabelView>();
            view.Build(labelId);
            return view;
        }

        void Build(int labelId)
        {
            LabelId = labelId;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            m_Root = (RectTransform)transform;
            m_Root.localScale = Vector3.one * k_MetersPerUnit;

            m_Panel = UiFactory.CreateImage("Panel", m_Root, UiFactory.RoundedFill, k_PanelColor, sliced: true);
            m_Panel.pixelsPerUnitMultiplier = 0.5f; // 角丸の半径を 24→48 単位（2.4 cm）に
            UiFactory.Stretch(m_Panel.rectTransform);

            m_Border = UiFactory.CreateImage("HoverBorder", m_Root, UiFactory.RoundedRing, k_BorderNormal, sliced: true);
            m_Border.pixelsPerUnitMultiplier = 0.5f;
            UiFactory.Stretch(m_Border.rectTransform, -6f);

            m_Text = UiFactory.CreateText("Text", m_Root, "", k_FontSize, Color.black);
            UiFactory.Stretch(m_Text.rectTransform, k_Padding);
            m_Outline = m_Text.gameObject.AddComponent<Outline>();
            m_Outline.effectDistance = new Vector2(4f, -4f);
            m_Outline.effectColor = Color.black;
            m_Outline.enabled = false;
        }

        public void SetText(string text)
        {
            m_Text.text = text;
            Relayout();
        }

        /// <summary>入力待ちの仮ラベル表示（パネルを薄くする）。</summary>
        public void SetPending(bool pending)
        {
            m_Pending = pending;
            m_Panel.color = pending ? k_PendingPanelColor : k_PanelColor;
        }

        public void SetHover(bool hover)
        {
            m_Border.color = hover || m_Pending ? k_BorderHover : k_BorderNormal;
        }

        public void SetStyle(LabelTextStyle style)
        {
            m_Text.color = style == LabelTextStyle.BlackKnockout ? Color.black : Color.white;
            m_Outline.enabled = style == LabelTextStyle.WhiteOutlined;
        }

        /// <summary>head（ユーザーの頭の位置）から direction 方向、distance m の位置に置き、頭の方を向ける。</summary>
        public void Place(Vector3 head, Vector3 direction, float distance)
        {
            transform.position = head + direction.normalized * distance;
            Face(head);
        }

        /// <summary>ビルボード：パネルの表面をユーザーに向ける。</summary>
        public void Face(Vector3 head)
        {
            var toLabel = transform.position - head;
            if (toLabel.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(toLabel, Vector3.up);
        }

        /// <summary>視線レイがパネルに当たっているか（パネル平面との交点が矩形内か）。当たれば距離を返す。</summary>
        public bool Raycast(Ray ray, out float distance)
        {
            var plane = new Plane(-transform.forward, transform.position);
            if (plane.Raycast(ray, out distance))
            {
                var local = m_Root.InverseTransformPoint(ray.GetPoint(distance));
                return m_Root.rect.Contains(local);
            }
            return false;
        }

        void Relayout()
        {
            var settings = m_Text.GetGenerationSettings(new Vector2(k_MaxTextWidth, 0f));
            var gen = m_Text.cachedTextGeneratorForLayout;
            float scale = m_Text.pixelsPerUnit;
            float width = Mathf.Min(gen.GetPreferredWidth(m_Text.text, settings) / scale, k_MaxTextWidth);
            width = Mathf.Max(width, k_FontSize); // 1文字分以上
            settings = m_Text.GetGenerationSettings(new Vector2(width, 0f));
            float height = Mathf.Max(gen.GetPreferredHeight(m_Text.text, settings) / scale, k_FontSize);
            m_Root.sizeDelta = new Vector2(width + k_Padding * 2f, height + k_Padding * 2f);
        }
    }
}
