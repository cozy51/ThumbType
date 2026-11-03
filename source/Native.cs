using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ThumbShiftPortable {
internal static class Native {
 // Some input paths report VK_SHIFT rather than a side-specific VK.
 // Use the physical scan code for both press and release.
 public static int NormalizeKey(uint vk,uint scan){if(vk==0x10||vk==0xA0||vk==0xA1){if((scan&0xff)==0x36)return 0xA1;if((scan&0xff)==0x2A)return 0xA0;}return (int)vk;}
 public delegate IntPtr HookProc(int n,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential)] public struct Kbd {public uint vk,scan,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] public struct Input {public uint type;public Union data;}
 [StructLayout(LayoutKind.Explicit)] public struct Union {[FieldOffset(0)]public Keyboard keyboard;[FieldOffset(0)]public Mouse mouse;}
 [StructLayout(LayoutKind.Sequential)] public struct Keyboard {public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] public struct Mouse {public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id,HookProc proc,IntPtr mod,uint tid);
 [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hook,int n,IntPtr w,IntPtr l);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint count,Input[] input,int size);
 [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [StructLayout(LayoutKind.Sequential)] struct Rect {public int left,top,right,bottom;}
 [StructLayout(LayoutKind.Sequential)] struct GuiInfo {public uint size,flags;public IntPtr active,focus,capture,menuOwner,moveSize,caret;public Rect caretRect;}
 [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint id,ref GuiInfo info);
 [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint id);
 [DllImport("imm32.dll")] static extern IntPtr ImmGetDefaultIMEWnd(IntPtr h);
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr h,uint msg,UIntPtr w,IntPtr l,uint flags,uint timeout,out UIntPtr result);
 public static bool JapaneseMode(bool success,bool open,uint conversion){return success&&open&&(conversion&1)!=0&&(conversion&0x100)==0;}
 public static bool IsJapaneseIme(IntPtr window){
  uint pid;uint tid=GetWindowThreadProcessId(window,out pid);if(tid==0||((long)GetKeyboardLayout(tid)&0xffff)!=0x0411)return false;
  var info=new GuiInfo{size=(uint)Marshal.SizeOf(typeof(GuiInfo))};if(!GetGUIThreadInfo(tid,ref info)||info.focus==IntPtr.Zero)return false;
  IntPtr ime=ImmGetDefaultIMEWnd(info.focus);if(ime==IntPtr.Zero)return false;UIntPtr open,mode;
  // Query only; never change IME state. Bound both cross-process calls.
  if(SendMessageTimeout(ime,0x283,new UIntPtr(5),IntPtr.Zero,3,10,out open)==IntPtr.Zero||open==UIntPtr.Zero)return false;
  if(SendMessageTimeout(ime,0x283,new UIntPtr(1),IntPtr.Zero,3,10,out mode)==IntPtr.Zero)return false;
  return Native.GetForegroundWindow()==window&&JapaneseMode(true,true,(uint)mode.ToUInt64());
 }
 [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint vk);
 [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
 [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr p,uint access,out IntPtr token);
 [DllImport("advapi32.dll")] static extern bool GetTokenInformation(IntPtr token,int info,out int value,int size,out int needed);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);
 public static bool SafeTarget(IntPtr h){uint pid;GetWindowThreadProcessId(h,out pid);if(pid==0||pid==(uint)Process.GetCurrentProcess().Id)return false;IntPtr p=OpenProcess(0x1000,false,pid);if(p==IntPtr.Zero)return false;IntPtr t=IntPtr.Zero;try{if(!OpenProcessToken(p,8,out t))return false;int v,n;return GetTokenInformation(t,20,out v,4,out n)&&v==0;}finally{if(t!=IntPtr.Zero)CloseHandle(t);CloseHandle(p);}}
 static Input Key(ushort vk,ushort scan,uint flags){return new Input{type=1,data=new Union{keyboard=new Keyboard{vk=vk,scan=scan,flags=flags,extra=new UIntPtr(0x545350)}}};}
 public static void Text(string text){var list=new List<Input>();foreach(char c in text){list.Add(Key(0,c,4));list.Add(Key(0,c,6));}Send(list.ToArray());}
 public static void Tap(int vk){uint ext=vk==0xA3||vk==0xA5?1u:0;Send(new Input[]{Key((ushort)vk,0,ext),Key((ushort)vk,0,ext|2)});}
 static void Send(Input[] keys){if(keys.Length==0)return;uint sent=SendInput((uint)keys.Length,keys,Marshal.SizeOf(typeof(Input)));if(sent!=(uint)keys.Length){
  // A partial insertion may end on a down event. Release only our own output keys.
  var releases=new List<Input>();for(int i=0;i<sent;i++)if((keys[i].data.keyboard.flags&2)==0){Input r=keys[i];r.data.keyboard.flags|=2;releases.Add(r);}if(releases.Count>0)SendInput((uint)releases.Count,releases.ToArray(),Marshal.SizeOf(typeof(Input)));
  throw new InvalidOperationException("入力を送信できなかったため停止しました。対象アプリの権限や入力方式を確認してください。");}}
}
public sealed class HookService : IDisposable {
 readonly Engine engine; readonly Action<string> failed; readonly InputRouter router;
 readonly object gate=new object(); Thread thread; ApplicationContext context; Native.HookProc proc; IntPtr hook;
 volatile bool active; bool disposed; IntPtr foreground;bool target; readonly Stopwatch clock=Stopwatch.StartNew();
 public HookService(Engine e,Action<string> f){engine=e;router=new InputRouter(e);failed=f;e.Text=Native.Text;e.Tap=Native.Tap;}
 public void Start(){var ready=new ManualResetEvent(false);Exception error=null;thread=new Thread(delegate(){try{
   context=new ApplicationContext();lock(gate){if(disposed)throw new OperationCanceledException();proc=Callback;hook=Native.SetWindowsHookEx(13,proc,Native.GetModuleHandle(null),0);if(hook==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());active=true;}
   using(var timer=new System.Windows.Forms.Timer()){timer.Interval=10;timer.Tick+=delegate{try{lock(gate){if(!active){context.ExitThread();return;}CheckFocus();if(engine.HasPending)router.Tick(clock.ElapsedMilliseconds,CanConvert());}}catch(Exception ex){Fail(ex);}};timer.Start();ready.Set();Application.Run(context);}
  }catch(Exception ex){error=ex;ready.Set();}finally{lock(gate){active=false;if(hook!=IntPtr.Zero){Native.UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}engine.Reset();}}});thread.IsBackground=true;thread.SetApartmentState(ApartmentState.STA);thread.Start();if(!ready.WaitOne(3000)){Dispose();throw new TimeoutException("フック起動がタイムアウトしました。");}if(error!=null)throw error;}
 void CheckFocus(){IntPtr h=Native.GetForegroundWindow();if(h!=foreground){foreground=h;target=Native.SafeTarget(h);router.SetEnabled(false);engine.Reset();}}
 bool CanConvert(){bool pinky=((engine.Left!=0xA0&&engine.Right!=0xA0)&&Held(0xA0))||((engine.Left!=0xA1&&engine.Right!=0xA1)&&Held(0xA1));if(engine.Pinky!=pinky){router.SetEnabled(false);engine.Pinky=pinky;}return target&&!Held(0xA2)&&!Held(0xA3)&&!Held(0xA4)&&!Held(0xA5)&&!Held(0x5B)&&!Held(0x5C)&&(!pinky||engine.Layout.HasPinky)&&Native.IsJapaneseIme(foreground);}
 static bool Held(int k){return (Native.GetAsyncKeyState(k)&0x8000)!=0;}
 IntPtr Callback(int n,IntPtr w,IntPtr l){
  if(n<0)return Native.CallNextHookEx(hook,n,w,l);
  try{lock(gate){if(!active)return Native.CallNextHookEx(hook,n,w,l);Native.Kbd k=(Native.Kbd)Marshal.PtrToStructure(l,typeof(Native.Kbd));if((k.flags&0x10)!=0)return Native.CallNextHookEx(hook,n,w,l);
   int vk=Native.NormalizeKey(k.vk,k.scan);bool up=(k.flags&0x80)!=0;
   if(vk==0x7B&&Held(0x11)&&Held(0x12)){Dispose();failed("緊急停止しました。通常の入力に戻りました。");return Native.CallNextHookEx(hook,n,w,l);}
   CheckFocus();
   bool eligible=CanConvert();
   if(router.Handle(vk,!up,clock.ElapsedMilliseconds,eligible))return new IntPtr(1);
  }}catch(Exception ex){Fail(ex);}return Native.CallNextHookEx(hook,n,w,l);
 }
 void Fail(Exception ex){Dispose();failed(ex.Message);}
 public void Dispose(){lock(gate){disposed=true;active=false;if(hook!=IntPtr.Zero){Native.UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}engine.Reset();} }
}
}
