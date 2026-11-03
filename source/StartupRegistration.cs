using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace ThumbShiftPortable {
// Per-user Startup-folder shortcut. Constructing/reading never registers anything.
// Uses the documented Shell API (IShellLinkW / IPersistFile) directly instead of late-bound
// WScript.Shell scripting, which antivirus heuristics associate with script droppers.
public sealed class StartupRegistration {
 const string Marker="ThumbType startup (managed by ThumbType)";
 readonly string folder,executable;
 public string LinkPath {get{return Path.Combine(folder,"ThumbType.lnk");}}
 public StartupRegistration(string target,string startupFolder){executable=Path.GetFullPath(target);folder=Path.GetFullPath(startupFolder);}

 [ComImport,Guid("00021401-0000-0000-C000-000000000046")] class ShellLink {}
 [ComImport,InterfaceType(ComInterfaceType.InterfaceIsIUnknown),Guid("000214F9-0000-0000-C000-000000000046")]
 interface IShellLinkW {
  void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,int max,IntPtr findData,uint flags);
  void GetIDList(out IntPtr pidl);
  void SetIDList(IntPtr pidl);
  void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder name,int max);
  void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
  void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir,int max);
  void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
  void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder args,int max);
  void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
  void GetHotkey(out short hotkey);
  void SetHotkey(short hotkey);
  void GetShowCmd(out int show);
  void SetShowCmd(int show);
  void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)] StringBuilder path,int max,out int index);
  void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path,int index);
  void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string rel,uint reserved);
  void Resolve(IntPtr hwnd,uint flags);
  void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
 }
 const int StgmRead=0; const uint SlgpRawPath=4;
 static void Release(object obj){if(obj!=null&&Marshal.IsComObject(obj))Marshal.FinalReleaseComObject(obj);}

 public string ReadTarget(){if(!File.Exists(LinkPath))return null;IShellLinkW link=null;try{
   link=(IShellLinkW)new ShellLink();
   try{((IPersistFile)link).Load(LinkPath,StgmRead);}catch(Exception ex){throw new IOException("同名のショートカットが既にあります。ThumbTypeが作成したものではないため変更しません。\n"+LinkPath,ex);}
   var description=new StringBuilder(1024);link.GetDescription(description,description.Capacity);
   if(description.ToString()!=Marker)throw new IOException("同名のショートカットが既にあります。ThumbTypeが作成したものではないため変更しません。\n"+LinkPath);
   var path=new StringBuilder(32768);link.GetPath(path,path.Capacity,IntPtr.Zero,SlgpRawPath);return path.ToString();
  }finally{Release(link);}}
 public bool IsCurrent(string target){return !string.IsNullOrEmpty(target)&&string.Equals(Path.GetFullPath(target),executable,StringComparison.OrdinalIgnoreCase);}
 public void SetEnabled(bool enabled){string previous=ReadTarget();if(!enabled){if(previous!=null)File.Delete(LinkPath);return;}
  if(!File.Exists(executable))throw new FileNotFoundException("起動するexeが見つかりません。",executable);
  Directory.CreateDirectory(folder);string temporary=Path.Combine(folder,"ThumbType-"+Guid.NewGuid().ToString("N")+".lnk");IShellLinkW link=null;
  try{link=(IShellLinkW)new ShellLink();
   link.SetPath(executable);link.SetWorkingDirectory(Path.GetDirectoryName(executable));link.SetArguments("");link.SetDescription(Marker);link.SetIconLocation(executable,0);
   ((IPersistFile)link).Save(temporary,true);Release(link);link=null;
   if(previous!=null)File.Replace(temporary,LinkPath,null);else File.Move(temporary,LinkPath);
  }finally{Release(link);if(File.Exists(temporary))File.Delete(temporary);}
 }
}
}
