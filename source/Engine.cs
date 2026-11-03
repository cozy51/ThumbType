using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ThumbShiftPortable {
public sealed class Mapping {
 public string Normal, Left, Right, Shift;
 public Mapping(string n,string l,string r){Normal=n;Left=l;Right=r;}
}
public sealed class Layout {
 public readonly Dictionary<int,Mapping> Rows=new Dictionary<int,Mapping>();
 public bool HasPinky {get{foreach(Mapping m in Rows.Values)if(m.Shift!=null)return true;return false;}}
 public static Layout Parse(string text) {
  Layout result=new Layout(); int line=0;
  foreach(string raw in text.Replace("\r","").Split('\n')) {
   line++; string s=raw.Trim(); if(s.Length==0||s.StartsWith("#"))continue;
   string[] f=raw.Split('\t');
   if(f.Length!=4&&f.Length!=5)throw new FormatException("行 "+line+": タブ区切りの4列または5列が必要です。");
   Keys k; if(!Enum.TryParse<Keys>(f[0],true,out k)||!Enum.IsDefined(typeof(Keys),k)||!Allowed((int)k))throw new FormatException("行 "+line+": 未対応のキー名です。");
   if(result.Rows.ContainsKey((int)k))throw new FormatException("行 "+line+": キーが重複しています。");
   for(int i=1;i<f.Length;i++)if(f[i].Length>32||f[i].IndexOfAny(new char[]{'\0','\r','\n'})>=0)throw new FormatException("行 "+line+": 出力は32文字以内にしてください。");
   result.Rows.Add((int)k,new Mapping(f[1],f[2],f[3]){Shift=f.Length==5?f[4]:null});
  }
  if(result.Rows.Count==0)throw new FormatException("配列が空です。");return result;
 }
 public static bool Allowed(int k){return (k>=65&&k<=90)||(k>=48&&k<=57)||(k>=186&&k<=192)||(k>=219&&k<=222)||k==226;}
 public static readonly string Sample=YabLayout.Export(YabLayout.Parse(YabLayout.ReadBuiltIn()));
}
// Pure timing state machine: no operating-system side effects.
public sealed class Engine {
 public Layout Layout; public int Left=0x1D,Right=0x1C,Window=65;
 public bool LeftTap=true,RightTap=true,LeftHold=false,RightHold=false,Repeat=true,Direct=false,Pinky=false;
 public Action<string> Text; public Action<int> Tap;
 readonly HashSet<int> down=new HashSet<int>(); readonly Dictionary<int,string> outputs=new Dictionary<int,string>();
 int pending; long pendingTime; int thumb; long thumbTime; bool used; string last;
 public bool HasPending {get{return pending!=0;}}
 public void Reset(){down.Clear();outputs.Clear();pending=thumb=0;used=false;last=null;}
 void Output(string value){if(value=="{None}")return;if(value=="{Backspace}"){Tap(8);return;}if(value=="{Escape}"){Tap(27);return;}if(value.Length>0)Text(value);}
 void Emit(int key,int side){Mapping m=Layout.Rows[key];string v=side==-1?(m.Shift??m.Normal):side==Left?m.Left:side==Right?m.Right:m.Normal;if(v.Length==0)v=m.Normal;Output(v);outputs[key]=v;last=v;}
 public void Tick(long now){if(pending!=0&&now-pendingTime>Window){int k=pending;pending=0;Emit(k,0);}}
 public bool Handle(int key,bool isDown,long now){
  Tick(now);bool isThumb=!Direct&&(key==Left||key==Right);
  if(!isDown){
   if(!down.Remove(key))return false;outputs.Remove(key);
   if(key==pending){pending=0;Emit(key,0);}
   if(isThumb&&key==thumb){if(!used&&((key==Left&&LeftTap)||(key==Right&&RightTap)))Tap(key);thumb=0;used=false;}
   return true;
  }
  if(down.Contains(key)){string value;if(Repeat&&!isThumb&&outputs.TryGetValue(key,out value))Output(value);return true;}
  if(isThumb){
   down.Add(key);
   // Two thumbs cancel pending thumb taps; newest thumb selects the layer.
   thumb=key;thumbTime=now;used=false;
   if(pending!=0&&now-pendingTime<=Window){int k=pending;pending=0;used=true;Emit(k,key);}return true;
  }
  if(!Layout.Rows.ContainsKey(key)){if(pending!=0){int k=pending;pending=0;Emit(k,0);}return false;}
  down.Add(key);if(pending!=0){int k=pending;pending=0;Emit(k,0);}
  if(Pinky&&Layout.Rows[key].Shift!=null){Emit(key,-1);return true;}
  if(Direct){Emit(key,0);return true;}
  bool held=thumb==Left?LeftHold:RightHold;
  if(thumb!=0&&(now-thumbTime<=Window||held)){used=true;Emit(key,thumb);}else{pending=key;pendingTime=now;last=null;}return true;
 }
}
}
