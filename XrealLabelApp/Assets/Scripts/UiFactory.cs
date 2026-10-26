using UnityEngine;
using UnityEngine.UI;

namespace XrealLabel
{
    /// <summary>UI用のスプライトとフォントを実行時に生成する（外部アセットなしで動かすため）。</summary>
    public static class UiFactory
    {
        static Sprite s_RoundedFill;
        static Sprite s_RoundedRing;
        static Sprite s_Circle;
        static Sprite s_Arrow;
        static Font s_Font;

        const int k_Size = 64;
        const int k_Radius = 24;

        /// <summary>角丸の塗りつぶし（9スライス用）。</summary>
        public static Sprite RoundedFill => s_RoundedFill ??= CreateRounded(0);

        /// <summary>角丸の枠線（9スライス用）。</summary>
        public static Sprite RoundedRing => s_RoundedRing ??= CreateRounded(5);

        public static Sprite Circle => s_Circle ??= CreateCircle();

        /// <summary>右向き（+x）の三角形の矢印。</summary>
        public static Sprite Arrow => s_Arrow ??= CreateArrow();

        /// <summary>
        /// 日本語を表示できるフォント。OSフォント（Noto Sans CJK JP など）を優先し、
        /// 見つからなければ組み込みフォントを使う（Android では組み込みフォントもOSのCJKフォントにフォールバックする）。
        /// </summary>
        public static Font Font
        {
            get
            {
                if (s_Font != null) return s_Font;
                string[] candidates = { "Noto Sans CJK JP", "Noto Sans JP", "NotoSansCJK-Regular", "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Yu Gothic UI", "Meiryo" };
                var installed = Font.GetOSInstalledFontNames();
                foreach (var name in candidates)
                {
                    if (System.Array.IndexOf(installed, name) >= 0)
                    {
                        s_Font = Font.CreateDynamicFontFromOSFont(name, 64);
                        break;
                    }
                }
                if (s_Font == null)
                    s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return s_Font;
            }
        }

        static Sprite CreateRounded(int ringThickness)
        {
            var tex = NewTexture();
            var px = new Color32[k_Size * k_Size];
            for (int y = 0; y < k_Size; y++)
            for (int x = 0; x < k_Size; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f);
                float a = Mathf.Clamp01(0.5f - d);
                if (ringThickness > 0)
                    a *= Mathf.Clamp01(d + ringThickness + 0.5f);
                px[y * k_Size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            var border = new Vector4(k_Radius, k_Radius, k_Radius, k_Radius);
            return Sprite.Create(tex, new Rect(0, 0, k_Size, k_Size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        /// <summary>角丸矩形の符号付き距離（内側が負）。</summary>
        static float RoundedRectDistance(float x, float y)
        {
            float half = k_Size * 0.5f;
            float qx = Mathf.Abs(x - half) - (half - k_Radius);
            float qy = Mathf.Abs(y - half) - (half - k_Radius);
            float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0);
            return outside + inside - k_Radius;
        }

        static Sprite CreateCircle()
        {
            var tex = NewTexture();
            var px = new Color32[k_Size * k_Size];
            float r = k_Size * 0.5f - 1f;
            for (int y = 0; y < k_Size; y++)
            for (int x = 0; x < k_Size; x++)
            {
                float d = new Vector2(x + 0.5f - k_Size * 0.5f, y + 0.5f - k_Size * 0.5f).magnitude - r;
                px[y * k_Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, k_Size, k_Size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite CreateArrow()
        {
            var tex = NewTexture();
            var px = new Color32[k_Size * k_Size];
            // 頂点 (60,32), (8,8), (8,56) の三角形
            Vector2 a = new Vector2(60, 32), b = new Vector2(8, 6), c = new Vector2(8, 58);
            for (int y = 0; y < k_Size; y++)
            for (int x = 0; x < k_Size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float e = Mathf.Min(Edge(a, b, p), Mathf.Min(Edge(b, c, p), Edge(c, a, p)));
                px[y * k_Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(e + 0.5f) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, k_Size, k_Size), new Vector2(0.5f, 0.5f), 100f);
        }

        /// <summary>辺 a→b から点 p までの符号付き距離（三角形の内側が正）。</summary>
        static float Edge(Vector2 a, Vector2 b, Vector2 p)
        {
            // 頂点は時計回りに並んでいるので、辺の右側が内側
            var n = new Vector2(b.y - a.y, a.x - b.x).normalized;
            return Vector2.Dot(p - a, n);
        }

        static Texture2D NewTexture() => new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        // ---- uGUI 生成ヘルパー ----

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent != null ? parent.gameObject.layer : 0;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image CreateImage(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
        {
            var img = CreateRect(name, parent).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            return img;
        }

        public static Text CreateText(string name, Transform parent, string content, int fontSize, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>親の上端から top の位置に、左右 margin・高さ height で配置する。</summary>
        public static void PlaceTop(RectTransform rt, float top, float height, float margin = 0f)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(margin, -top - height);
            rt.offsetMax = new Vector2(-margin, -top);
        }
    }
}
