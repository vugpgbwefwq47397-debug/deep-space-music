using DeepSpaceMusic;
int count=0;
void Check(bool value,string name) { if(!value) throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);count++; }
await AuditTests.Run(Check);
PlatformTests.Run(Check);
await OperationTests.Run(Check);
await FeedbackTests.Run(Check);
FollowupTests.Run(Check);
CassetteClaimTests.Run(Check);
KugouFeedbackTests.Run(Check);
LocalFolderTests.Run(Check);
var simplifiedSample=LyricText.ToSimplified("為什麼天這麼安靜，所有的雲都跑到我這裡");
Check(simplifiedSample == "为什么天这么安静，所有的云都跑到我这里","traditional lyric source displays Simplified Chinese: "+simplifiedSample);
Check(LyricText.ToSimplified("哪里有彩虹告诉我") == "哪里有彩虹告诉我" && LyricText.ToSimplified("")=="","simplified and empty lyrics remain unchanged");
Check(LyricText.ToSimplified("Hello, 世界! café 🎵") == "Hello, 世界! café 🎵" && LyricText.ToSimplified("君の聲を聴く") == "君の聲を聴く","foreign lyrics, punctuation and surrogate pairs are preserved");
var l=Lrc.Parse("[offset:-100]\n[00:01.2][00:03.45]第一句\n[00:02.000]第二句\n[ar:test]\ninvalid\n[99:99]bad");
Check(l.Count==3,"multi timestamps, metadata and malformed lines");
Check(l[0].StartMs==1100 && l[1].StartMs==1900 && l[2].StartMs==3350,"fraction precision, sort and LRC offset");
Check(Lrc.Find(l,1099)==-1 && Lrc.Find(l,1100)==0,"first line boundary");
Check(Lrc.Find(l,4000)==2 && Lrc.Find(l,1500)==0,"seek backwards and forwards");
Check(Lrc.Find([],1000)==-1,"empty lyrics");
Check(Lrc.Parse("[00:01]a\n[00:01]b")[0].Text=="a / b","same timestamp bilingual merge");
Check(Sessions.Matches("qq","QQMusic.exe") && !Sessions.Matches("qq","cloudmusic.exe"),"strict selected platform");
Check(!Sessions.Matches("qq","FakeQQMusic.exe") && !Sessions.Matches("netease","my-cloudmusic.exe"),"reject substring identities");
var temp=Path.Combine(Path.GetTempPath(),"DeepSpaceMusicTests-"+Guid.NewGuid());Directory.CreateDirectory(temp);
try {
 File.WriteAllText(Path.Combine(temp,"a.json"),"{\"Title\":\"Song\",\"Artist\":\"Artist\",\"DurationMs\":10000}");
 File.WriteAllText(Path.Combine(temp,"a.lrc"),"[00:01]original test text");var p=new LocalLyricsProvider(temp);
 Check(p.Get("Song","Artist",10000).Count==1,"exact local metadata match");
 Check(p.Get("Song","Other",10000).Count==0 && p.Get("Song","Artist",20000).Count==0,"reject wrong artist/version");
 Check(p.Get("Song","Artist",0).Count==0,"unknown duration cannot match constrained recording");
} finally {Directory.Delete(temp,true);}
int reads=0;string stamp="first";
var songs=Enumerable.Range(0,21).Select(i=>new QueueTrack {title="Song "+i,artist="Artist",catalogId=(100+i).ToString()}).ToArray();
songs[1]=songs[0]; // Duplicates are real queue positions, not records to discard.
var queue=new ClientQueue(()=>{reads++;return Task.FromResult(songs);},()=>stamp);var snapshot=new Snapshot();
async Task Ready(ClientQueue q,Snapshot s) {for(int i=0;i<200;i++) {q.Apply(s);if(s.queueState!="loading")return;await Task.Delay(5);}throw new Exception("queue test timed out");}
queue.Request("qq",0,true);await Ready(queue,snapshot);
Check(snapshot.queueState=="ready" && snapshot.queue.Length==10 && snapshot.queueTotal==21 && snapshot.queue[1].title==snapshot.queue[0].title,"complete queue snapshot, ten rows, duplicate positions preserved");
Check(snapshot.queueText.Split('\n').Length==10 && snapshot.queueText.StartsWith("1. Song 0"),"flat text page survives clients without nested array support");
ClientQueue.AdaptForClient(snapshot,0);
Check(snapshot.queueState=="ready-text" && snapshot.queueError==snapshot.queueText,"legacy in-game session receives compatible text page");
queue.Request("qq",1,false);queue.Apply(snapshot);
Check(reads==1 && snapshot.queue[0].title=="Song 10","cached next page without rereading client");
queue.Request("qq",2,false);queue.Apply(snapshot);
Check(snapshot.queue.Length==1 && snapshot.queue[0].title=="Song 20","partial final page");
stamp="changed";queue.Request("qq",1,false);await Ready(queue,snapshot);
Check(reads==2 && snapshot.queuePage==1,"database change invalidates cached queue");
queue.Request("qq",9999,false);queue.Apply(snapshot);
Check(snapshot.queuePage==2,"queue shrink or excessive page clamps to last page");
Check(queue.Select(10,snapshot.queueVersion).catalogId=="110","clicked position resolves to verified catalog ID");
bool stale=false;try {queue.Select(10,snapshot.queueVersion-1);}catch(InvalidOperationException){stale=true;}Check(stale,"stale queue button cannot select a different snapshot");
Check(AutomaticLyrics.Matches("Song","Artist",200000,"Song","Artist",201) && !AutomaticLyrics.Matches("Song","Artist",200000,"Song","Other",200) && !AutomaticLyrics.Matches("Song","Artist",200000,"Song","Artist",210),"online lyrics require exact artist/title and matching recording duration");
bool replaceBlocked=false;try{TrackDispatch.EnsurePreservesQueue(1200);}catch(InvalidOperationException){replaceBlocked=true;}Check(replaceBlocked,"unverified dispatch cannot replace a multi-track client queue");
var selectedSong=new QueueTrack {title="Same",artist="Artist",catalogId="123",durationMs=200000};
var playingSong=new Snapshot {connected=true,platform="qq",title="Same",artist="Artist",durationMs=200500};
TrackDispatch.EnsureCurrent(selectedSong,1,playingSong);Check(true,"single verified current song may restart through SMTC");
bool wrongRecording=false;playingSong.durationMs=240000;try{TrackDispatch.EnsureCurrent(selectedSong,1,playingSong);}catch(InvalidOperationException){wrongRecording=true;}Check(wrongRecording,"same title and artist with a different duration cannot be selected");
bool disconnected=false;playingSong.durationMs=200000;playingSong.connected=false;try{TrackDispatch.EnsureCurrent(selectedSong,1,playingSong);}catch(InvalidOperationException){disconnected=true;}Check(disconnected,"disconnected metadata cannot authorize restarting a queue item");
var batch=QueueBatchCommand.Create(songs,"C:\\QQMusic.exe");
Check(batch.Tracks[0].title=="Song 0" && batch.Tracks.Length==songs.Length && batch.Tracks[10].title=="Song 10","batch plan keeps original order and every queue position");
Check(batch.Argument.StartsWith("cmd_count==1&&id_0==100,100,102,") && batch.Argument.Split("&&songtype_0==")[1].Split(',').Length==songs.Length,"batch command uses one group with a song type for every ID");
string oldTitle=batch.Tracks[10].title;songs[10].title="Changed";Check(batch.Tracks[10].title==oldTitle,"batch plan owns a copy of track values");songs[10].title="Song 10";
bool invalidId=false;try{QueueBatchCommand.Create([new QueueTrack {title="bad",artist="a",catalogId="1,2"}],"QQMusic.exe");}catch(InvalidDataException){invalidId=true;}Check(invalidId,"batch plan rejects injected list syntax before dispatch");
bool longQueue=false;try{QueueBatchCommand.Create(Enumerable.Repeat(new QueueTrack {title="Song",artist="Artist",catalogId="4294967295"},3000).ToArray(),"QQMusic.exe");}catch(InvalidDataException){longQueue=true;}Check(longQueue,"oversized Windows command line is rejected before dispatch");
queue.Request("netease",1,false);queue.Apply(snapshot);
Check(snapshot.queueError.Contains("网易云音乐") && snapshot.queuePage==1,"unsupported background source returns immediately per platform");
bool badPage=false;try {queue.Request("qq",-1,false);} catch(InvalidDataException) {badPage=true;}
Check(badPage,"reject invalid queue page");
queue.Clear();queue.Apply(snapshot);
Check(snapshot.queuePage==0 && snapshot.queue.Length==0,"disconnect clears queue state");
bool malformed=false;try {QqQueueReader.Validate(new QueueReadResult {ok=true,total=22,tracks=songs});} catch(InvalidDataException) {malformed=true;}
Check(malformed,"reject incomplete helper snapshot");
Check(QqQueueReader.Validate(new QueueReadResult {ok=true,total=0,tracks=[]}).Length==0,"empty client queue is a successful snapshot");
var pending=new TaskCompletionSource<QueueTrack[]>();var inFlight=new ClientQueue(()=>pending.Task,()=>"same");
inFlight.Request("qq",0,true);await Task.Delay(10);inFlight.Clear();pending.SetResult(songs);await Task.Delay(20);inFlight.Apply(snapshot);
Check(snapshot.queueState=="unavailable" && snapshot.queueTotal==0,"late background response cannot repopulate disconnected queue");
// Static mode must suppress both mesh motion and the separate entrance transition.
bool staticIdentity=true;
foreach(float seconds in new[]{0f,.15f,2f,20f})
foreach(int index in new[]{0,5,11})
foreach(float strength in new[]{0f,1f,2f}) {
 var pose=LyricMotion.Sample(LyricMotion.StaticMode,seconds,index,12,strength);
 staticIdentity&=pose.X==0 && pose.Y==0 && pose.ScaleX==1 && pose.ScaleY==1 && pose.Angle==0;
}
Check(staticIdentity,"static lyrics preserve original geometry across time, characters and animation strengths");
Check(LyricMotion.IsStill(LyricMotion.StaticMode,false) && LyricMotion.IsStill("呼吸悬浮",true) && !LyricMotion.IsStill("呼吸悬浮",false),"static selection and legacy reduced-motion setting stop the same rendering paths");
Check(PlaybackPresentation.Reveal(true,LyricMotion.IsStill(LyricMotion.StaticMode,false),0,1000)==1,"static lyrics appear immediately even with a configured entrance duration");
// Every animated mode must keep visibly moving after its entrance, including at 12 px.
foreach(var mode in LyricMotion.Modes) {
 if(mode==LyricMotion.StaticMode)continue;
 var frames=new HashSet<string>();bool bounded=true;
 for(int tick=32;tick<96;tick++) {
  var frame=new System.Text.StringBuilder();
  for(int i=0;i<12;i++) {
   var pose=LyricMotion.Sample(mode,tick/(float)LyricMotion.FramesPerSecond,i,12,1);
   foreach(float v in new[]{pose.X,pose.Y,pose.ScaleX,pose.ScaleY,pose.Angle})bounded&=float.IsFinite(v);
   bounded&=Math.Abs(pose.X)<=8 && Math.Abs(pose.Y)<=8 && pose.ScaleX>.5 && pose.ScaleX<1.5 && pose.ScaleY>.5 && pose.ScaleY<1.5;
   // Quantize actual corners as the renderer does, so subpixel-only motion fails.
   foreach(var corner in new[]{(-3f,-6f),(-3f,6f),(3f,6f),(3f,-6f)}) {
    double x=corner.Item1*pose.ScaleX,y=corner.Item2*pose.ScaleY;
    frame.Append(Math.Round(x*Math.Cos(pose.Angle)-y*Math.Sin(pose.Angle)+pose.X*.75)).Append(',');
    frame.Append(Math.Round(x*Math.Sin(pose.Angle)+y*Math.Cos(pose.Angle)+pose.Y*.75)).Append(';');
   }
  }
  frames.Add(frame.ToString());
 }
 Check(bounded && frames.Count>=4,mode+": held lyrics animate visibly beyond entrance, with bounded geometry");
 var stopped=LyricMotion.Sample(mode,4,3,12,0);
 Check(stopped.X==0 && stopped.Y==0 && stopped.ScaleX==1 && stopped.ScaleY==1 && stopped.Angle==0,mode+": zero strength restores original geometry");
}
Console.WriteLine($"{count} tests passed");
