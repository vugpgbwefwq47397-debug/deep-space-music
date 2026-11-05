using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
namespace DeepSpaceMusic;
public static class PixelFont {
    public static TMP_FontAsset Load() {
        var data=ModHelper.ReadModFileBytes(ModEntry.Id,"Fonts/fusion-pixel.bin");
        int width=BitConverter.ToInt32(data,0),height=BitConverter.ToInt32(data,4),count=BitConverter.ToInt32(data,8);
        if(count<1 || count>100000 || data.Length!=12+count*36) throw new InvalidOperationException("像素字体索引无效");
        var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
        if(!ImageConversion.LoadImage(texture,ModHelper.ReadModFileBytes(ModEntry.Id,"Fonts/fusion-pixel.png"))) throw new InvalidOperationException("像素字体图集读取失败");
        texture.name="Fusion Pixel 12px zh_Hans";texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Clamp;
        var original=FontLoader.Instance.simplifiedChineseFont;
        var font=ScriptableObject.CreateInstance<TMP_FontAsset>();font.name="Fusion Pixel 12px zh_Hans";
        // Use Unity's asset serialization API for TMP's serialized import settings.
        JsonUtility.FromJsonOverwrite("{\"m_Version\":\"1.1.0\",\"m_AtlasWidth\":"+width+",\"m_AtlasHeight\":"+height+",\"m_AtlasPadding\":0,\"m_AtlasRenderMode\":"+(int)GlyphRenderMode.RASTER+"}",font);
        font.atlasPopulationMode=AtlasPopulationMode.Static;font.atlasTextures=new[]{texture};
        font.faceInfo=new FaceInfo {familyName="Fusion Pixel 12px zh_Hans",styleName="Regular",pointSize=12,scale=1,lineHeight=16,ascentLine=13,capLine=9,meanLine=6,baseline=0,descentLine=-3};
        font.glyphTable.Clear();font.characterTable.Clear();
        uint index=1;
        for(int i=0;i<count;i++) {int p=12+i*36;int code=BitConverter.ToInt32(data,p),w=BitConverter.ToInt32(data,p+4),h=BitConverter.ToInt32(data,p+8),bx=BitConverter.ToInt32(data,p+12),by=BitConverter.ToInt32(data,p+16),advance=BitConverter.ToInt32(data,p+20),x=BitConverter.ToInt32(data,p+24),y=BitConverter.ToInt32(data,p+28);var glyph=new Glyph(index++,new GlyphMetrics(w,h,bx,by+h,advance),new GlyphRect(x,y,w,h),1,0);font.glyphTable.Add(glyph);font.characterTable.Add(new TMP_Character((uint)code,glyph));}
        font.fallbackFontAssetTable=new List<TMP_FontAsset>{original};
        var shader=Shader.Find("TextMeshPro/Mobile/Bitmap") ?? Shader.Find("UI/Default");if(shader==null)throw new InvalidOperationException("游戏未包含位图 UI shader");
        font.material=new Material(shader);font.material.mainTexture=texture;font.material.SetFloat("_TextureWidth",width);font.material.SetFloat("_TextureHeight",height);
        font.ReadFontAssetDefinition();return font;
    }
    public static void Release(TMP_FontAsset font) {if(font==null)return;UnityEngine.Object.Destroy(font.material);UnityEngine.Object.Destroy(font.atlasTextures[0]);UnityEngine.Object.Destroy(font);}
}
