using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using PixelCleaners;

public class NavButtonSetter : MonoBehaviour
{
    public enum Destination { AR, Map, Factory, Creature, Bag, Delivery }
    public Destination destination;

    void Start()
    {
        bool isCurrent = SceneManager.GetActiveScene().name == DestToScene(destination);

        if (!TryGetComponent(out Button btn))
            btn = GetComponentInChildren<Button>(true);
        if (btn == null) return;

        if (isCurrent)
        {
            btn.interactable = false;
            ApplyCurrentStyle();
        }
        else
        {
            btn.onClick.AddListener(() => DestToAction(destination)());
        }
    }

    void ApplyCurrentStyle()
    {
        // 배경 이미지: 밝은 강조색으로
        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images)
            img.color = new Color(0.35f, 0.45f, 0.65f, 1f);

        // 텍스트: 흰색 + 반투명
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            tmp.color = new Color(1f, 1f, 1f, 0.5f);
            tmp.fontStyle = FontStyles.Normal;
        }

        // 하단 밑줄 표시
        var underlineGO = new GameObject("Underline");
        underlineGO.transform.SetParent(transform, false);
        var rt = underlineGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0f);
        rt.anchorMax = new Vector2(0.9f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, 4f);
        rt.anchoredPosition = new Vector2(0f, 4f);
        underlineGO.AddComponent<Image>().color = new Color(0.6f, 0.8f, 1f, 0.9f);
    }

    static string DestToScene(Destination d) => d switch
    {
        Destination.AR       => SceneController.AR,
        Destination.Map      => SceneController.Map,
        Destination.Factory  => SceneController.Factory,
        Destination.Creature => SceneController.Creature,
        Destination.Bag      => SceneController.Bag,
        Destination.Delivery => SceneController.Delivery,
        _                    => ""
    };

    static System.Action DestToAction(Destination d) => d switch
    {
        Destination.AR       => SceneController.GoAR,
        Destination.Map      => SceneController.GoMap,
        Destination.Factory  => SceneController.GoFactory,
        Destination.Creature => SceneController.GoCreature,
        Destination.Bag      => SceneController.GoBag,
        Destination.Delivery => SceneController.GoDelivery,
        _                    => () => { }
    };
}
