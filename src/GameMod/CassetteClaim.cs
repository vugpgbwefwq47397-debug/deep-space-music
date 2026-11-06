using System;
namespace DeepSpaceMusic;

public enum CassetteClaimResult { NotInGame, SpawnFailed, Full, Claimed, Failed, CleanupFailed }
public static class CassetteClaim {
    public static CassetteClaimResult Give<T>(bool ready,Func<T> spawn,Action<T> place,Func<T,bool> stored,Action<T> discard,Action<Exception> report) where T:class {
        if(!ready)return CassetteClaimResult.NotInGame;
        T item=null;CassetteClaimResult result;
        try {
            item=spawn();
            if(item==null)return CassetteClaimResult.SpawnFailed;
            place(item);
            if(stored(item))return CassetteClaimResult.Claimed;
            result=CassetteClaimResult.Full;
        } catch(Exception error) {report(error);result=CassetteClaimResult.Failed;}
        if(item!=null)try {
            // A placement callback can fail after moving the item. Never destroy
            // a cassette that already belongs to an inventory.
            if(!stored(item))discard(item);
        } catch(Exception error) {report(error);result=CassetteClaimResult.CleanupFailed;}
        return result;
    }
    public static string Message(CassetteClaimResult result) {
        switch(result) {
            case CassetteClaimResult.NotInGame:return "请先载入存档进入当铺，再领取磁带。连接音乐后仍需进入存档。";
            case CassetteClaimResult.SpawnFailed:return "磁带尚未初始化，请载入存档后重试；若仍失败，请在官方模组菜单重新启用并重启游戏。";
            case CassetteClaimResult.Full:return "下方背包没有足够空位，未领取磁带。请腾出空间后再试。";
            case CassetteClaimResult.Claimed:return "云音乐磁带已放入下方背包，请拖入原版磁带机的「磁带」栏。";
            default:return "领取磁带时发生错误。请先检查下方背包，避免重复领取；详情见游戏日志。";
        }
    }
}
