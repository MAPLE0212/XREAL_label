using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace XrealLabel
{
    /// <summary>
    /// 眼鏡側の頭部固定表示：中央のレティクル、案内文、通知、画角外ラベルへの矢印（F-08）。
    /// ラベルと同じ 2 m 先に置き、ピント（輻輳）を揃える。
    /// </summary>
    public class GlassesHud : MonoBehaviour
    {
        const float k_Distance = 2.0f;
        const float k_MetersPerUnit = 0.0005f;
        /// <summary>矢印を画角の端から内側にどれだけ寄せるか（画角半幅に対する比）。</summary>
        const float k_ArrowEdge = 0.86f;
        /// <summary>画面上の向きの差がこれ未満の矢印は1本にまとめる（度）。</summary>
        const float k_ArrowMergeDeg = 12f;

        Camera m_Camera;
        RectTransform m_Root;
        Text m_Guide;
        Image m_GuideBg;
        Text m_Toast;
        Image m_ToastBg;
        float m_ToastUntil;
        readonly List<Image> m_Arrows = new List<Image>();

        public static GlassesHud Create(Camera camera)
        {
            var go = new GameObject("GlassesHud");
            go.transform.SetParent(camera.transform, false);
            go.transform.localPosition = new Vector3(0, 0, k_Distance);
            go.transform.localRotation = Quaternion.identity;
            var hud = go.AddComponent<GlassesHud>();
            hud.Build(camera);
            return hud;
        }

        void Build(Camera camera)
        {
            m_Camera = camera;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;
            m_Root = (RectTransform)transform;
            m_Root.sizeDelta = new Vector2(4000, 4000);
            m_Root.localScale = Vector3.one * k_MetersPerUnit;

            // レティクル：画面中央の小さな白点（直径 1.2 cm ≒ 0.34°）
            var reticle = UiFactory.CreateImage("Reticle", m_Root, UiFactory.Circle, Color.white);
            reticle.rectTransform.sizeDelta = new Vector2(24, 24);

            // 案内文（中央より少し下）
            m_GuideBg = UiFactory.CreateImage("GuideBg", m_Root, UiFactory.RoundedFill, new Color(1, 1, 1, 0.5f), sliced: true);
            m_GuideBg.rectTransform.anchoredPosition = new Vector2(0, -330);
            m_Guide = UiFactory.CreateText("Guide", m_GuideBg.rectTransform, "", 64, Color.black);
            UiFactory.Stretch(m_Guide.rectTransform, 28);

            // 通知（中央より少し上）
            m_ToastBg = UiFactory.CreateImage("ToastBg", m_Root, UiFactory.RoundedFill, new Color(1, 1, 1, 0.7f), sliced: true);
            m_ToastBg.rectTransform.anchoredPosition = new Vector2(0, 300);
            m_Toast = UiFactory.CreateText("Toast", m_ToastBg.rectTransform, "", 60, Color.black);
            UiFactory.Stretch(m_Toast.rectTransform, 28);

            SetGuide("");
            m_ToastBg.gameObject.SetActive(false);
        }

        public void SetGuide(string text)
        {
            m_GuideBg.gameObject.SetActive(!string.IsNullOrEmpty(text));
            m_Guide.text = text;
            FitBackground(m_GuideBg.rectTransform, m_Guide, 1500);
        }

        public void ShowToast(string text, float seconds = 3f)
        {
            m_Toast.text = text;
            FitBackground(m_ToastBg.rectTransform, m_Toast, 1500);
            m_ToastBg.gameObject.SetActive(true);
            m_ToastUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            if (m_ToastBg.gameObject.activeSelf && Time.unscaledTime > m_ToastUntil)
                m_ToastBg.gameObject.SetActive(false);
        }

        /// <summary>
        /// 画角外のラベルのうち、視線との角度差が threshold 度以内のものについて、画面端にその方向を示す矢印を出す。
        /// </summary>
        public void UpdateArrows(IReadOnlyList<Vector3> labelPositions, float thresholdDeg)
        {
            // 2 m 先の平面での見える範囲（半幅・半高、Canvas単位）
            float halfH = k_Distance * Mathf.Tan(m_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) / k_MetersPerUnit;
            float halfW = halfH * m_Camera.aspect;

            int used = 0;
            var camT = m_Camera.transform;
            foreach (var pos in labelPositions)
            {
                var dir = pos - camT.position;
                float angle = Vector3.Angle(camT.forward, dir);
                if (angle > thresholdDeg) continue;
                if (IsInView(pos)) continue;

                var local = camT.InverseTransformDirection(dir);
                var screenDir = new Vector2(local.x, local.y);
                if (screenDir.sqrMagnitude < 1e-8f) continue;
                screenDir.Normalize();

                // 画角の矩形の縁（内側に寄せた位置）に置く
                float tx = Mathf.Abs(screenDir.x) > 1e-5f ? halfW * k_ArrowEdge / Mathf.Abs(screenDir.x) : float.MaxValue;
                float ty = Mathf.Abs(screenDir.y) > 1e-5f ? halfH * k_ArrowEdge / Mathf.Abs(screenDir.y) : float.MaxValue;
                var arrowPos = screenDir * Mathf.Min(tx, ty);

                // 同じ方向に複数のラベルがあるときは矢印を1本にまとめる
                bool duplicate = false;
                for (int i = 0; i < used && !duplicate; i++)
                    duplicate = Vector2.Angle(m_Arrows[i].rectTransform.anchoredPosition, arrowPos) < k_ArrowMergeDeg;
                if (duplicate) continue;

                var arrow = GetArrow(used++);
                arrow.rectTransform.anchoredPosition = arrowPos;
                arrow.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg);
                arrow.gameObject.SetActive(true);
            }
            for (int i = used; i < m_Arrows.Count; i++)
                m_Arrows[i].gameObject.SetActive(false);
        }

        bool IsInView(Vector3 worldPos)
        {
            var vp = m_Camera.WorldToViewportPoint(worldPos);
            return vp.z > 0 && vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1;
        }

        Image GetArrow(int index)
        {
            while (m_Arrows.Count <= index)
            {
                var arrow = UiFactory.CreateImage("Arrow", m_Root, UiFactory.Arrow, Color.white);
                arrow.rectTransform.sizeDelta = new Vector2(90, 90);
                m_Arrows.Add(arrow);
            }
            return m_Arrows[index];
        }

        static void FitBackground(RectTransform bg, Text text, float maxWidth)
        {
            const float pad = 28f;
            var gen = text.cachedTextGeneratorForLayout;
            var settings = text.GetGenerationSettings(new Vector2(maxWidth, 0));
            float w = Mathf.Min(gen.GetPreferredWidth(text.text, settings) / text.pixelsPerUnit, maxWidth);
            settings = text.GetGenerationSettings(new Vector2(w, 0));
            float h = gen.GetPreferredHeight(text.text, settings) / text.pixelsPerUnit;
            bg.sizeDelta = new Vector2(w + pad * 2, h + pad * 2);
        }
    }
}
