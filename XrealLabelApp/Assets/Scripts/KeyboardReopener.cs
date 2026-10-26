using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XrealLabel
{
    /// <summary>
    /// 入力欄をタップしたら、ソフトキーボードを開き直す。
    /// XREAL のアプリでは眼鏡側にも画面（NRFakeActivity）があり、眼鏡の Xボタンで確定した直後は
    /// 入力フォーカスが眼鏡側にあるため、キーボードの表示が失敗したまま入力欄だけ「入力中」になることがある
    /// （2026-10-25 実機確認）。Beam Pro の画面をタップすればフォーカスが移るので、そこで開き直す。
    /// </summary>
    [RequireComponent(typeof(InputField))]
    public class KeyboardReopener : MonoBehaviour, IPointerClickHandler
    {
        InputField m_Field;

        void Awake() => m_Field = GetComponent<InputField>();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Application.isMobilePlatform) return;
            StartCoroutine(Reopen());
        }

        IEnumerator Reopen()
        {
            // InputField 自身のクリック処理が終わってから開き直す
            yield return null;
            if (m_Field.isFocused)
                m_Field.DeactivateInputField();
            yield return null;
            m_Field.Select();
            m_Field.ActivateInputField();
            Debug.Log($"[LabelApp] ソフトキーボードを開き直しました（visible={TouchScreenKeyboard.visible}）");
        }
    }
}
