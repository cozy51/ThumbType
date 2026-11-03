using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ThumbShiftPortable {
static class Program {
 [STAThread] static void Main(string[] args){
  if(args.Length>0&&args[0]=="--export-layout"){string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layouts");Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"NICOLA.tsv"),ThumbShiftPortable.Layout.Sample,new UTF8Encoding(true));return;}
  if(args.Length>0&&args[0]=="--self-test"){Environment.Exit(SelfTest.Run());return;}
  if(args.Length>0&&args[0]=="--settings-test"){Application.EnableVisualStyles();Environment.ExitCode=MainForm.TestRememberedLayout();return;}
  if(args.Length>0&&args[0]=="--startup-test"){Application.EnableVisualStyles();Environment.ExitCode=MainForm.TestStartup();return;}
  if(args.Length>0&&args[0]=="--registration-test"){Application.EnableVisualStyles();Environment.ExitCode=MainForm.TestStartupRegistration();return;}
  if(args.Length>0&&args[0]=="--preview"){Application.EnableVisualStyles();using(var f=new MainForm(false,false)){f.RenderPreview();}return;}
  if(args.Length>0&&args[0]=="--tray-test"){Application.EnableVisualStyles();using(var f=new MainForm(true,false)){f.TestTrayLifecycle();}return;}
  bool first;using(var mutex=new Mutex(true,"Local\\ThumbShiftPortable-v1",out first)){
   if(!first){MessageBox.Show("すでに起動しています。通知領域（時計の横、または「^」の中）のアイコンをダブルクリックしてください。");return;}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   MainForm form=null;Application.ThreadException+=delegate(object s,ThreadExceptionEventArgs e){if(form!=null)form.Stop();MessageBox.Show("安全のため停止しました。\n"+e.Exception.Message);};
   AppDomain.CurrentDomain.UnhandledException+=delegate{if(form!=null)form.Stop();};
   try{form=new MainForm();form.QueueStartup();Application.Run(form);}finally{if(form!=null)form.Stop();}
  }
 }
}
public sealed class MainForm:Form {
 readonly TextBox path=new TextBox();readonly Label status=new Label();readonly Button start=new Button();readonly TabControl tabs=new TabControl();
 readonly ComboBox left=new ComboBox(),right=new ComboBox(),mode=new ComboBox();readonly NumericUpDown time=new NumericUpDown();
 readonly CheckBox lt=new CheckBox(),rt=new CheckBox(),lh=new CheckBox(),rh=new CheckBox(),repeat=new CheckBox();readonly TextBox test=new TextBox();
 HookService service;Layout layout;bool hotkey;bool preview;bool allowShow;bool exiting;bool resourcesDisposed;bool testing;
 readonly NotifyIcon tray=new NotifyIcon();readonly ContextMenuStrip trayMenu=new ContextMenuStrip();
 ToolStripMenuItem trayStart,trayStop;
 string layoutError;
 readonly LayoutSettings settings;
 StartupRegistration registration;TabPage startupPage;readonly CheckBox loginStart=new CheckBox();readonly Label loginStatus=new Label();readonly Button repairStartup=new Button();bool refreshingStartup;
 public MainForm(bool enableTray=true,bool rememberLayout=true,string settingsFile=null,StartupRegistration loginRegistration=null){settings=rememberLayout?new LayoutSettings(settingsFile??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ThumbShiftPortable.settings.xml")):null;using(var iconStream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ThumbShiftPortable.AppIcon")){if(iconStream!=null)using(var appIcon=new Icon(iconStream)){Icon=(Icon)appIcon.Clone();}}ShowInTaskbar=false;Text="ThumbType v0.7 — 停止中";ClientSize=new Size(700,570);MinimumSize=new Size(720,610);Font=new Font("Yu Gothic UI",10);StartPosition=FormStartPosition.CenterScreen;
  status.SetBounds(20,15,660,44);status.Text="● 停止中 — 通常のキーボード入力です";status.ForeColor=Color.DarkGreen;
  tabs.SetBounds(20,66,660,330);tabs.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
  TabPage p=Page("配列");LabelAt(p,"配列定義ファイル（.yab / .tsv）",20,20);path.SetBounds(20,55,490,29);p.Controls.Add(path);
  var browse=ButtonAt(p,"参照…",520,54,90);browse.Click+=delegate{using(var d=new OpenFileDialog()){d.Filter="配列ファイル (*.yab;*.tsv)|*.yab;*.tsv|すべてのファイル|*.*";if(d.ShowDialog()==DialogResult.OK){path.Text=d.FileName;LoadLayout();}}};
  var load=ButtonAt(p,"読み込み",20,96,110);load.Click+=delegate{LoadLayout();};var sample=ButtonAt(p,"内蔵NICOLA",145,96,140);sample.Click+=delegate{Stop();path.Text="";LoadLayout();};
  var export=ButtonAt(p,"配列保存…",300,96,150);export.Click+=delegate{using(var d=new SaveFileDialog()){d.FileName="NICOLA.tsv";d.Filter="配列 (*.tsv)|*.tsv";if(d.ShowDialog()==DialogResult.OK)try{File.WriteAllText(d.FileName,ThumbShiftPortable.Layout.Sample,new UTF8Encoding(true));}catch(Exception ex){MessageBox.Show(ex.Message);}}};
  LabelAt(p,"前回読み込んだ配列を記憶します。起動時に前回の配列で自動的に開始します。\n添付NICOLA.yabを内蔵。JIS配列用、ローマ字4セクションに対応。\n出力はUnicode文字です。IMEの変換前かな入力とは動作が異なります。",20,155,600,100);
  p=Page("親指シフト");LabelAt(p,"左親指キー",25,22);LabelAt(p,"右親指キー",345,22);SetupKey(left,25,55,p);SetupKey(right,345,55,p);left.SelectedIndex=2;right.SelectedIndex=4;
  SetupCheck(lt,p,"単独打鍵を有効にする",25,105,true);SetupCheck(rt,p,"単独打鍵を有効にする",345,105,true);SetupCheck(lh,p,"長押し中は連続シフト",25,145,true);SetupCheck(rh,p,"長押し中は連続シフト",345,145,true);
  LabelAt(p,"単独打鍵は親指キーを離した時に元のキーを1回送信します。\n左右を同時に押した場合は、後から押した親指を優先します。",25,205,590,65);
  p=Page("同時打鍵・リピート");LabelAt(p,"同時打鍵の時間範囲（ms）",25,25,300,28);time.SetBounds(350,25,100,30);time.Minimum=10;time.Maximum=300;time.Value=65;p.Controls.Add(time);
  var slider=new TrackBar();slider.SetBounds(25,75,570,55);slider.Minimum=10;slider.Maximum=300;slider.TickFrequency=25;slider.Value=65;p.Controls.Add(slider);slider.ValueChanged+=delegate{time.Value=slider.Value;};time.ValueChanged+=delegate{slider.Value=(int)time.Value;};
  SetupCheck(repeat,p,"割り当てた文字キーのリピートを有効にする",25,145,true);LabelAt(p,"文字→親指、親指→文字の両順序に対応します。\n文字の確定は、親指の押下・文字キーを離す・時間切れのいずれかです。",25,200,600,70);
  p=Page("動作モード");mode.SetBounds(25,30,480,30);mode.DropDownStyle=ComboBoxStyle.DropDownList;mode.Items.AddRange(new object[]{"親指シフト（通常／左／右の3層）","キー配列変更（親指キーは通常動作）"});mode.SelectedIndex=0;p.Controls.Add(mode);
  LabelAt(p,"Ctrl・Alt・Windowsキーを使うショートカットは素通しします。\n親指以外のShiftは小指シフト（定義がない場合は素通し）です。\n英数（A）・IMEオフ・状態不明では通常入力を通します。\n\n日本語（あ）のときだけ配列を変換します。\n緊急停止：Ctrl + Alt + F12\n停止後の再開は、この画面の「開始」から行ってください。",25,85,600,180);
  startupPage=Page("起動設定");SetupCheck(loginStart,startupPage,"Windowsログイン時に起動する",25,25,false);
  loginStatus.SetBounds(25,70,590,80);startupPage.Controls.Add(loginStatus);
  repairStartup.Text="現在のexeで登録し直す";repairStartup.SetBounds(25,155,235,34);startupPage.Controls.Add(repairStartup);
  LabelAt(startupPage,"チェックで登録、解除で登録を削除します。管理者権限は不要です。\n登録後にexeを移動した場合は、この画面で登録し直してください。\nWindows側で無効にした場合は、Windowsのスタートアップ設定も確認してください。",25,210,590,75);
  loginStart.CheckedChanged+=delegate{if(refreshingStartup||registration==null)return;try{registration.SetEnabled(loginStart.Checked);}catch(Exception ex){MessageBox.Show("スタートアップ設定を変更できません。\n"+ex.Message,"起動設定");}RefreshStartup();};
  repairStartup.Click+=delegate{if(registration==null)return;try{registration.SetEnabled(true);}catch(Exception ex){MessageBox.Show("登録を更新できません。\n"+ex.Message,"起動設定");}RefreshStartup();};
  try{string folder=Environment.GetFolderPath(Environment.SpecialFolder.Startup);if(loginRegistration==null&&string.IsNullOrEmpty(folder))throw new IOException("スタートアップフォルダを取得できません。");registration=loginRegistration??new StartupRegistration(Application.ExecutablePath,folder);RefreshStartup();}catch(Exception ex){loginStart.Enabled=repairStartup.Enabled=false;loginStatus.Text=ex.Message;}
  start.SetBounds(20,410,145,38);start.Text="開始";start.Click+=delegate{Start();};var stop=new Button();stop.SetBounds(180,410,145,38);stop.Text="停止・通常入力へ";stop.Click+=delegate{Stop();};var close=new Button();close.SetBounds(535,410,145,38);close.Text="終了";close.Click+=delegate{ExitApplication();};
  var hide=ButtonAt(this,"トレーに戻す",350,410,155);hide.Click+=delegate{HideSettings();};
  var help=new Label();help.SetBounds(20,462,660,70);help.Text="×・最小化：トレーに戻る（動作は継続）／「終了」：解除して終了\n緊急停止：Ctrl + Alt + F12　／　トレーをダブルクリックで設定\n起動時に自動開始します。トレーの右クリックから停止できます。";
  Controls.AddRange(new Control[]{status,tabs,start,stop,close,help});layout=ThumbShiftPortable.Layout.Parse(ThumbShiftPortable.Layout.Sample);
  trayMenu.Items.Add("設定を開く",null,delegate{ShowSettings();});
  trayStart=(ToolStripMenuItem)trayMenu.Items.Add("開始",null,delegate{Start();});
  trayStop=(ToolStripMenuItem)trayMenu.Items.Add("停止・通常入力へ",null,delegate{Stop();});
  trayMenu.Items.Add(new ToolStripSeparator());trayMenu.Items.Add("終了",null,delegate{ExitApplication();});
  tray.Icon=Icon;tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=delegate{ShowSettings();};UpdateTray();
  // Keep a stable window handle for hotkeys even while settings are hidden.
  IntPtr windowHandle=Handle;tray.Visible=enableTray;
  Resize+=delegate{if(WindowState==FormWindowState.Minimized)HideSettings();};
  FormClosing+=delegate(object sender,FormClosingEventArgs e){if(e.CloseReason==CloseReason.UserClosing&&!exiting&&!preview){e.Cancel=true;HideSettings();return;}Stop();};
  SystemEvents.SessionSwitch+=SessionChanged;SystemEvents.PowerModeChanged+=PowerChanged;
  if(settings!=null)try{path.Text=settings.Load();if(path.Text.Length>0)LoadLayout(false,false);}catch(Exception ex){layout=null;status.Text="設定の復元に失敗しました。配列を選び直してください。";tray.ShowBalloonTip(6000,"設定の復元に失敗",ex.Message,ToolTipIcon.Warning);}
 }
 protected override void SetVisibleCore(bool value){base.SetVisibleCore(value&&(allowShow||preview));}
 bool startupPending;
 // Queue only once, after the UI message loop begins; Stop cancels this request.
 internal void QueueStartup(Action begin=null){if(layout==null||resourcesDisposed||startupPending)return;startupPending=true;BeginInvoke((Action)delegate{if(!startupPending||resourcesDisposed)return;startupPending=false;if(layout!=null){if(begin!=null)begin();else Start(false);}});}
 void ShowSettings(){RefreshStartup();allowShow=true;WindowState=FormWindowState.Normal;Show();if(!testing)Activate();}
 void RefreshStartup(){if(registration==null)return;refreshingStartup=true;try{string target=registration.ReadTarget();loginStart.Checked=target!=null;loginStart.Enabled=repairStartup.Enabled=true;loginStatus.Text=target==null?"未登録です。":registration.IsCurrent(target)?"登録済み：\n"+target:"別の場所のexeが登録されています。登録し直してください。\n"+target;}catch(Exception ex){loginStart.Checked=false;loginStart.Enabled=repairStartup.Enabled=false;loginStatus.Text=ex.Message;}finally{refreshingStartup=false;}}
 void SetConfigEnabled(bool enabled){foreach(TabPage page in tabs.TabPages)page.Enabled=enabled||page==startupPage;}
 void HideSettings(){allowShow=false;Hide();}
 void ExitApplication(){exiting=true;Stop();Close();}
 void UpdateTray(){bool running=service!=null;tray.Text=running?"ThumbType：動作中（Ctrl+Alt+F12で停止）":"ThumbType：停止中（ダブルクリックで設定）";if(trayStart!=null)trayStart.Enabled=!running;if(trayStop!=null)trayStop.Enabled=running;}
 protected override void Dispose(bool disposing){if(disposing&&!resourcesDisposed){Stop();tray.Visible=false;tray.Dispose();trayMenu.Dispose();SystemEvents.SessionSwitch-=SessionChanged;SystemEvents.PowerModeChanged-=PowerChanged;resourcesDisposed=true;}base.Dispose(disposing);}
 void SessionChanged(object s,SessionSwitchEventArgs e){if(IsHandleCreated)BeginInvoke((Action)Stop);}
 void PowerChanged(object s,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Suspend&&IsHandleCreated)BeginInvoke((Action)Stop);}
 TabPage Page(string title){var p=new TabPage(title);tabs.TabPages.Add(p);return p;}
 static void LabelAt(Control p,string text,int x,int y,int w=260,int h=28){var l=new Label{Text=text};l.SetBounds(x,y,w,h);p.Controls.Add(l);}
 static Button ButtonAt(Control p,string text,int x,int y,int w){var b=new Button{Text=text};b.SetBounds(x,y,w,34);p.Controls.Add(b);return b;}
 static void SetupCheck(CheckBox c,Control p,string text,int x,int y,bool value){c.Text=text;c.Checked=value;c.SetBounds(x,y,560-x,30);p.Controls.Add(c);}
 static readonly string[] KeyNames={"無変換", "変換", "Space", "左Shift", "右Shift"};static readonly int[] KeyCodes={0x1D,0x1C,0x20,0xA0,0xA1};
 static void SetupKey(ComboBox c,int x,int y,Control p){c.DropDownStyle=ComboBoxStyle.DropDownList;c.Items.AddRange(KeyNames);c.SetBounds(x,y,250,30);p.Controls.Add(c);}
 void LoadLayout(bool remember=true,bool showError=true){Stop();layoutError=null;try{string selected=path.Text.Trim();if(selected.Length>0){selected=Path.GetFullPath(selected);path.Text=selected;}layout=selected.Length==0?ThumbShiftPortable.Layout.Parse(ThumbShiftPortable.Layout.Sample):YabLayout.ReadFile(selected);status.Text="配列を読み込みました："+layout.Rows.Count+"キー（停止中）";}catch(Exception ex){layoutError=ex.ToString();layout=null;status.Text="配列を読み込めません。ファイルを選び直してください（停止中）。";if(showError)MessageBox.Show("配列を読み込めません。\n"+ex.Message,"読み込みエラー");else tray.ShowBalloonTip(6000,"前回の配列を読み込めません", "移動・削除・内容を確認して選び直してください。停止状態を維持します。",ToolTipIcon.Warning);return;}if(remember&&settings!=null)try{settings.Save(path.Text.Trim());}catch(Exception ex){if(!showError)throw;MessageBox.Show("配列は読み込めましたが、次回用の保存に失敗しました。\nexeのフォルダに書き込めるか確認してください。\n"+ex.Message,"設定保存エラー");}}
 void Start(bool manual=true){Stop();LoadLayout(manual,manual);if(layout==null)return;if(left.SelectedIndex==right.SelectedIndex){MessageBox.Show("左右の親指には別のキーを指定してください。");return;}
  if(!Native.RegisterHotKey(Handle,1,0x4003,0x7B)){MessageBox.Show("緊急停止キー Ctrl+Alt+F12 を確保できないため開始しません。他のアプリの割り当てを確認してください。");return;}hotkey=true;
  try{var engine=new Engine{Layout=layout,Left=KeyCodes[left.SelectedIndex],Right=KeyCodes[right.SelectedIndex],Window=(int)time.Value,LeftTap=lt.Checked,RightTap=rt.Checked,LeftHold=lh.Checked,RightHold=rh.Checked,Repeat=repeat.Checked,Direct=mode.SelectedIndex==1};
   service=new HookService(engine,delegate(string message){if(IsHandleCreated)BeginInvoke((Action)delegate{Stop();MessageBox.Show(message,"安全停止");});});service.Start();SetConfigEnabled(false);start.Enabled=false;status.Text="● 動作中 — 緊急停止 Ctrl + Alt + F12";status.ForeColor=Color.DarkRed;Text="ThumbType v0.7 — 動作中";UpdateTray();
  }catch(Exception ex){Stop();MessageBox.Show(ex.Message,"開始できません");}}
 public void Stop(){startupPending=false;if(resourcesDisposed)return;if(service!=null){service.Dispose();service=null;}if(hotkey){Native.UnregisterHotKey(Handle,1);hotkey=false;}SetConfigEnabled(true);start.Enabled=true;status.Text="● 停止中 — 通常のキーボード入力です";status.ForeColor=Color.DarkGreen;Text="ThumbType v0.7 — 停止中";UpdateTray();}
 protected override bool ShowWithoutActivation { get { return preview||testing; } }
 public static int TestStartupRegistration(){string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Registration-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var report=new StringBuilder();int code=0;
  Action<bool,string> check=delegate(bool ok,string name){if(!ok)throw new Exception(name);report.AppendLine("PASS: "+name);};
  try{var r=new StartupRegistration(Application.ExecutablePath,root);
   check(r.ReadTarget()==null&&!File.Exists(r.LinkPath),"reading does not register");
   using(var f=new MainForm(false,false,null,r)){check(!f.loginStart.Checked&&!File.Exists(r.LinkPath),"opening settings leaves registration unchanged");}
   r.SetEnabled(true);check(r.IsCurrent(r.ReadTarget()),"shortcut registered with exact exe path");r.SetEnabled(true);check(Directory.GetFiles(root,"*.lnk").Length==1,"repeated registration does not duplicate");
   string other=Path.Combine(root,"日本語 空白.exe");File.WriteAllBytes(other,new byte[]{0});var newer=new StartupRegistration(other,root);newer.SetEnabled(true);check(newer.IsCurrent(newer.ReadTarget())&&!r.IsCurrent(r.ReadTarget()),"registration updates to new path with Japanese and spaces");
   using(var f=new MainForm(false,false,null,newer)){check(f.loginStart.Checked,"new instance shows saved registration");f.SetConfigEnabled(false);check(f.startupPage.Enabled&&!f.tabs.TabPages[0].Enabled,"startup settings usable while input configuration locked");f.loginStart.Checked=false;check(!File.Exists(newer.LinkPath),"unchecking removes shortcut");f.loginStart.Checked=true;check(newer.IsCurrent(newer.ReadTarget()),"checking registers immediately");}
   string unrelated=Path.Combine(root,"Other.lnk");File.WriteAllText(unrelated,"keep");newer.SetEnabled(false);newer.SetEnabled(false);check(!File.Exists(newer.LinkPath)&&File.ReadAllText(unrelated)=="keep","unregister is idempotent and preserves unrelated files");
   File.WriteAllText(newer.LinkPath,"unrelated shortcut");bool rejected=false;try{newer.SetEnabled(true);}catch(Exception){rejected=true;}check(rejected&&File.ReadAllText(newer.LinkPath)=="unrelated shortcut","same-name unrelated shortcut is preserved on registration");
   rejected=false;try{newer.SetEnabled(false);}catch(Exception){rejected=true;}check(rejected&&File.Exists(newer.LinkPath),"same-name unrelated shortcut is preserved on removal");
  }catch(Exception ex){code=1;report.AppendLine("FAIL: "+ex);}finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
  report.AppendLine("Used a private test directory only; actual Windows Startup folder was not modified. No input hooks installed.");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"registration-test.txt"),report.ToString(),new UTF8Encoding(true));return code;
 }
 public static int TestStartup(){var report=new StringBuilder();int code=0;Action<bool,string> check=delegate(bool ok,string name){if(!ok)throw new Exception(name);report.AppendLine("PASS: "+name);};
  try{using(var f=new MainForm(false,false)){int calls=0;Action begin=delegate{calls++;};
   f.QueueStartup(begin);f.QueueStartup(begin);check(calls==0,"startup waits for message loop");Application.DoEvents();check(calls==1,"startup runs once despite duplicate queue");
   check(!f.Visible&&!f.ShowInTaskbar,"auto-start does not open settings or taskbar");f.Stop();Application.DoEvents();check(calls==1,"stop does not auto-restart");
   f.QueueStartup(begin);f.Stop();Application.DoEvents();check(calls==1,"stop cancels pending auto-start");
   f.layout=null;f.QueueStartup(begin);Application.DoEvents();check(calls==1,"invalid restored layout prevents auto-start");
  }}catch(Exception ex){code=1;report.AppendLine("FAIL: "+ex);}
  report.AppendLine("Startup scheduling tested with a substitute start action; no real input hook installed.");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"startup-test.txt"),report.ToString(),new UTF8Encoding(true));return code;
 }
 public static int TestRememberedLayout(){string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ThumbShift-settings-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var report=new StringBuilder();int code=0;
  Action<bool,string> check=delegate(bool ok,string name){if(!ok)throw new Exception(name);report.AppendLine("PASS: "+name);};
  string config=Path.Combine(root,"settings.xml"),custom=Path.Combine(root,"日本語 & 配列.tsv");
  try{
   using(var f=new MainForm(false,true,config)){check(f.path.Text==""&&!File.Exists(config),"first startup uses sample without creating settings");File.WriteAllText(custom,ThumbShiftPortable.Layout.Sample,Encoding.UTF8);f.path.Text=custom;f.LoadLayout(true,false);check(File.Exists(config)&&f.layout!=null,"successful load saves immediately: "+f.layoutError);}
   check(File.ReadAllText(config).Contains("layout=\"日本語 &amp; 配列.tsv\""),"portable relative path and XML escaping");
   using(var f=new MainForm(false,true,config)){check(f.path.Text==custom&&f.layout.Rows.Count==48,"new instance restores path and layout");check(f.service==null&&!f.hotkey,"restoration never starts remapping");f.path.Text=Path.Combine(root,"missing.tsv");f.LoadLayout(true,false);check(f.layout==null&&new LayoutSettings(config).Load()==custom,"failed load preserves last successful setting");}
   using(var f=new MainForm(false,true,config)){f.path.Text="";f.LoadLayout(true,false);}using(var f=new MainForm(false,true,config)){check(f.path.Text==""&&f.layout!=null,"sample selection is remembered");}
   new LayoutSettings(config).Save(custom);File.Delete(custom);
   using(var f=new MainForm(false,true,config)){check(f.path.Text==custom&&f.layout==null&&f.service==null,"missing restored file stays stopped without sample fallback");}
   string external=Path.Combine(Path.GetTempPath(),"external-"+Guid.NewGuid().ToString("N")+".tsv");new LayoutSettings(config).Save(external);check(new LayoutSettings(config).Load()==external,"external absolute path round trip and atomic replacement");
   File.WriteAllText(config,"broken");bool rejected=false;try{new LayoutSettings(config).Load();}catch(System.Xml.XmlException){rejected=true;}check(rejected,"corrupt settings rejected");
  }catch(Exception ex){code=1;report.AppendLine("FAIL: "+ex);}finally{foreach(string file in Directory.GetFiles(root))File.Delete(file);Directory.Delete(root);}
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-test.txt"),report.ToString(),new UTF8Encoding(true));return code;
 }
 public void TestTrayLifecycle(){testing=true;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);var report=new StringBuilder();bool ok=true;
  Action<bool,string> check=delegate(bool condition,string name){if(!condition)throw new Exception(name);report.AppendLine("PASS: "+name);};
  using(var timer=new System.Windows.Forms.Timer()){timer.Interval=50;timer.Tick+=delegate{timer.Stop();try{
   check(!Visible&&!ShowInTaskbar,"startup hidden, no taskbar button");check(tray.Visible,"tray icon visible");check(service==null&&!hotkey,"startup stopped");check(trayStart.Enabled&&!trayStop.Enabled,"stopped menu state");
   ShowSettings();check(Visible&&!ShowInTaskbar,"settings open without taskbar");
   Close();check(!Visible&&!IsDisposed&&tray.Visible,"close hides settings and keeps tray resident");
   ShowSettings();check(Visible,"reopen after close");WindowState=FormWindowState.Minimized;check(!Visible&&!IsDisposed,"minimize hides settings");
   Stop();check(tray.Text.Contains("停止中"),"stop updates tray tooltip");ExitApplication();check(IsDisposed&&!tray.Visible,"exit disposes form and removes tray icon");Stop();check(true,"cleanup is idempotent");
  }catch(Exception ex){ok=false;report.AppendLine("FAIL: "+ex);ExitApplication();}};timer.Start();Application.Run(this);}
  File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tray-test.txt"),report.ToString(),new UTF8Encoding(true));if(!ok)Environment.ExitCode=1;
 }
 public void RenderPreview(){preview=true;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Location=new Point(-32000,-32000);Show();Application.DoEvents();for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;tabs.TabPages[i].CreateControl();Application.DoEvents();using(var bitmap=new Bitmap(Width,Height)){DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"screen-"+i+".png"));}}}
 protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==1)Stop();base.WndProc(ref m);}
}
}
