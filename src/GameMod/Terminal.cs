using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace DeepSpaceMusic;
public class Terminal : MonoBehaviour {
    public static Terminal Instance;
    public Snapshot State=new Snapshot {bridge="offline"};
    public bool Bound;
    struct Reply {public int epoch,token,view;public bool cancelled;public string payload,action;}
    readonly ConcurrentQueue<Reply> responses=new ConcurrentQueue<Reply>();
    readonly CancellationTokenSource stop=new CancellationTokenSource();
    bool polling,settingsDirty;float pollAt,saveAt,transition,diagnosticUntil;
    int epoch,viewRevision;
    string lastKey="",lastTrack="",lastSettings="";TextMeshProUGUI current,next,outgoing;
    string diagnostic="",lastError="";
    Canvas canvas; CustomUIWindow panel; TMP_Text status,feedback;
    readonly PendingOperation clientOperation=new PendingOperation();
    readonly PendingOperation commandOperation=new PendingOperation(.25);
    readonly PendingOperation claimOperation=new PendingOperation(.75);
    TMP_FontAsset pixelFont;
    CustomUIWindow pickerPanel,toolsPanel;TMP_Text details;
    public float AnimationClock {get;private set;}
    public bool MotionActive=>Bound && State.playback=="playing";
    void Awake() {
        Instance=this;
        if(ModEntry.Reconnect.Value && ModEntry.ConnectionEnabled.Value && MusicPlatforms.IsSupported(ModEntry.Platform.Value)) Send("connect");
    }
    IEnumerator Start() {
        while(FontLoader.Instance==null) yield return null;
        try {pixelFont=PixelFont.Load();Debug.Log("[DeepSpaceMusic] 像素字体已加载：Fusion Pixel 12px，38131 字形");} catch(Exception e) {Debug.LogError("[DeepSpaceMusic] 像素字体加载失败，使用游戏字体："+e);}
        var go=new GameObject("Neon lyrics",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(transform,false);
        canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=90;
        canvas.pixelPerfect=true;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        current=Line(go.transform,"Current");next=Line(go.transform,"Next");outgoing=Line(go.transform,"Outgoing");
    }
    TextMeshProUGUI Line(Transform parent,string name) {
        var go=new GameObject(name,typeof(RectTransform));go.SetActive(false);go.transform.SetParent(parent,false);
        var text=go.AddComponent<TextMeshProUGUI>();text.alignment=TextAlignmentOptions.Center;
        if(pixelFont!=null) text.font=pixelFont;
        else {var localizer=go.AddComponent<TMPLocalizer>();localizer.ChangeFontOnly=true;localizer.fontRole=FontLoader.FontRole.Noto_Sans;}
        text.enableKerning=false;
        text.raycastTarget=true;text.richText=false;text.enableWordWrapping=false;text.overflowMode=TextOverflowModes.Ellipsis;
        text.rectTransform.sizeDelta=new Vector2(1300,65);go.AddComponent<NeonDrag>();go.AddComponent<PixelWiggle>();go.SetActive(true);return text;
    }
    public void Send(string action,string command="",bool urgent=false) {
        if(action=="command" && (ModEntry.Platform.Value=="未选择" || State.platform!=ModEntry.Platform.Value)) {diagnostic="请先选择平台并连接，当前会话与所选平台不一致";diagnosticUntil=Time.unscaledTime+5;return;}
        // Eject/stop must still pause after an earlier control request finishes.
        if(action=="command" && command=="pause" && urgent)commandOperation.Reset();
        int token=0;
        if(action=="command" && !commandOperation.TryBegin(Time.unscaledTime,out token)) {diagnostic="播放操作正在处理，请稍候";diagnosticUntil=Time.unscaledTime+2;return;}
        if(action=="connect" || action=="disconnect") {
            ModEntry.ConnectionEnabled.Set(action=="connect");SaveSettings();
            commandOperation.Reset();clientOperation.Reset();
            epoch++;lastSettings="";diagnostic="";
            State=new Snapshot {platform=ModEntry.Platform.Value};
        }
        var req=new Request {clientVersion=2,action=action,command=command,platform=ModEntry.Platform.Value,autoReconnect=ModEntry.Reconnect.Value,offsetMs=ModEntry.Offset.Value};
        var payload=JsonUtility.ToJson(req);_ = SendRaw(payload,action,token);
    }
    async Task<bool> SendRaw(string payload,string action="",int token=0,int view=-1) {
        int version=epoch;
        try {
            string reply=await IpcClient.Call(payload,stop.Token,()=>version==epoch && (action!="openClient" || view==viewRevision));
            responses.Enqueue(new Reply {epoch=version,action=action,token=token,view=view,payload=reply});
            return PlaybackPresentation.Normalize(JsonUtility.FromJson<Snapshot>(reply)).ok;
        }
        catch(Exception e) {if(!stop.IsCancellationRequested) responses.Enqueue(new Reply {epoch=version,action=action,token=token,view=view,cancelled=e is OperationCanceledException,payload="ERROR:"+BridgeFailures.Describe(e)});return false;}
    }
    async void Poll(string settings) {
        polling=true;
        try {
            int connectionVersion=epoch;
            if(PlaybackPresentation.NeedsReconnect(State,ModEntry.Platform.Value,ModEntry.ConnectionEnabled.Value,ModEntry.Reconnect.Value)) {
                // Re-establish only an enabled connection after the bridge returns.
                // Never undo an explicit Disconnect, and never open a client here.
                var reconnect=new Request {clientVersion=2,action="connect",platform=ModEntry.Platform.Value,autoReconnect=true,offsetMs=ModEntry.Offset.Value};
                if(!await SendRaw(JsonUtility.ToJson(reconnect)) || connectionVersion!=epoch)return;
            }
            if(settings!=lastSettings || State.bridge=="offline") {
                int version=epoch;
                if(await SendRaw(settings) && version==epoch)lastSettings=settings;
            }
            await SendRaw("{\"protocolVersion\":1,\"clientVersion\":2,\"action\":\"status\"}");
        }
        finally {polling=false;if(State.bridge=="offline")pollAt=Time.unscaledTime+1;}
    }
    void Update() {
        if(ShortcutPressed()) {if((panel!=null && panel.IsAlive) || (toolsPanel!=null && toolsPanel.IsAlive) || (pickerPanel!=null && pickerPanel.IsAlive))CloseWindows();else Open();}
        if(settingsDirty && Time.unscaledTime>=saveAt) {ModEntry.Config.Save();settingsDirty=false;}
        if(!polling && Time.unscaledTime>=pollAt) {
            pollAt=Time.unscaledTime+0.1f;
            Poll(JsonUtility.ToJson(new Request {clientVersion=2,action="configure",offsetMs=ModEntry.Offset.Value,autoReconnect=ModEntry.Reconnect.Value}));
        }
        while(responses.TryDequeue(out var response)) {
            if(response.action=="openClient")clientOperation.Complete(response.token,Time.unscaledTime);
            if(response.action=="command")commandOperation.Complete(response.token,Time.unscaledTime);
            if(response.cancelled || response.epoch!=epoch)continue;
            try {
                State=response.payload.StartsWith("ERROR:")?new Snapshot {ok=false,bridge="offline",error=response.payload.Substring(6)}:PlaybackPresentation.Normalize(JsonUtility.FromJson<Snapshot>(response.payload));
            } catch(Exception e) {State=new Snapshot {ok=false,bridge="offline",error="桥接状态无效："+e.Message};}
            if(State.error.Length>0 && State.error!=lastError)Debug.Log("[DeepSpaceMusic] "+State.error);
            lastError=State.error;
            if(State.result.Length>0) {diagnostic=State.result;diagnosticUntil=Time.unscaledTime+5;}
            if(response.action.Length>0 && !State.ok) {diagnostic=State.error;diagnosticUntil=Time.unscaledTime+8;}
            if(response.action=="openClient" && State.ok && response.view==viewRevision)CloseWindows();
        }
        if(Bound && Deck.Owner!=null) Deck.State(Deck.Owner,State.connected?(State.playback=="playing"?1:2):0);
        if(EmporiumEntry.Instance==null && Bound) Deck.Release(ModEntry.PauseEject.Value);
        if(status!=null)status.text=PlaybackPresentation.Status(State);
        if(feedback!=null)feedback.text=Time.unscaledTime<diagnosticUntil && diagnostic.Length>0?diagnostic:"切换即生效 / 自动保存 / 拖动字幕可定位";
        if(details!=null)details.text=Time.unscaledTime<diagnosticUntil && diagnostic.Length>0?diagnostic:PlaybackPresentation.Details(State);
        if(MotionActive && !LyricMotion.IsStill(ModEntry.AnimationMode.Value,ModEntry.ReducedMotion.Value)) AnimationClock+=Time.unscaledDeltaTime;
        Render();
    }
    bool ShortcutPressed() {
        var selected=EventSystem.current?.currentSelectedGameObject;
        if(selected!=null && (selected.GetComponent<TMP_InputField>()?.isFocused==true || selected.GetComponent<InputField>()?.isFocused==true))return false;
        bool ctrl=Input.GetKey(KeyCode.LeftControl)||Input.GetKey(KeyCode.RightControl);
        bool alt=Input.GetKey(KeyCode.LeftAlt)||Input.GetKey(KeyCode.RightAlt);
        bool shift=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift);
        bool Pressed(string key)=>Enum.TryParse<KeyCode>(key,out var code) && Input.GetKeyDown(code) && MenuHotkey.Matches(ModEntry.Shortcut.Value,key,ctrl,alt,shift);
        return Pressed(MenuHotkey.Key(ModEntry.Shortcut.Value)) || Pressed("F10") || Pressed("F9");
    }
    void Render() {
        if(canvas==null) return;
        canvas.enabled=ModEntry.Show.Value && Bound;
        string track=State.trackId;
        if(track!=lastTrack) {current.text=next.text=outgoing.text="";lastKey="";lastTrack=track;}
        string a=State.current;
        string b=State.next;
        if(a.Length==0) a=State.title.Length>0?State.title+" — "+State.artist:"";
        string key=track+"|"+State.currentStartMs+"|"+a;
        if(key!=lastKey) {outgoing.text=current.text;current.text=a;next.text=b;transition=AnimationClock-(MotionActive?0:1);lastKey=key;}
        next.text=b;
        bool still=LyricMotion.IsStill(ModEntry.AnimationMode.Value,ModEntry.ReducedMotion.Value);
        if(!MotionActive || still)transition=AnimationClock-2;
        float t=PlaybackPresentation.Reveal(MotionActive,still,AnimationClock-transition,ModEntry.FadeMs.Value);
        float eased=t*t*(3-2*t), alpha=ModEntry.Opacity.Value;
        if(!still && ModEntry.AnimationMode.Value=="呼吸悬浮") alpha*=.9f+.1f*Mathf.Sin(AnimationClock*1.6f);
        var primary=LyricColor(ModEntry.Theme.Value);
        float y=ModEntry.Position.Value=="上方"?420:ModEntry.Position.Value=="中间"?0:-390;
        Style(current);Style(next);Style(outgoing);
        current.color=new Color(primary.r,primary.g,primary.b,alpha*eased);
        next.color=new Color(primary.r,primary.g,primary.b,ModEntry.TwoLines.Value?alpha*.58f:0);
        outgoing.color=new Color(primary.r,primary.g,primary.b,alpha*(1-eased));
        current.rectTransform.anchoredPosition=new Vector2(ModEntry.X.Value,y+ModEntry.Y.Value-8*(1-eased));
        float pop=still?1:1+Mathf.Sin(t*Mathf.PI)*.045f*ModEntry.Motion.Value;
        current.rectTransform.localScale=Vector3.one*pop;
        next.rectTransform.anchoredPosition=new Vector2(ModEntry.X.Value,y+ModEntry.Y.Value-Mathf.Max(30,current.fontSize*1.25f));
        outgoing.rectTransform.anchoredPosition=new Vector2(ModEntry.X.Value,y+ModEntry.Y.Value+14*eased);
    }
    void SaveSettings() {settingsDirty=true;saveAt=Time.unscaledTime+.4f;}
    void ClaimCassette() {
        var entry=EmporiumEntry.Instance;
        bool ready=entry!=null && entry.invElement!=null;
        int token=0;
        if(ready && !claimOperation.TryBegin(Time.unscaledTime,out token)) {ShowClaimMessage("请稍后再试，避免重复领取。");return;}
        try {
            var result=CassetteClaim.Give(ready,()=>ModHelper.SpawnItem(ModEntry.CassetteId),
                item=>{GraphUtils.TryAcceptAll(entry.invElement,item);},
                item=>item.parentInventory!=null || item.unitCount<=0,
                item=>item.Destroy(),
                error=>Debug.LogError("[DeepSpaceMusic] 领取磁带失败："+error));
            ShowClaimMessage(CassetteClaim.Message(result));
        } finally {if(ready)claimOperation.Complete(token,Time.unscaledTime);}
    }
    void ShowClaimMessage(string message) {
        diagnostic=message;diagnosticUntil=Time.unscaledTime+12;
        if(details!=null)details.text=message;
    }
    static string OffsetLabel()=>(ModEntry.Offset.Value>=0?"提前 ":"延后 ")+Math.Abs(ModEntry.Offset.Value)+" ms";
    void CloseWindows() {
        viewRevision++;
        var manager=CustomUIManager.Instance;
        if(manager!=null) {manager.CloseWindow(ModEntry.Id+".status");manager.CloseWindow(ModEntry.Id+".tools");manager.CloseWindow(ModEntry.Id+".picker");}
        panel=toolsPanel=pickerPanel=null;status=details=feedback=null;
    }
    public void Open() {
        CloseWindows();
        var builder=TerminalUI.Window(".status","音乐终端 / 字幕控制",820,740);
        builder.BeginPanel(padding:12,spacing:8);
        builder.AddLabel("当前歌曲", "status");status=builder.Last.text;
        builder.Last.SetPreferredSize(-1,76);status.maxVisibleLines=2;status.enableWordWrapping=false;status.overflowMode=TextOverflowModes.Ellipsis;
        builder.BeginRow(12).AddButton(MusicPlatforms.OpenLabel(ModEntry.Platform.Value),OpenSelectedClient);TerminalUI.Fill(builder.Last);
        builder.AddButton("连接与工具",OpenTools);TerminalUI.Fill(builder.Last);builder.End().End();
        builder.AddHeader("字幕样式");
        builder.BeginRow(24);
        builder.AddToggle("显示字幕",ModEntry.Show.Value,v=>{ModEntry.Show.Set(v);SaveSettings();});
        builder.AddToggle("双行字幕",ModEntry.TwoLines.Value,v=>{ModEntry.TwoLines.Set(v);SaveSettings();});builder.End();
        builder.BeginRow(12);TerminalUI.Label(builder,"字幕动效");
        TerminalUI.Dropdown(builder,ModEntry.Animations,Math.Max(0,Array.IndexOf(ModEntry.Animations,ModEntry.AnimationMode.Value)),i=>{ModEntry.AnimationMode.Set(ModEntry.Animations[i]);SaveSettings();});builder.End();
        builder.BeginRow(16).BeginColumn(6);
        TerminalUI.Column(builder);
        builder.AddLabel("字号");
        TerminalUI.Dropdown(builder,ModEntry.FontSizes,Mathf.Clamp(Mathf.RoundToInt(ModEntry.Size.Value/12)-1,0,5),i=>{ModEntry.Size.Set((i+1)*12);SaveSettings();});
        builder.End().BeginColumn(6).AddLabel("字体颜色");
        TerminalUI.Column(builder);
        TerminalUI.Dropdown(builder,ModEntry.Colors,Math.Max(0,Array.IndexOf(ModEntry.Colors,ModEntry.Theme.Value)),i=>{ModEntry.Theme.Set(ModEntry.Colors[i]);SaveSettings();});builder.End().End();
        builder.AddSeparator(verticalPadding:4).AddHeader("时间校准");
        int fadeIndex=0;
        for(int i=1;i<ModEntry.FadeDurations.Length;i++)if(Math.Abs(ModEntry.FadeDurations[i]-ModEntry.FadeMs.Value)<Math.Abs(ModEntry.FadeDurations[fadeIndex]-ModEntry.FadeMs.Value))fadeIndex=i;
        builder.BeginRow(12);TerminalUI.Label(builder,"浮现时长");
        TerminalUI.Dropdown(builder,ModEntry.FadeOptions,fadeIndex,i=>{ModEntry.FadeMs.Set(ModEntry.FadeDurations[i]);SaveSettings();});builder.End();
        builder.BeginRow(12);TerminalUI.Label(builder,OffsetLabel(),184);var offsetLabel=builder.Last.text;
        builder.AddButton("提前 50",()=>{ModEntry.Offset.Set(Math.Min(10000,ModEntry.Offset.Value+50));offsetLabel.text=OffsetLabel();SaveSettings();});TerminalUI.Fill(builder.Last);
        builder.AddButton("延后 50",()=>{ModEntry.Offset.Set(Math.Max(-10000,ModEntry.Offset.Value-50));offsetLabel.text=OffsetLabel();SaveSettings();});TerminalUI.Fill(builder.Last);
        builder.AddButton("归零",()=>{ModEntry.Offset.Set(0);offsetLabel.text=OffsetLabel();SaveSettings();});TerminalUI.Fill(builder.Last);builder.End();
        builder.AddLabel("切换即生效 / 自动保存 / 拖动字幕可定位");
        feedback=builder.Last.text;feedback.enableWordWrapping=false;feedback.overflowMode=TextOverflowModes.Ellipsis;
        panel=TerminalUI.Show(builder,pixelFont);
    }
    void OpenTools() {
        CloseWindows();
        var builder=TerminalUI.Window(".tools","音乐终端 / 连接与工具",760,800);
        builder.AddHeader("音乐来源");
        TerminalUI.Dropdown(builder,MusicPlatforms.Labels,MusicPlatforms.Index(ModEntry.Platform.Value),i=>{if(ModEntry.Platform.Value==MusicPlatforms.Choices[i])return;ModEntry.Platform.Set(MusicPlatforms.Choices[i]);SaveSettings();Send(i==0?"disconnect":"connect");});
        builder.BeginRow(12).AddButton("连接 / 重新检测",()=>Send("connect"));TerminalUI.Fill(builder.Last);
        builder.AddButton("断开",()=>Send("disconnect"));TerminalUI.Fill(builder.Last);builder.End();
        builder.AddToggle("自动重连",ModEntry.Reconnect.Value,v=>{ModEntry.Reconnect.Set(v);SaveSettings();});
        builder.BeginRow(12);TerminalUI.Label(builder,"设置快捷键");
        TerminalUI.Dropdown(builder,MenuHotkey.Choices,Math.Max(0,Array.IndexOf(MenuHotkey.Choices,ModEntry.Shortcut.Value)),i=>{ModEntry.Shortcut.Set(MenuHotkey.Choices[i]);SaveSettings();});builder.End();
        builder.AddSeparator(verticalPadding:4).AddHeader("工具");
        builder.AddButton("重新匹配歌词",()=>Send("reload"));TerminalUI.Fill(builder.Last);
        builder.AddButton("领取云音乐磁带",ClaimCassette);
        TerminalUI.Fill(builder.Last);
        builder.AddLabel("状态信息", "diagnostics");
        details=builder.Last.text;details.maxVisibleLines=5;details.overflowMode=TextOverflowModes.Ellipsis;builder.Last.SetPreferredSize(-1,160);
        builder.BeginRow(12).AddButton("返回字幕设置",Open);TerminalUI.Fill(builder.Last);
        builder.AddButton("恢复默认设置",()=>{ModEntry.Config.ResetToDefaults();SaveSettings();Send("disconnect");Open();});TerminalUI.Fill(builder.Last);builder.End();
        toolsPanel=TerminalUI.Show(builder,pixelFont);
        details.text=PlaybackPresentation.Details(State);
    }
    async void OpenSelectedClient() {
        string platform=ModEntry.Platform.Value;
        if(!MusicPlatforms.IsSupported(platform)) {OpenTools();return;}
        if(!clientOperation.TryBegin(Time.unscaledTime,out int token))return;
        int view=viewRevision;
        bool needsConnection=!ModEntry.ConnectionEnabled.Value || !State.connected || State.platform!=platform;
        ModEntry.ConnectionEnabled.Set(true);SaveSettings();
        // The pipe handles requests one at a time, but simultaneous connects can
        // arrive out of order. Finish selection before requesting a window.
        if(needsConnection) {
            commandOperation.Reset();
            epoch++;lastSettings="";diagnostic="";State=new Snapshot {platform=platform};
            int version=epoch;
            bool sent=await SendRaw(JsonUtility.ToJson(new Request {clientVersion=2,action="connect",platform=platform,autoReconnect=ModEntry.Reconnect.Value,offsetMs=ModEntry.Offset.Value}),"connect");
            if(!sent || version!=epoch || platform!=ModEntry.Platform.Value || stop.IsCancellationRequested || view!=viewRevision) {clientOperation.Complete(token,Time.unscaledTime);return;}
        }
        await SendRaw(JsonUtility.ToJson(new Request {clientVersion=2,action="openClient",platform=platform}),"openClient",token,view);
    }
    static Color LyricColor(string name) {
        string hex=name=="紫色"?"#D3B8FF":name=="白色"?"#F4F1E8":name=="暖黄"?"#FFE6A0":name=="粉色"?"#FFC2DC":name=="薄荷"?"#ACF4CD":"#6BEFFF";
        ColorUtility.TryParseHtmlString(hex,out var color);return color;
    }
    public void OpenMusicPicker() {
        CloseWindows();
        var builder=TerminalUI.Window(".picker","云音乐磁带 / 选歌",640,350);
        builder.AddHeader(MusicPlatforms.IsSupported(ModEntry.Platform.Value)?"在 "+MusicPlatforms.Name(ModEntry.Platform.Value)+" 中选择歌曲":"先选择你的音乐平台");
        builder.AddLabel("返回游戏后，歌名与歌词自动同步。");
        builder.AddSpace(12).AddButton(MusicPlatforms.OpenLabel(ModEntry.Platform.Value),OpenSelectedClient);TerminalUI.Fill(builder.Last,52);
        builder.AddSpace(12).AddLabel("点击上方按钮才会调出客户端。");
        pickerPanel=TerminalUI.Show(builder,pixelFont);
    }
    void Style(TextMeshProUGUI line) {
        float size=pixelFont!=null?Mathf.Max(12,Mathf.Round(ModEntry.Size.Value/12)*12)/canvas.scaleFactor:ModEntry.Size.Value;
        if(line.fontSize!=size) line.fontSize=size;
        line.rectTransform.sizeDelta=new Vector2(1300,Mathf.Max(65,size*1.6f));
        float outline=0.08f*ModEntry.Glow.Value;
        if(!line.fontSharedMaterial.HasProperty("_OutlineWidth")) return;
        if(Math.Abs(line.outlineWidth-outline)>.001f) {
            line.outlineWidth=outline;line.outlineColor=new Color(.04f,.06f,.15f,.9f);
            var mat=line.fontMaterial;
            if(mat.HasProperty("_GlowPower")) {mat.EnableKeyword("GLOW_ON");mat.SetFloat("_GlowPower",ModEntry.Glow.Value*.25f);mat.SetColor("_GlowColor",new Color(.25f,.85f,1,.25f));}
        }
    }
    void OnDestroy() {if(settingsDirty)ModEntry.Config.Save();stop.Cancel();CloseWindows();PixelFont.Release(pixelFont);Instance=null;stop.Dispose();}
}
