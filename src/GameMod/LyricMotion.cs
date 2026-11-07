using System;
namespace DeepSpaceMusic;

// No Unity dependencies: sample from absolute time, never from the previous mesh.
public static class LyricMotion {
    public const int FramesPerSecond=16;
    public const string StaticMode="静态";
    public static readonly string[] Modes={"像素轻跳","波浪流动","接力弹跳","呼吸悬浮","横向摇摆","螺旋漂浮","弹簧伸缩","阶梯律动","果冻摇摆","涟漪扩散",StaticMode};
    public static bool IsStill(string mode,bool reducedMotion)=>reducedMotion || mode==StaticMode;
    public struct Pose {
        public float X,Y,ScaleX,ScaleY,Angle;
        public Pose(float x,float y,float sx,float sy,float angle=0) {X=x;Y=y;ScaleX=sx;ScaleY=sy;Angle=angle;}
    }
    public static Pose Sample(string mode,float seconds,int index,int count,float amount) {
        if(mode==StaticMode)return new Pose(0,0,1,1);
        double t=seconds,i=index;
        float x=0,y=0,sx=1,sy=1,angle=0;
        switch(mode) {
            case "波浪流动":y=(float)Math.Sin(i*.65+t*4.4)*3.5f;break;
            case "接力弹跳":
                float hop=(float)Math.Pow(Math.Max(0,Math.Sin(t*4.4-i*.65)),4);
                y=hop*6;sx=sy=1+hop*.08f;break;
            case "呼吸悬浮":y=(float)Math.Sin(t*1.6)*3;break;
            case "横向摇摆":x=(float)Math.Sin(t*2.7)*5;y=(float)Math.Sin(t*2.7+i*.35)*1.5f;break;
            case "螺旋漂浮":x=(float)Math.Cos(t*2.6+i*.55)*3;y=(float)Math.Sin(t*2.6+i*.55)*4;break;
            case "弹簧伸缩":
                float spring=(float)Math.Sin(t*3.8+i*.18);
                sx=1-spring*.05f;sy=1+spring*.14f;y=spring*2;break;
            case "阶梯律动":
                y=(float)Math.Round(Math.Sin(t*3.2-i*.7)*2)*2;
                x=(float)Math.Round(Math.Sin(t*1.6+i*.3));break;
            case "果冻摇摆":
                float jelly=(float)Math.Sin(t*3.2-i*.4);
                angle=jelly*.12f;sx=1+jelly*.06f;sy=1-jelly*.06f;y=jelly*2;break;
            case "涟漪扩散":
                double distance=Math.Abs(i-(count-1)*.5);
                y=(float)Math.Sin(t*4-distance*.85)*4;
                x=(float)Math.Cos(t*4-distance*.85)*1.5f*(i<(count-1)*.5?-1:1);break;
            default:
                x=(float)Math.Sin(i*2.13+t*6.64);
                y=(float)Math.Cos(i*1.67+t*9.52)*1.5f;break;
        }
        return new Pose(x*amount,y*amount,1+(sx-1)*amount,1+(sy-1)*amount,angle*amount);
    }
}
