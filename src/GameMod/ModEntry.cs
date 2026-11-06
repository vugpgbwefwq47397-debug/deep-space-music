using System;
using UnityEngine;
namespace DeepSpaceMusic;
public class ModEntry : IMod {
    public const string Id="deep_space_music", CassetteId=Id+".cassette", Track=Id+".external";
    public static ModConfig Config;
    public static ConfigEntry<string> Platform, Theme, Position, AnimationMode, Shortcut;
    public static readonly string[] Animations=LyricMotion.Modes;
    public static readonly string[] Colors={"青蓝","紫色","白色","暖黄","粉色","薄荷"};
    public static readonly string[] FontSizes={"12 px","24 px","36 px","48 px","60 px","72 px"};
    public static readonly int[] FadeDurations={0,50,100,150,200,300,500,750,1000};
    public static readonly string[] FadeOptions={"立即显示 · 0 ms","50 ms","100 ms","150 ms","200 ms","300 ms","500 ms","750 ms","1000 ms"};
    public static ConfigEntry<bool> Reconnect, ConnectionEnabled, PauseEject, Show, TwoLines, ReducedMotion, Draggable;
    public static ConfigEntry<float> Size, Opacity, Glow, X, Y, Motion;
    public static ConfigEntry<int> Offset, FadeMs;
    GameObject root;
    public void Init(ModManifest manifest) {
        Config=ModHelper.GetConfig(Id);
        Platform=Config.Define("platform","未选择","音乐平台连接",()=>"平台",choices:MusicPlatforms.Choices);
        Shortcut=Config.Define("menuHotkey","F9","操作",()=>"打开设置快捷键（F10 / Ctrl+F9 保留）",choices:MenuHotkey.Choices);
        if(Shortcut.Value!=MenuHotkey.Normalize(Shortcut.Value))Shortcut.Set("F9");
        Reconnect=Config.Define("autoReconnect",true,"音乐平台连接",()=>"自动重连");
        ConnectionEnabled=Config.Define("connectionEnabled",true,"音乐平台连接",()=>"允许启动时连接",()=>"由 F10 连接/断开按钮记录。关闭后游戏启动时保持断开。",requiresRestart:true);
        PauseEject=Config.Define("pauseOnEject",true,"磁带机",()=>"取出时暂停外部音乐");
        Show=Config.Define("lyrics",true,"霓虹悬浮歌词",()=>"显示歌词");
        TwoLines=Config.Define("twoLines",true,"霓虹悬浮歌词",()=>"双行显示");
        ReducedMotion=Config.Define("reducedMotion",false,"霓虹悬浮歌词",()=>"减少动画");
        Draggable=Config.Define("draggable",true,"霓虹悬浮歌词",()=>"允许拖动歌词");
        AnimationMode=Config.Define("animationMode","像素轻跳","霓虹悬浮歌词",()=>"歌词动效",choices:Animations);
        if(Array.IndexOf(Animations,AnimationMode.Value)<0) AnimationMode.Set(Animations[0]);
        Motion=Config.Define("motion",1f,"霓虹悬浮歌词",()=>"动效强度",0f,2f);
        Size=Config.Define("fontSize",24f,"霓虹悬浮歌词",()=>"字体大小",12f,72f);
        FadeMs=Config.Define("fadeDurationMs",150,"霓虹悬浮歌词",()=>"浮现时长 ms（0 为立即）",0,1000);
        Opacity=Config.Define("opacity",0.88f,"霓虹悬浮歌词",()=>"文字透明度",0.2f,1f);
        Glow=Config.Define("glow",0.3f,"霓虹悬浮歌词",()=>"发光描边强度",0f,1f);
        Theme=Config.Define("theme","青蓝","霓虹悬浮歌词",()=>"字体颜色",choices:Colors);
        Position=Config.Define("position","下方","霓虹悬浮歌词",()=>"位置",choices:new[]{"下方","中间","上方"});
        X=Config.Define("x",0f,"霓虹悬浮歌词",()=>"水平偏移",-900f,900f);
        Y=Config.Define("y",0f,"霓虹悬浮歌词",()=>"垂直偏移",-900f,900f);
        Offset=Config.Define("offset",100,"霓虹悬浮歌词",()=>"歌词时间偏移 ms",-10000,10000);
        new ModLog(manifest).Log("初始化完成；桥接需独立启动。F9 / F10 打开连接状态，快捷键可修改。目标 Steam build 26337366。");
    }
    public void OnEnable() {
        ModHook.OnModItemDirectoryInit+=Register;
        root=new GameObject("Deep Space Music Terminal");UnityEngine.Object.DontDestroyOnLoad(root);root.AddComponent<Terminal>();
        ModHelper.AddDebugFunction(Id,ModHelper.DEBUG_CATEGORY_SPECIAL,"Music Terminal",()=>Terminal.Instance.Open());
    }
    void Register(ModItemDirectory dir) {
        dir.Add(CassetteId,()=>{
            var item=ItemDirectory.CreateEmptyItem(CassetteId);
            item.name="云音乐磁带 / Cloud Music Cassette";
            item.shortDescription="连接已选择的桌面音乐客户端。原版磁带机播放/暂停、上下首、停止控制。";
            item.SetSpriteAndShape("Items/items_amenities","cassette1");
            CassetteHelper.InitCassetteItem(item,Track);item.SetGameItemType("ART");ModHelper.SetItemOwnerShip(item,true);
            item.mayActivateSlotItemFunc=(i,v,s)=>true;item.canActivateSlotItemFunc=(i,v,s)=>true;
            item.onActivateSlotItemFunc+=(i,v,s)=>Terminal.Instance?.OpenMusicPicker();
            AudioHelper.InitCassette(item);return item;
        });
    }
    public void OnDisable() {
        ModHook.OnModItemDirectoryInit-=Register;ModHelper.RemoveDebugFunctions(Id);
        Deck.Release(false);
        if(root!=null) UnityEngine.Object.Destroy(root);
    }
}
