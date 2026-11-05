using System;
using System.Collections.Generic;
using HarmonyLib;
namespace DeepSpaceMusic;
// Bound to inspected build 26337366. Game loader applies attributed patches; no global unpatch.
static class Deck {
    public static GameItem Owner;
    public static bool Active(GameItem machine)=>Owner==machine && Terminal.Instance!=null && Terminal.Instance.Bound;
    public static bool IsCloud(GameItem machine)=>machine.GetTagReadonly("CURRENT_PLAYING_TRACK_ID_TAG").GetString()==ModEntry.Track;
    public static void Bind(GameItem machine) {if(Owner!=machine) UnityEngine.Debug.Log("[DeepSpaceMusic] Bound to original cassette player");Owner=machine;if(Terminal.Instance!=null) Terminal.Instance.Bound=true;}
    public static void Release(bool pause) {if(Terminal.Instance!=null) {Terminal.Instance.Bound=false;if(pause) Terminal.Instance.Send("command","pause",urgent:true);}Owner=null;}
    public static void Command(string command,bool urgent=false) {Terminal.Instance?.Send("command",command,urgent);}
    public static void State(GameItem machine,int value) {if(machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt()!=value) machine.ModifyTag("CASETTE_PLAYER_STATE_INT",t=>t.SetInt(value));}
}
[HarmonyPatch(typeof(MachineCassettePlayer),"PlayTrackAtIndex")]
static class SelectTrack {
    static bool Prefix(GameItem machine,List<string> trackList,int index) {
        if(index<0 || index>=trackList.Count) return true;
        if(trackList[index]!=ModEntry.Track) {if(Deck.Active(machine)) Deck.Release(ModEntry.PauseEject.Value);return true;}
        AudioManager.StopAudioCoroutine();AudioManager.Instance.StopMusic();
        Deck.Bind(machine);
        machine.ModifyTag("CURRENT_PLAYING_CASSETTE_INDEX_TAG",t=>t.SetInt(index));
        machine.ModifyTag("CURRENT_PLAYING_TRACK_ID_TAG",t=>t.SetString(ModEntry.Track));
        Deck.Command("play");return false;
    }
}
[HarmonyPatch(typeof(MachineCassettePlayer),"OnPlayButtonPressed")]
static class Play {
    static bool Prefix(GameItem machine) {
        if(!Deck.Active(machine) || !Deck.IsCloud(machine)) return true;
        Deck.Command(Terminal.Instance.State.playback=="playing"?"pause":"play");return false;
    }
}
[HarmonyPatch(typeof(MachineCassettePlayer),"OnStopButtonPressed")]
static class Stop {
    static bool Prefix(GameItem machine) {if(!Deck.Active(machine))return true;Deck.Command("pause",urgent:true);Deck.State(machine,0);Deck.Release(false);return false;}
}
[HarmonyPatch(typeof(MachineCassettePlayer),"OnNextButtonPressed")]
static class Next {
    static bool Prefix(GameItem machine) {if(!Deck.Active(machine))return true;Deck.Command("next");return false;}
}
[HarmonyPatch(typeof(MachineCassettePlayer),"OnPreviousButtonPressed")]
static class Previous {
    static bool Prefix(GameItem machine) {if(!Deck.Active(machine))return true;Deck.Command("previous");return false;}
}
[HarmonyPatch(typeof(MachineCassettePlayer),"OnCassetteInventoryChanged")]
static class InventoryChanged {
    static void Prefix(GameItem machine,GameGridInventory cassetteInventory) {
        var tracks=MachineCassettePlayer.GetTrackListFromCassette(cassetteInventory);
        if(Deck.Active(machine) && !tracks.Contains(ModEntry.Track)) {Deck.Release(ModEntry.PauseEject.Value);Deck.State(machine,0);}
        if(!Deck.Active(machine) && tracks.Count>0 && tracks[0]==ModEntry.Track && machine.GetTagReadonly("CASETTE_PLAYER_STATE_INT").GetInt()==0) {
            Deck.Bind(machine);machine.ModifyTag("CURRENT_PLAYING_TRACK_ID_TAG",t=>t.SetString(ModEntry.Track));
        }
    }
}
[HarmonyPatch(typeof(MachineCassettePlayer),"OnMachineLoaded")]
static class Loaded {
    static bool Prefix(GameItem machine,GameGridInventory cassetteInventory) {
        if(!Deck.IsCloud(machine))return true;
        Deck.State(machine,0);
        if(MachineCassettePlayer.GetTrackListFromCassette(cassetteInventory).Contains(ModEntry.Track)) Deck.Bind(machine);
        return false;
    }
}
[HarmonyPatch(typeof(MachineCassettePlayer),"PauseForGameOver")]
static class GameOver {static void Prefix() {if(Deck.Owner!=null) Deck.Release(true);}}
