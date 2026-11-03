using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ThumbShiftPortable {
// Notification-area icon built directly on Shell_NotifyIcon.
// WinForms NotifyIcon ignores a failed NIM_ADD (for example a timeout while Explorer is
// still starting at Windows login) and then believes the icon is shown, so it never appears.
// This wrapper checks the result, retries until the shell accepts the icon, and re-adds it
// whenever Explorer restarts (TaskbarCreated).
internal sealed class TrayIcon : NativeWindow, IDisposable {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct NotifyIconData {
  public int cbSize;public IntPtr hWnd;public int uID;public int uFlags;public int uCallbackMessage;public IntPtr hIcon;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string szTip;
  public int dwState,dwStateMask;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string szInfo;
  public int uTimeoutOrVersion;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string szInfoTitle;
  public int dwInfoFlags;public Guid guidItem;public IntPtr hBalloonIcon;
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern bool Shell_NotifyIcon(int message,ref NotifyIconData data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
 const int NIM_ADD=0,NIM_MODIFY=1,NIM_DELETE=2;
 const int NIF_MESSAGE=1,NIF_ICON=2,NIF_TIP=4,NIF_INFO=0x10;
 const int CallbackMessage=0x8000+0x51; // WM_APP based
 const int WM_LBUTTONDBLCLK=0x203,WM_RBUTTONUP=0x205,WM_NULL=0;
 // Same id as the first WinForms NotifyIcon, so Windows keeps the user's "show in taskbar" choice.
 const int IconId=1;
 static readonly int TaskbarCreated=RegisterWindowMessage("TaskbarCreated");

 readonly Timer retry=new Timer();
 Icon icon;string text="";bool visible;bool added;bool disposed;
 string pendingTitle,pendingText;ToolTipIcon pendingKind;int pendingTimeout;

 public event EventHandler DoubleClick;
 public ContextMenuStrip ContextMenuStrip{get;set;}
 /// <summary>True once the shell has confirmed the icon.</summary>
 public bool Added{get{return added;}}

 public TrayIcon(){CreateHandle(new CreateParams());retry.Interval=2000;retry.Tick+=delegate{if(!visible||added||disposed){retry.Stop();return;}TryAdd();};}

 /// <summary>The tray owns this icon and disposes it.</summary>
 public Icon Icon{get{return icon;}set{if(icon==value)return;Icon old=icon;icon=value;if(added)Modify(NIF_ICON);if(old!=null&&old!=value)old.Dispose();}}
 public string Text{get{return text;}set{text=value??"";if(text.Length>127)text=text.Substring(0,127);if(added)Modify(NIF_TIP);}}
 public bool Visible{get{return visible;}set{if(disposed||visible==value)return;visible=value;if(value)TryAdd();else Remove();}}

 public void ShowBalloonTip(int timeout,string title,string message,ToolTipIcon kind){
  if(disposed)return;
  if(!added){pendingTimeout=timeout;pendingTitle=title;pendingText=message;pendingKind=kind;return;}
  var data=Data(NIF_INFO);data.szInfoTitle=Cut(title,63);data.szInfo=Cut(message,255);data.uTimeoutOrVersion=timeout;
  data.dwInfoFlags=kind==ToolTipIcon.Info?1:kind==ToolTipIcon.Warning?2:kind==ToolTipIcon.Error?3:0;
  Shell_NotifyIcon(NIM_MODIFY,ref data);
 }

 void TryAdd(){
  if(disposed||!visible||added)return;
  var data=Data(NIF_MESSAGE|NIF_ICON|NIF_TIP);
  // NIM_ADD can report failure (timeout) even though the shell added the icon; NIM_MODIFY tells which.
  if(Shell_NotifyIcon(NIM_ADD,ref data)||Shell_NotifyIcon(NIM_MODIFY,ref data)){added=true;retry.Stop();
   if(pendingTitle!=null){string title=pendingTitle,message=pendingText;pendingTitle=pendingText=null;ShowBalloonTip(pendingTimeout,title,message,pendingKind);}}
  else if(!retry.Enabled)retry.Start();
 }
 void Remove(){retry.Stop();if(!added)return;var data=Data(0);Shell_NotifyIcon(NIM_DELETE,ref data);added=false;}
 void Modify(int flags){var data=Data(flags);if(!Shell_NotifyIcon(NIM_MODIFY,ref data)){added=false;TryAdd();}}
 NotifyIconData Data(int flags){
  var data=new NotifyIconData{cbSize=Marshal.SizeOf(typeof(NotifyIconData)),hWnd=Handle,uID=IconId,uFlags=flags,uCallbackMessage=CallbackMessage,szTip=text,szInfo="",szInfoTitle=""};
  if(icon!=null)data.hIcon=icon.Handle;else data.uFlags&=~NIF_ICON;
  return data;
 }
 static string Cut(string s,int max){s=s??"";return s.Length>max?s.Substring(0,max):s;}

 void ShowMenu(){
  var menu=ContextMenuStrip;if(menu==null)return;
  // Required so the menu closes when the user clicks elsewhere.
  SetForegroundWindow(Handle);menu.Show(Cursor.Position,ToolStripDropDownDirection.AboveLeft);PostMessage(Handle,WM_NULL,IntPtr.Zero,IntPtr.Zero);
 }

 protected override void WndProc(ref Message m){
  if(m.Msg==CallbackMessage){
   int e=(int)((long)m.LParam&0xffff);
   if(e==WM_LBUTTONDBLCLK){var handler=DoubleClick;if(handler!=null)handler(this,EventArgs.Empty);}
   else if(e==WM_RBUTTONUP)ShowMenu();
   return;
  }
  if(TaskbarCreated!=0&&m.Msg==TaskbarCreated){added=false;TryAdd();}
  base.WndProc(ref m);
 }

 public void Dispose(){
  if(disposed)return;
  Remove();visible=false;disposed=true;retry.Dispose();
  if(icon!=null){icon.Dispose();icon=null;}
  DestroyHandle();
 }
}
}
