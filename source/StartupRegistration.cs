using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ThumbShiftPortable {
// Per-user Startup-folder shortcut. Constructing/reading never registers anything.
public sealed class StartupRegistration {
 const string Marker="ThumbType startup (managed by ThumbType)";
 readonly string folder,executable;
 public string LinkPath {get{return Path.Combine(folder,"ThumbType.lnk");}}
 public StartupRegistration(string target,string startupFolder){executable=Path.GetFullPath(target);folder=Path.GetFullPath(startupFolder);}
 static object Get(object obj,string name){return obj.GetType().InvokeMember(name,BindingFlags.GetProperty,null,obj,null);}
 static void Set(object obj,string name,object value){obj.GetType().InvokeMember(name,BindingFlags.SetProperty,null,obj,new object[]{value});}
 static object Call(object obj,string name,params object[] args){return obj.GetType().InvokeMember(name,BindingFlags.InvokeMethod,null,obj,args);}
 static void Release(object obj){if(obj!=null&&Marshal.IsComObject(obj))Marshal.FinalReleaseComObject(obj);}
 public string ReadTarget(){if(!File.Exists(LinkPath))return null;object shell=null,link=null;try{
   shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));link=Call(shell,"CreateShortcut",LinkPath);
   if((string)Get(link,"Description")!=Marker)throw new IOException("同名のショートカットが既にあります。ThumbTypeが作成したものではないため変更しません。\n"+LinkPath);
   return (string)Get(link,"TargetPath");
  }finally{Release(link);Release(shell);}}
 public bool IsCurrent(string target){return !string.IsNullOrEmpty(target)&&string.Equals(Path.GetFullPath(target),executable,StringComparison.OrdinalIgnoreCase);}
 public void SetEnabled(bool enabled){string previous=ReadTarget();if(!enabled){if(previous!=null)File.Delete(LinkPath);return;}
  if(!File.Exists(executable))throw new FileNotFoundException("起動するexeが見つかりません。",executable);
  Directory.CreateDirectory(folder);string temporary=Path.Combine(folder,"ThumbType-"+Guid.NewGuid().ToString("N")+".lnk");object shell=null,link=null;
  try{shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));link=Call(shell,"CreateShortcut",temporary);
   Set(link,"TargetPath",executable);Set(link,"WorkingDirectory",Path.GetDirectoryName(executable));Set(link,"Arguments","");Set(link,"Description",Marker);Set(link,"IconLocation",executable+",0");Call(link,"Save");Release(link);link=null;
   if(previous!=null)File.Replace(temporary,LinkPath,null);else File.Move(temporary,LinkPath);
  }finally{Release(link);Release(shell);if(File.Exists(temporary))File.Delete(temporary);}
 }
}
}
