using System;
namespace DeepSpaceMusic;
public static class MenuHotkey {
    public static readonly string[] Choices={"F9","F10","F1","F2","F3","F4","F5","F6","F7","F8","F11","F12","Ctrl+F9","Ctrl+F8","Ctrl+F7","Ctrl+F6"};
    public static string Normalize(string value)=>Array.IndexOf(Choices,value)>=0?value:"F9";
    public static string Key(string value)=>Normalize(value).Replace("Ctrl+","");
    public static bool Matches(string configured,string key,bool ctrl,bool alt,bool shift) {
        if(alt || shift)return false;
        // Always retain an accessible recovery shortcut and the old F10 shortcut.
        if(ctrl && key=="F9" || !ctrl && key=="F10")return true;
        string selected=Normalize(configured);
        return Key(selected)==key && selected.StartsWith("Ctrl+",StringComparison.Ordinal)==ctrl;
    }
}
