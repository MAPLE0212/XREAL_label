using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace XrealLabel
{
    /// <summary>
    /// 操作ログをCSVで残す。列は仕様書の timestamp, event, labelId, headYaw, headPitch, headRoll に、
    /// 補足用の detail 列（キー種別など）を末尾に追加している。
    /// 保存先：Application.persistentDataPath/Logs/（Beam Pro では /sdcard/Android/data/&lt;package&gt;/files/Logs/）
    /// </summary>
    public class OperationLogger : IDisposable
    {
        public const string OriginSet = "origin_set";
        public const string Recenter = "recenter";
        public const string Place = "place";
        public const string Edit = "edit";
        public const string Delete = "delete";
        public const string ClearAll = "clear_all";
        public const string KeyPress = "key_press";
        public const string RecenterDetected = "recenter_detected";

        readonly StreamWriter m_Writer;
        readonly string m_SessionPath;

        public string CsvPath { get; }

        public OperationLogger()
        {
            var dir = Path.Combine(Application.persistentDataPath, "Logs");
            Directory.CreateDirectory(dir);
            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            CsvPath = Path.Combine(dir, $"oplog_{stamp}.csv");
            m_SessionPath = Path.Combine(dir, $"session_{stamp}.json");
            m_Writer = new StreamWriter(CsvPath, false, new UTF8Encoding(false)) { AutoFlush = true };
            m_Writer.WriteLine("timestamp,event,labelId,headYaw,headPitch,headRoll,detail");
            Debug.Log($"[LabelApp] 操作ログ: {CsvPath}");
        }

        public void Log(string evt, int? labelId, float headYaw, float headPitch, float headRoll, string detail = "")
        {
            var ts = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
            var line = string.Join(",",
                ts,
                evt,
                labelId.HasValue ? labelId.Value.ToString(CultureInfo.InvariantCulture) : "",
                headYaw.ToString("F3", CultureInfo.InvariantCulture),
                headPitch.ToString("F3", CultureInfo.InvariantCulture),
                headRoll.ToString("F3", CultureInfo.InvariantCulture),
                Escape(detail));
            m_Writer.WriteLine(line);
            Debug.Log($"[LabelApp] {line}");
        }

        /// <summary>セッション情報（originYaw・originSetAt・recenterCount）をJSONで上書き保存する。</summary>
        public void SaveSession(SessionInfo session)
        {
            var json = "{\n" +
                $"  \"originSet\": {(session.originSet ? "true" : "false")},\n" +
                $"  \"originYaw\": {session.originYaw.ToString("F3", CultureInfo.InvariantCulture)},\n" +
                $"  \"originSetAt\": \"{(session.originSet ? session.originSetAt.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture) : "")}\",\n" +
                $"  \"recenterCount\": {session.recenterCount}\n" +
                "}\n";
            File.WriteAllText(m_SessionPath, json);
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        public void Dispose() => m_Writer?.Dispose();
    }
}
