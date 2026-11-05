using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace DeepSpaceMusic;
public class NeonDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ICanvasRaycastFilter {
    Vector2 startPointer, startOffset;
    public bool IsRaycastLocationValid(Vector2 point,Camera camera) => ModEntry.Draggable.Value && GetComponent<TMP_Text>().color.a>.05f && TMP_TextUtilities.FindIntersectingCharacter(GetComponent<TMP_Text>(),point,camera,true)>=0;
    public void OnBeginDrag(PointerEventData e) {startPointer=e.position;startOffset=new Vector2(ModEntry.X.Value,ModEntry.Y.Value);}
    public void OnDrag(PointerEventData e) {
        var scale=GetComponentInParent<Canvas>().scaleFactor;
        var p=startOffset+(e.position-startPointer)/scale;
        var bounds=GetComponentInParent<Canvas>().GetComponent<RectTransform>().rect;
        float baseY=ModEntry.Position.Value=="上方"?420:ModEntry.Position.Value=="中间"?0:-390;
        float margin=Mathf.Min(bounds.width/2-20,GetComponent<TMP_Text>().preferredWidth/2+12);
        p.x=Mathf.Clamp(p.x,-bounds.width/2+margin,bounds.width/2-margin);
        p.y=Mathf.Clamp(p.y,-bounds.height/2-baseY+70,bounds.height/2-baseY-40);
        ModEntry.X.Set(Mathf.Round(p.x/2)*2);ModEntry.Y.Set(Mathf.Round(p.y/2)*2);
    }
    public void OnEndDrag(PointerEventData e) {ModEntry.Config.Save();}
}
public class PixelWiggle : MonoBehaviour {
    TextMeshProUGUI label;Canvas canvas;int phase=-1;string lastMode="";float lastAmount=-1;bool lastReduced,wasVisible;
    void Awake() {label=GetComponent<TextMeshProUGUI>();canvas=GetComponentInParent<Canvas>();label.OnPreRenderText+=Deform;}
    void LateUpdate() {
        if(Terminal.Instance==null) return;
        bool visible=canvas!=null && canvas.isActiveAndEnabled && label.color.a>.005f && label.text.Length>0;
        if(!visible) {wasVisible=false;return;}
        string mode=ModEntry.AnimationMode.Value;float amount=ModEntry.Motion.Value;bool reduced=ModEntry.ReducedMotion.Value;
        int p=Mathf.FloorToInt(Terminal.Instance.AnimationClock*LyricMotion.FramesPerSecond);
        bool changed=lastMode!=mode || lastAmount!=amount || lastReduced!=reduced;
        if(!wasVisible || changed || (p!=phase && !reduced && amount>0)) {
            phase=p;lastMode=mode;lastAmount=amount;lastReduced=reduced;
            // SetVerticesDirty alone does not open this TMP version's properties-changed gate.
            // Regenerate clean glyphs before OnPreRenderText so deformation cannot accumulate.
            label.ForceMeshUpdate();
        }
        wasVisible=true;
    }
    void Deform(TMP_TextInfo info) {
        if(Terminal.Instance==null || ModEntry.ReducedMotion.Value || ModEntry.Motion.Value<=0) return;
        float factor=Mathf.Max(.01f,canvas.scaleFactor);
        float amount=ModEntry.Motion.Value*(gameObject.name=="Next"?.55f:1f);
        float unit=Mathf.Clamp(label.fontSize*factor/24f,.75f,3f)/factor;
        float seconds=Mathf.Floor(Terminal.Instance.AnimationClock*LyricMotion.FramesPerSecond)/LyricMotion.FramesPerSecond;
        for(int i=0;i<info.characterCount;i++) {
            var c=info.characterInfo[i];if(!c.isVisible)continue;
            var vertices=info.meshInfo[c.materialReferenceIndex].vertices;
            var pose=LyricMotion.Sample(ModEntry.AnimationMode.Value,seconds,i,info.characterCount,amount);
            var delta=new Vector3(Mathf.Round(pose.X*unit*factor)/factor,Mathf.Round(pose.Y*unit*factor)/factor,0);
            float cos=Mathf.Cos(pose.Angle),sin=Mathf.Sin(pose.Angle);
            var center=(vertices[c.vertexIndex]+vertices[c.vertexIndex+2])*.5f;
            for(int j=0;j<4;j++) {
                var local=vertices[c.vertexIndex+j]-center;local.x*=pose.ScaleX;local.y*=pose.ScaleY;
                var v=center+new Vector3(local.x*cos-local.y*sin,local.x*sin+local.y*cos,local.z)+delta;
                vertices[c.vertexIndex+j]=new Vector3(Mathf.Round(v.x*factor)/factor,Mathf.Round(v.y*factor)/factor,v.z);
            }
        }
    }
    void OnDestroy() {if(label!=null)label.OnPreRenderText-=Deform;}
}
