using System.Runtime.InteropServices;
namespace DeepSpaceMusic;

public static class LyricText {
    const uint SimplifiedChinese=0x02000000;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)]
    static extern int LCMapStringEx(string locale,uint flags,string source,int sourceLength,[Out,MarshalAs(UnmanagedType.LPArray,ArraySubType=UnmanagedType.U2,SizeParamIndex=5)] char[]? destination,int destinationLength,IntPtr version,IntPtr reserved,IntPtr sortHandle);

    public static string ToSimplified(string text) {
        if(string.IsNullOrEmpty(text)) return text;
        // Preserve Japanese lines; their kanji are not Traditional Chinese lyrics.
        if(text.Any(c=>c is >= '\u3040' and <= '\u30ff' or >= '\u31f0' and <= '\u31ff')) return text;
        int size=LCMapStringEx("zh-CN",SimplifiedChinese,text,text.Length,null,0,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        if(size<=0) return text;
        var result=new char[size];
        int written=LCMapStringEx("zh-CN",SimplifiedChinese,text,text.Length,result,size,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);
        // Windows maps 麼 to the variant 麽; lyrics should use the usual simplified 么.
        return written>0 && written<=size?new string(result,0,written).Replace('麽','么'):text;
    }
}
