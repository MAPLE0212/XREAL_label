using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace XrealLabel.Tests
{
    /// <summary>
    /// エディタ（XRデバイスなし）で Main シーンを動かし、仕様書の操作フローを通しで確認する。
    /// 眼鏡ビューと Beam Pro 画面のスクリーンショットを Logs/TestCaptures/ に保存する。
    /// </summary>
    public class LabelAppFlowTests
    {
        const string k_CaptureDir = "Logs/TestCaptures";

        EditorHeadSimulator m_Sim;
        Camera m_Camera;
        float m_Yaw;
        float m_Pitch;

        [UnityTest]
        public IEnumerator FullFlow()
        {
            SceneManager.LoadScene("Main");
            yield return null;
            yield return null;
            m_Sim = Object.FindAnyObjectByType<EditorHeadSimulator>();
            m_Camera = Camera.main;
            Assert.IsNotNull(m_Sim, "EditorHeadSimulator がない");
            Directory.CreateDirectory(k_CaptureDir);

            // ---- 起動時の安全表示 ----
            StringAssert.Contains("開始", ConfirmLabel());
            StringAssert.Contains("座って", Hud("Guide"));
            yield return Capture("01_safety");

            Click("Confirm");
            yield return null;
            StringAssert.Contains("原点", ConfirmLabel());

            // ---- F-01 原点設定（yaw=10°）----
            Click("Confirm"); // 原点未設定のあいだは配置できない（ここで原点が設定される）
            yield return null;
            Assert.AreEqual(0, LabelCount(), "原点設定の確定でラベルが置かれてはいけない");
            yield return TurnTo(10, 0);
            // 原点は最初の確定時点（yaw=0）で設定されている。リセンターして yaw=10 でやり直す
            yield return RecenterViaButton();
            yield return TurnTo(10, 0);
            Click("Confirm");
            yield return null;
            StringAssert.Contains("ラベルを置く", ConfirmLabel());

            // ---- F-02 配置：yaw=30, pitch=5 に「机」----
            yield return TurnTo(30, -5); // 模擬は pitch 下向きが正なので -5 で上を向く
            Click("Confirm");
            yield return null;
            Assert.IsTrue(InputOpen(), "文字入力が開かない");
            yield return Capture("02_input");
            SetInput("机");
            Click("OK");
            yield return null;
            Assert.AreEqual(1, LabelCount());
            var label1 = Labels().First();
            var dir = (label1.transform.position - m_Camera.transform.position).normalized;
            Assert.AreEqual(30f, HeadMath.Yaw(dir), 0.5f, "ラベルのyawがずれている");
            Assert.AreEqual(5f, HeadMath.Pitch(dir), 0.5f, "ラベルのpitchがずれている");
            Assert.AreEqual(2.0f, Vector3.Distance(label1.transform.position, m_Camera.transform.position), 0.01f);

            // ---- F-03 ホバー ----
            yield return null;
            StringAssert.Contains("机", Text("HoverText"));
            Assert.IsTrue(Button("Edit").interactable);
            yield return TurnTo(0, 0);
            StringAssert.Contains("なし", Text("HoverText"));
            Assert.IsFalse(Button("Edit").interactable);

            // ---- F-04 編集 ----
            yield return TurnTo(30, -5);
            Click("Edit");
            yield return null;
            Assert.IsTrue(InputOpen());
            SetInput("作業机");
            Click("OK");
            yield return null;
            Assert.AreEqual("作業机", label1.GetComponentInChildren<Text>().text);

            // ---- 候補（プリセット）で配置 ----
            yield return TurnTo(-20, 0);
            Click("Confirm");
            yield return null;
            Click("Preset_1"); // ドア
            yield return null;
            Assert.AreEqual(2, LabelCount());
            yield return TurnTo(5, 3);
            Click("Confirm");
            yield return null;
            SetInput("長いラベルの折り返しを確認するためのテキストです");
            Click("OK");
            yield return null;
            yield return TurnTo(10, 0);
            yield return Capture("03_labels");

            // ---- F-08 画角外の矢印：yaw=-30 を向くと、長いラベル(yaw=5) が画角外・40°以内 ----
            yield return TurnTo(-30, 0);
            yield return null;
            int arrows = ActiveArrows();
            Assert.Greater(arrows, 0, "画角外・40°以内のラベルに矢印が出ていない");
            yield return Capture("04_arrow");
            yield return TurnTo(-110, 0);
            Assert.AreEqual(0, ActiveArrows(), "40°より離れたラベルに矢印が出ている");

            // ---- F-05 削除（ホバー中）----
            yield return TurnTo(-20, 0);
            StringAssert.Contains("ドア", Text("HoverText"));
            Click("Delete");
            yield return null;
            Assert.AreEqual(2, LabelCount());

            // ---- F-09 上限 20 個 ----
            for (int i = LabelCount(); i < 20; i++)
            {
                yield return TurnTo(-170 + i * 8, 20);
                Click("Confirm");
                yield return null;
                Click("Preset_0");
                yield return null;
            }
            Assert.AreEqual(20, LabelCount());
            Click("Confirm");
            yield return null;
            Assert.IsFalse(InputOpen(), "上限を超えて入力が開いた");
            StringAssert.Contains("最大20", Text("Toast"));
            yield return Capture("05_limit");

            // ---- F-06 全消去（キャンセル → OK）----
            Click("ClearAll");
            yield return null;
            Assert.IsTrue(DialogOpen());
            ClickIn("Dialog", "Cancel");
            yield return null;
            Assert.AreEqual(20, LabelCount());
            Click("ClearAll");
            yield return null;
            ClickIn("Dialog", "OK");
            yield return null;
            yield return null;
            Assert.AreEqual(0, LabelCount());

            // ---- F-07 リセンター（確認ダイアログの文言）----
            yield return TurnTo(40, 0);
            Click("Confirm");
            yield return null;
            Click("Preset_2");
            yield return null;
            Assert.AreEqual(1, LabelCount());
            Click("Recenter");
            yield return null;
            StringAssert.Contains("ラベルがすべてリセットされますが、よろしいですか？", Text("Message"));
            yield return Capture("06_recenter_dialog");
            ClickIn("Dialog", "OK");
            yield return null;
            yield return null;
            Assert.AreEqual(0, LabelCount());
            StringAssert.Contains("原点", ConfirmLabel());
            // リセンター後は今の向きが yaw=0
            Assert.AreEqual(0f, HeadMath.Yaw(m_Camera.transform.forward), 0.5f);
            SyncYawFromCamera();

            // ---- システム側リセンターの検知（yaw の跳び）----
            Click("Confirm");
            yield return null;
            yield return TurnTo(15, 0);
            Click("Confirm");
            yield return null;
            Click("Preset_3");
            yield return null;
            Assert.AreEqual(1, LabelCount());
            yield return new WaitForSecondsRealtime(3.2f); // 起動直後（3秒）とアプリからのリセンター直後（1秒）の検知抑制が切れるのを待つ
            m_Yaw += 60f;
            m_Sim.SetHead(m_Yaw, m_Pitch); // 1フレームで 60° 跳ぶ
            yield return null;
            yield return null;
            Assert.AreEqual(0, LabelCount(), "システムリセンター検知でラベルが消えていない");
            StringAssert.Contains("リセンターされたためラベルをリセットしました", Text("Toast"));
            StringAssert.Contains("原点", ConfirmLabel());
            yield return Capture("07_recenter_detected");

            // ---- 操作ログ CSV ----
            var csv = Directory.GetFiles(Path.Combine(Application.persistentDataPath, "Logs"), "oplog_*.csv")
                .OrderBy(File.GetLastWriteTimeUtc).Last();
            var log = ReadShared(csv);
            Debug.Log($"[Test] CSV: {csv}\n{log}");
            StringAssert.StartsWith("timestamp,event,labelId,headYaw,headPitch,headRoll", log);
            foreach (var evt in new[] { "origin_set", "recenter", "place", "edit", "delete", "clear_all", "key_press", "recenter_detected" })
                StringAssert.Contains("," + evt + ",", log, $"CSV に {evt} がない");
            File.Copy(csv, Path.Combine(k_CaptureDir, "oplog_sample.csv"), true);
        }

        // ---------------- helpers ----------------

        /// <summary>yaw 検知に引っかからないよう、1フレーム 8° 以下で頭を回す。</summary>
        IEnumerator TurnTo(float yaw, float pitch)
        {
            while (Mathf.Abs(m_Yaw - yaw) > 0.01f || Mathf.Abs(m_Pitch - pitch) > 0.01f)
            {
                m_Yaw = Mathf.MoveTowards(m_Yaw, yaw, 8f);
                m_Pitch = Mathf.MoveTowards(m_Pitch, pitch, 8f);
                m_Sim.SetHead(m_Yaw, m_Pitch);
                yield return null;
            }
            yield return null;
            yield return null;
        }

        void SyncYawFromCamera()
        {
            var e = m_Camera.transform.parent.localEulerAngles;
            m_Yaw = HeadMath.NormalizeAngle(e.y);
            m_Pitch = HeadMath.NormalizeAngle(e.x);
            m_Sim.SetHead(m_Yaw, m_Pitch);
        }

        IEnumerator RecenterViaButton()
        {
            Click("Recenter");
            yield return null;
            ClickIn("Dialog", "OK");
            yield return null;
            SyncYawFromCamera();
            yield return null;
        }

        static GameObject PhoneRoot => GameObject.Find("PhoneUI");

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static Button Button(string name)
        {
            // 入力パネル・ダイアログにも同名ボタンがあるので、表示中のものを優先する
            var all = PhoneRoot.GetComponentsInChildren<Button>(true).Where(b => b.name == name).ToArray();
            var active = all.FirstOrDefault(b => b.gameObject.activeInHierarchy);
            Assert.IsNotNull(active ?? all.FirstOrDefault(), $"ボタン {name} がない");
            return active ?? all.First();
        }

        static void Click(string name)
        {
            var b = Button(name);
            Assert.IsTrue(b.gameObject.activeInHierarchy, $"ボタン {name} が表示されていない");
            Assert.IsTrue(b.interactable, $"ボタン {name} が無効");
            b.onClick.Invoke();
        }

        static void ClickIn(string panel, string name)
        {
            var root = FindDeep(PhoneRoot.transform, panel);
            var b = root.GetComponentsInChildren<Button>(true).First(x => x.name == name);
            b.onClick.Invoke();
        }

        static string Text(string name) => FindDeep(PhoneRoot.transform, name).GetComponentInChildren<Text>(true).text;
        static string ConfirmLabel() => FindDeep(PhoneRoot.transform, "Confirm").GetComponentInChildren<Text>().text;
        static bool InputOpen() => FindDeep(PhoneRoot.transform, "InputPanel").gameObject.activeSelf;
        static bool DialogOpen() => FindDeep(PhoneRoot.transform, "Dialog").gameObject.activeSelf;

        static void SetInput(string text)
        {
            var field = FindDeep(PhoneRoot.transform, "InputPanel").GetComponentInChildren<InputField>();
            field.text = text;
        }

        static string Hud(string name) => FindDeep(GameObject.Find("GlassesHud").transform, name).GetComponent<Text>().text;

        static LabelView[] Labels() => Object.FindObjectsByType<LabelView>(FindObjectsSortMode.None).Where(v => v.LabelId > 0).ToArray();
        static int LabelCount() => Labels().Length;

        static int ActiveArrows() => GameObject.Find("GlassesHud").GetComponentsInChildren<Image>(false).Count(i => i.name == "Arrow");

        static string ReadShared(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            return sr.ReadToEnd();
        }

        /// <summary>眼鏡ビュー（メインカメラ）と Beam Pro 画面をそれぞれ PNG に保存する。</summary>
        IEnumerator Capture(string name)
        {
            yield return null; // バッチモードでは WaitForEndOfFrame が返らないので使わない
            Canvas.ForceUpdateCanvases();

            // 眼鏡ビュー：OST では黒が透明。見やすいよう背景をグレーにして撮る
            var rt = new RenderTexture(1280, 720, 24);
            var rect = m_Camera.rect;
            var bg = m_Camera.backgroundColor;
            m_Camera.rect = new Rect(0, 0, 1, 1);
            m_Camera.backgroundColor = new Color(0.35f, 0.38f, 0.42f);
            m_Camera.targetTexture = rt;
            m_Camera.Render();
            m_Camera.targetTexture = null;
            m_Camera.rect = rect;
            m_Camera.backgroundColor = bg;
            Save(rt, $"{name}_glasses.png");

            // Beam Pro 画面
            var phone = Object.FindAnyObjectByType<PhoneUI>();
            var canvas = phone.GetComponent<Canvas>();
            var capGo = new GameObject("CaptureCam");
            capGo.transform.position = new Vector3(0, -1000, 0);
            var cap = capGo.AddComponent<Camera>();
            cap.clearFlags = CameraClearFlags.SolidColor;
            cap.backgroundColor = Color.black;
            var prt = new RenderTexture(540, 1170, 24);
            cap.targetTexture = prt;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cap;
            canvas.planeDistance = 1f;
            phone.LayoutForCapture(new Vector2(540, 1170));
            Canvas.ForceUpdateCanvases();
            cap.Render();
            Save(prt, $"{name}_phone.png");
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            phone.RestoreLayout();
            Object.Destroy(capGo);
            rt.Release();
            prt.Release();
        }

        static void Save(RenderTexture rt, string file)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(k_CaptureDir, file), tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
