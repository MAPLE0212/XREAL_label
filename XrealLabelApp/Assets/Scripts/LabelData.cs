using System;

namespace XrealLabel
{
    /// <summary>ラベル1個分のデータ（仕様書「データ仕様・ログ」）。セッション中だけメモリに保持する。</summary>
    [Serializable]
    public class LabelData
    {
        public int id;
        public string text;
        /// <summary>原点θ0からの相対yaw（度、右が正、-180〜180）</summary>
        public float yawRel;
        /// <summary>水平を0とする上下角（度、上が正）</summary>
        public float pitch;
        /// <summary>v1では2.0 m固定</summary>
        public float distance;
        public DateTime createdAt;
        public DateTime updatedAt;
    }

    /// <summary>セッション情報。originYaw（θ0）・originSetAt・recenterCount。</summary>
    [Serializable]
    public class SessionInfo
    {
        public bool originSet;
        public float originYaw;
        public DateTime originSetAt;
        public int recenterCount;
    }
}
