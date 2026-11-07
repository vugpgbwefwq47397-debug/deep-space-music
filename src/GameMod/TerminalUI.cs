using System;
using TMPro;
using UnityEngine;
namespace DeepSpaceMusic;

// Use the game's own machinery chrome and interaction sprites, scoped to our windows.
public static class TerminalUI {
    public static void ApplyOpacity(CustomUIWindow window,float value) {
        if(window==null || !window.IsAlive || window.root==null)return;
        var group=window.root.GetComponent<CanvasGroup>() ?? window.root.AddComponent<CanvasGroup>();
        group.alpha=VisualOpacity.Panel(value);
    }
    public static CustomUIBuilder Window(string id,string title,float width,float height) =>
        CustomUIManager.Instance.CreateWindow(ModEntry.Id+id,title,CustomUIManager.LAYER_OVERLAY,style:CustomUIManager.Style.Machinery)
            .SetSize(width,height).SetCloseOnEscape();

    public static void Fill(CustomUIElement element,float height=44) {
        if(element?.root==null) return;
        var layout=element.root.GetComponent<UnityEngine.UI.LayoutElement>() ?? element.root.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.minWidth=0;layout.preferredWidth=0;layout.flexibleWidth=1;
        layout.minHeight=height;layout.preferredHeight=height;
    }
    public static void Column(CustomUIBuilder b) {
        var layout=b.CurrentContainer.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.minWidth=0;layout.preferredWidth=0;layout.flexibleWidth=1;
    }
    public static void Label(CustomUIBuilder b,string text,float width=132) {
        b.AddLabel(text);b.Last.SetPreferredSize(width,36);
        var layout=b.Last.root.GetComponent<UnityEngine.UI.LayoutElement>();
        layout.minWidth=width;layout.flexibleWidth=0;
        b.Last.text.enableWordWrapping=false;
    }
    public static void Dropdown(CustomUIBuilder b,string[] values,int initial,Action<int> change) {
        // Keep the native dropdown's arrow, checkmark and scrolling hierarchy intact.
        b.UseMachineryStyle(false).AddDropdown(values,initial,change).UseMachineryStyle();
        Fill(b.Last);
        var dropdown=b.Last.dropdown;
        if(dropdown==null) return;
        var image=dropdown.targetGraphic as UnityEngine.UI.Image;
        if(image!=null) {
            Plate(image,MachineryStyle.ButtonNormal,Color.white);
            dropdown.transition=UnityEngine.UI.Selectable.Transition.SpriteSwap;
            var sprites=dropdown.spriteState;sprites.highlightedSprite=MachineryStyle.ButtonHover;
            sprites.pressedSprite=MachineryStyle.ButtonPressed;sprites.selectedSprite=null;dropdown.spriteState=sprites;
        }
        if(dropdown.captionText!=null) {
            dropdown.captionText.color=MachineryStyle.BUTTON_TEXT;
            var rect=dropdown.captionText.rectTransform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(44,2);rect.offsetMax=new Vector2(-44,-2);
            dropdown.captionText.margin=Vector4.zero;
        }
        if(dropdown.itemText!=null) {
            dropdown.itemText.color=MachineryStyle.VALUE_TEXT;
            var item=dropdown.itemText.GetComponentInParent<UnityEngine.UI.Toggle>();
            if(item!=null) {
                var rect=item.transform as RectTransform;rect.sizeDelta=new Vector2(rect.sizeDelta.x,44);
            }
        }
        if(dropdown.template!=null) {
            dropdown.template.sizeDelta=new Vector2(dropdown.template.sizeDelta.x,320);
            var background=dropdown.template.GetComponent<UnityEngine.UI.Image>();
            if(background!=null) Plate(background,MachineryStyle.PlateNotched,MachineryStyle.SLIDER_TRACK);
        }
    }
    static void Plate(UnityEngine.UI.Image image,Sprite sprite,Color color) {
        image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;
        image.pixelsPerUnitMultiplier=.5f;image.color=color;
    }
    public static CustomUIWindow Show(CustomUIBuilder b,TMP_FontAsset font) {
        var window=b.Show();
        float factor=Mathf.Max(.01f,window.root.GetComponentInParent<Canvas>().scaleFactor);
        foreach(var text in window.root.GetComponentsInChildren<TextMeshProUGUI>(true)) {
            var localizer=text.GetComponent<TMPLocalizer>();if(localizer!=null)localizer.enabled=false;
            if(font!=null)text.font=font;
            text.fontSize=font!=null?24/factor:22;text.enableAutoSizing=false;
            text.fontStyle=FontStyles.Normal;text.enableKerning=false;text.richText=false;
            text.alignment=TextAlignmentOptions.Center;
            text.raycastTarget=text.GetComponentInParent<UnityEngine.UI.Toggle>()!=null;
            var layout=text.GetComponent<UnityEngine.UI.LayoutElement>();
            if(layout!=null)layout.minHeight=Mathf.Max(36,layout.minHeight);
        }
        foreach(var button in window.root.GetComponentsInChildren<UnityEngine.UI.Button>(true)) {
            // The close button keeps the game's compact header dimensions.
            if(window.frame!=null && button==window.frame.closeButton)continue;
            var layout=button.GetComponent<UnityEngine.UI.LayoutElement>();
            if(layout!=null){layout.minHeight=44;layout.preferredHeight=44;}
        }
        CustomUIBuilder.RebuildLayout(window.ContentRoot);
        ApplyOpacity(window,ModEntry.PanelOpacity.Value);return window;
    }
}
