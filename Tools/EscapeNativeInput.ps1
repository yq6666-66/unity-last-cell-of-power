param(
 [Parameter(Mandatory=$true)][int]$TargetProcessId,
 [Parameter(Mandatory=$true)][ValidateSet('W','A','S','D','E','Up','Down','Left','Right','F1','Escape','R','W+D','W+A','Click','Mouse')][string]$Action,
 [ValidateRange(20,12000)][int]$Milliseconds=200,
 [ValidateRange(-1200,1200)][int]$MouseX=0,
 [ValidateRange(-1200,1200)][int]$MouseY=0
)
$ErrorActionPreference='Stop'
# Execute only after explicit user approval for native scan-code input.
$expected='C:\Users\admin\Desktop\game create\Builds\EscapeSample-20261111-231429\LastCellOfPower.exe'
if($Action -ne 'E' -and $Milliseconds -gt 3000){throw 'Only E interaction may be held longer than 3 seconds'}
$target=Get-Process -Id $TargetProcessId
if($target.Path -ine $expected){throw 'Target executable is not the approved StealthSample build'}
Add-Type -TypeDefinition @'
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
public static class EscapeNativeInput {
 [StructLayout(LayoutKind.Sequential)] struct KI { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }
 [StructLayout(LayoutKind.Sequential)] struct MI { public int dx,dy; public uint data,flags,time; public UIntPtr extra; }
 [StructLayout(LayoutKind.Explicit)] struct U { [FieldOffset(0)]public KI key; [FieldOffset(0)]public MI mouse; }
 [StructLayout(LayoutKind.Sequential)] struct I { public uint type; public U data; }
 [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,I[] input,int size);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 static void Send(I i) { if(SendInput(1,new[]{i},Marshal.SizeOf(typeof(I)))!=1)throw new Exception("SendInput failed: "+Marshal.GetLastWin32Error()); }
 static void Key(ushort scan,bool extended,bool up) { var i=new I();i.type=1;i.data.key.scan=scan;i.data.key.flags=8u|(extended?1u:0u)|(up?2u:0u);Send(i); }
 static void Mouse(int x,int y,uint flags){var i=new I();i.type=0;i.data.mouse.dx=x;i.data.mouse.dy=y;i.data.mouse.flags=flags;Send(i);}
 public static string Run(Process process,string action,int ms,int mx,int my) {
  var expectedWindow=process.MainWindowHandle;
  Action requireFocus=()=>{process.Refresh();if(process.HasExited || expectedWindow==IntPtr.Zero || process.MainWindowHandle!=expectedWindow || GetForegroundWindow()!=expectedWindow)throw new Exception("Input stopped: game is no longer foreground");};
  requireFocus();
  var scans=new Dictionary<string,ushort>{{"E",0x12},{"W",0x11},{"A",0x1e},{"S",0x1f},{"D",0x20},{"F1",0x3b},{"Escape",1},{"R",0x13},{"Up",0x48},{"Down",0x50},{"Left",0x4b},{"Right",0x4d}};
  var held=new List<string>();bool mouseDown=false;var watch=Stopwatch.StartNew();
  try {
   if(action=="Mouse"){Mouse(mx,my,1);return "relative mouse sent";}
   if(action=="Click"){Mouse(0,0,2);mouseDown=true;}
   else foreach(var key in action.Split('+')) {requireFocus();Key(scans[key],key=="Up"||key=="Down"||key=="Left"||key=="Right",false);held.Add(key);}
   while(watch.ElapsedMilliseconds<ms){requireFocus();Thread.Sleep(10);}
   return "completed elapsedMs="+watch.ElapsedMilliseconds;
  }finally{
   foreach(var key in held)Key(scans[key],key=="Up"||key=="Down"||key=="Left"||key=="Right",true);
   if(mouseDown)Mouse(0,0,4);
  }
 }
}
'@
$outcome=[EscapeNativeInput]::Run($target,$Action,$Milliseconds,$MouseX,$MouseY)
[pscustomobject]@{Utc=[DateTime]::UtcNow.ToString('o');TargetProcessId=$TargetProcessId;Action=$Action;Milliseconds=$Milliseconds;MouseX=$MouseX;MouseY=$MouseY;Outcome=$outcome}|ConvertTo-Json -Compress
