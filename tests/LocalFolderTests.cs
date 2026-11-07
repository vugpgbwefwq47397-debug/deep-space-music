using DeepSpaceMusic;
static class LocalFolderTests {
 public static void Run(Action<bool,string> check) {
  check(MusicPlatforms.IsSupported("local") && MusicPlatforms.OpenLabel("local")=="选择音乐文件夹","local folder is a distinct source with an explicit picker action");
  check(VisualOpacity.Panel(0)==.2f && VisualOpacity.Panel(-1)==.2f && VisualOpacity.Panel(2)==1,"panel opacity never becomes invisible, including invalid stored values");
  check(VisualOpacity.Panel(float.NaN)==1 && VisualOpacity.Lyrics(float.PositiveInfinity)==.88f && VisualOpacity.Lyrics(0)==0,"opacity handles non-finite config and allows invisible lyrics independently");
  string root=Path.Combine(Path.GetTempPath(),"LocalMusicTests-"+Guid.NewGuid());Directory.CreateDirectory(root);
  try {
   foreach(string name in new[]{"b.MP3","a.wav","c.flac","secret.ncm","encrypted.kgg","picture.jpg"})File.WriteAllBytes(Path.Combine(root,name),[]);
   Directory.CreateDirectory(Path.Combine(root,"child"));File.WriteAllBytes(Path.Combine(root,"child","hidden.mp3"),[]);
   var files=LocalFolderPlayer.Catalog(root);
   check(files.Length==3 && Path.GetFileName(files[0])=="a.wav" && Path.GetFileName(files[1])=="b.MP3","local catalog sorts supported formats, excludes encrypted downloads and does not recurse");
   string audio=Path.Combine(root,"a.wav");File.WriteAllText(Path.ChangeExtension(audio,".lrc"),"[00:00.00]測試\n[00:01]第二句");
   var lines=LocalFolderPlayer.Sidecar(audio);check(lines.Count==2 && lines[0].Text=="测试" && Lrc.Find(lines,1000)==1,"same-basename LRC uses exact file identity, time axis and simplified display");
   check(LocalFolderPlayer.Sidecar(Path.Combine(root,"b.MP3")).Count==0,"missing sidecar does not borrow another track's lyrics");
   File.WriteAllBytes(Path.ChangeExtension(audio,".lrc"),new byte[2_000_001]);bool rejected=false;
   try{LocalFolderPlayer.Sidecar(audio);}catch(InvalidDataException){rejected=true;}check(rejected,"oversized local lyric file is rejected before loading");
  }finally{Directory.Delete(root,true);}
 }
}
