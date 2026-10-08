using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LWBridge.Desktop;

if(args.Length!=1 || File.Exists(args[0])) throw new InvalidOperationException("Use a fresh output path");
var rows = new List<object>();
foreach(bool releaseAfterNewStart in new[]{false,true})
{
 string root=Path.Combine(Path.GetTempPath(),"home009-r3-lead-"+Guid.NewGuid().ToString("N"));
 string gameRoot=Path.Combine(root,"selected"), runtime=Path.Combine(root,"runtime"), backups=Path.Combine(root,"backups");
 string game=Path.Combine(gameRoot,"Game","LastWar.exe"), creation="2026-10-08T11:00:00.0000000Z";
 Directory.CreateDirectory(Path.GetDirectoryName(game)!); File.WriteAllBytes(game,new byte[]{77,90});
 Directory.CreateDirectory(runtime); Directory.CreateDirectory(backups);
 string nonce=new('a',64), token=Convert.ToBase64String(Enumerable.Range(0,32).Select(x=>(byte)x).ToArray()).TrimEnd('=').Replace('+','-').Replace('/','_');
 var record=new OverviewAdoptionSnapshot("primary","old-session",nonce,4242,game,creation,OverviewLifecycleService.BridgeVersion,token,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),true);
 File.WriteAllBytes(Path.Combine(runtime,"adoption.json"),OverviewAdoptionRecord.Serialize(record));
 File.WriteAllBytes(Path.Combine(runtime,"recovery.json"),JsonSerializer.SerializeToUtf8Bytes(new {schemaVersion=1,profileId="primary",requestId="old-session",sessionId="old-session",stage="active_ready_deferred_restore",gamePid=4242,gamePath=game,gameStartedAtUtc=creation,backupPath=Path.Combine(backups,"owned"),originalFiles=new{}}));
 var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
 bool oldAlive=true, newAlive=false; int stage=0, starts=0, stops=0;
 OverviewHelperInvocation? current=null;
 var hooks=new OverviewLifecycleTestHooks {
  ProcessMatches=(pid,path,started)=>path==game && started==creation && ((pid==4242 && oldAlive)||(pid==50000 && newAlive)),
  SelectedGamePids=_=>oldAlive?new[]{4242}:Array.Empty<int>(),
  RunOfficialRecoverAsync=(_,_)=>Task.CompletedTask, RunOfficialSettleAsync=(_,_)=>Task.CompletedTask,
  DelayAsync=(_,ct)=>{if(stage==0){entered.TrySetResult();return release.Task;}return Task.Delay(1,ct);},
  ReadAllBytes=path=>{
   if(path.EndsWith("heartbeat.json")){
    if(current is null) return Encoding.UTF8.GetBytes("{\"schemaVersion\":1}");
    return JsonSerializer.SerializeToUtf8Bytes(new {schemaVersion=1,bridgeVersion=OverviewLifecycleService.BridgeVersion,profileId="primary",sessionId=current.SessionId,challenge=current.Challenge,gamePid=50000,updatedAt=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),ready=true,messageVisible=true,messageText=OverviewLifecycleService.ReadyMessage});
   }
   if(path.EndsWith("game-reported.txt") && current is not null) return Encoding.UTF8.GetBytes($"schema=1\nbridgeVersion={OverviewLifecycleService.BridgeVersion}\nsessionId={current.SessionId}\nchallenge={current.Challenge}\ngamePid=50000\ndeadlineMilliseconds={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+90000}\n");
   return File.ReadAllBytes(path);
  },
  RunHelperAsync=(inv,ct)=>{
   if(inv.Operation=="stop"){
    stops++; oldAlive=false; File.Delete(Path.Combine(runtime,"recovery.json"));
    return Task.FromResult(JsonSerializer.SerializeToElement(new{ok=true,mode="overview_exact_pid_close_restore",bridgeVersion=OverviewLifecycleService.BridgeVersion,profileId="primary",sessionId=inv.SessionId,gamePid=inv.GamePid,gamePath=inv.GamePath,gameStartedAtUtc=inv.GameStartedAtUtc,close=new{method="inert",accepted=true,processExited=true,alreadyExited=false},restore=new{restored=true},gameRunning=false,installedFilesChanged=false}));
   }
   starts++;current=inv;newAlive=true; long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
   return Task.FromResult(JsonSerializer.SerializeToElement(new{ok=true,mode="overview_install_launch_ready_deferred_restore",bridgeVersion=OverviewLifecycleService.BridgeVersion,profileId="primary",sessionId=inv.SessionId,challengeSha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inv.Challenge!))).ToLowerInvariant(),gamePid=50000,launcherPid=60000,gamePath=game,gameStartedAtUtc=creation,gameRunning=true,installedFilesChanged=true,restore=new{restored=false,deferred=true,stage="active_ready_deferred_restore"},ready=new{schemaVersion=1,bridgeVersion=OverviewLifecycleService.BridgeVersion,profileId="primary",sessionId=inv.SessionId,challenge=inv.Challenge,gamePid=50000,ready=true,messageVisible=true,messageText=OverviewLifecycleService.ReadyMessage,readyAt=now,updatedAt=now}}));
  }
 };
 try{
  using var host=new LWBridgeControlPipeHostState(@"\\.\pipe\lwbridge-r3-lead-inert",new LWBridgeControlPipeRegistry());
  using var life=new OverviewLifecycleService("primary",gameRoot,helperPath:Path.Combine(root,"missing.py"),requireCurrentClientEvidence:false,testHooks:hooks,startRecoveryMonitor:false,bridgeHostState:host,enableBridgeControlPipeLaunchBinding:true,runtimeRoot:runtime,backupRoot:backups,evidenceRoot:Path.Combine(root,"evidence"),applicationDataRoot:root);
  Task<object?> pending=life.InvokeAsync("profile_instances_reconcile",JsonSerializer.SerializeToElement(new{autoLaunchAll=false}),CancellationToken.None);
  await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
  await life.InvokeAsync("profile_instance_stop",JsonSerializer.SerializeToElement(new{profileId="primary",instanceId="old-session"}),CancellationToken.None);
  stage=1;
  if(!releaseAfterNewStart){release.TrySetResult();await pending;}
  await life.InvokeAsync("profile_instance_start",JsonSerializer.SerializeToElement(new{}),CancellationToken.None);
  var timer=typeof(OverviewLifecycleService).GetField("leaseTimer",BindingFlags.NonPublic|BindingFlags.Instance)!;
  bool timerBefore=timer.GetValue(life)!=null;
  if(releaseAfterNewStart){release.TrySetResult();await pending;}
  rows.Add(new{releaseAfterNewStart,starts,stops,newSessionRunning=life.RuntimeManaged,newSessionReady=life.IsReady,leaseTimerBefore=timerBefore,leaseTimerAfter=timer.GetValue(life)!=null,expectedLeaseTimerAfter=true,mismatch=timer.GetValue(life)==null});
 }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
}
var result=new{checkpoint="98b9d4f78825970590ccb57ef8e146f2d78f30ea",scope="actual reconcile/Stop/Start with controlled helper/process seams; no real game or OS termination",cases=rows,realGameLaunches=0};
File.WriteAllText(args[0],JsonSerializer.Serialize(result));Console.WriteLine(JsonSerializer.Serialize(result));
