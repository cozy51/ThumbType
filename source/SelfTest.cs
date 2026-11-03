using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ThumbShiftPortable {
public static class SelfTest {
 static int count;static readonly StringBuilder log=new StringBuilder();
 static void Check(bool ok,string name){count++;if(!ok)throw new Exception("FAIL: "+name);log.AppendLine("PASS: "+name);}
 public static int Run(){try{
  var events=new List<string>();var e=new Engine{Layout=Layout.Parse("A\ta\tL\tR\nB\tb\tX\tY\n")};e.Text=delegate(string s){events.Add(s);};e.Tap=delegate(int k){events.Add("tap"+k);};
  Action reset=delegate{e.Reset();events.Clear();e.Direct=false;e.Repeat=true;e.LeftTap=e.RightTap=true;e.LeftHold=e.RightHold=false;};Func<string> result=delegate{return string.Join("|",events.ToArray());};
  e.Handle(65,true,0);e.Tick(65);Check(events.Count==0,"deadline is inclusive");e.Tick(66);Check(result()=="a","timeout normal");e.Handle(65,false,70);Check(result()=="a","no duplicate keyup");
  reset();e.Handle(65,true,0);e.Handle(e.Left,true,65);e.Handle(65,false,70);e.Handle(e.Left,false,80);Check(result()=="L","character then left / exact boundary / suppress thumb tap");
  reset();e.Handle(e.Right,true,0);e.Handle(65,true,40);e.Handle(e.Right,false,50);e.Handle(65,false,60);Check(result()=="R","right then character");
  reset();e.Handle(e.Left,true,0);e.Handle(e.Left,false,30);Check(result()=="tap29","standalone thumb");
  reset();e.LeftTap=false;e.Handle(e.Left,true,0);e.Handle(e.Left,false,30);Check(events.Count==0,"disabled standalone");
  reset();e.Handle(65,true,0);e.Handle(65,false,10);e.Tick(100);Check(result()=="a","quick key release");
  reset();e.Handle(e.Left,true,0);e.Handle(65,true,66);e.Handle(65,false,70);Check(result()=="a","late thumb not chord");
  reset();e.LeftHold=true;e.Handle(e.Left,true,0);e.Handle(65,true,200);e.Handle(65,false,210);e.Handle(66,true,300);e.Handle(66,false,310);e.Handle(e.Left,false,320);Check(result()=="L|X","continuous held thumb");
  reset();e.Handle(65,true,0);e.Tick(70);e.Handle(66,true,80);e.Tick(150);e.Handle(65,true,160);Check(result()=="a|b|a","repeat remembers each physical key");
  reset();e.Repeat=false;e.Handle(65,true,0);e.Tick(70);e.Handle(65,true,80);Check(result()=="a","repeat disabled");
  reset();e.Direct=true;e.Handle(e.Left,true,0);Check(events.Count==0,"direct mode thumb pass through");e.Handle(65,true,10);Check(result()=="a","direct mode immediate normal");
  reset();e.Handle(65,true,0);e.Reset();e.Tick(100);Check(events.Count==0,"stop discards pending text");
  reset();e.Handle(65,true,0);Check(!e.Handle(13,true,10)&&result()=="a","unmapped key flushes earlier text");
  reset();e.Handle(e.Left,true,0);e.Handle(e.Right,true,10);e.Handle(65,true,20);e.Handle(e.Left,false,30);e.Handle(e.Right,false,40);Check(result()=="R","two thumbs newest wins");
  Check(Layout.Parse("A\ta\t\t\n").Rows.Count==1,"empty shifted columns accepted");
  reset();e.Layout=Layout.Parse("A\ta\t\t\n");e.Handle(e.Left,true,0);e.Handle(65,true,10);Check(result()=="a","empty layer falls back to normal");
  bool bad=false;try{Layout.Parse("A\ta\tb\tc\nA\tx\ty\tz");}catch(FormatException){bad=true;}Check(bad,"reject duplicate keys");
  bad=false;try{Layout.Parse("LControlKey\ta\tb\tc");}catch(FormatException){bad=true;}Check(bad,"reject modifier mapping");
  bad=false;try{Layout.Parse("A,a,b,c");}catch(FormatException){bad=true;}Check(bad,"reject malformed file");
  Check(Layout.Parse(Layout.Sample).Rows.Count==48,"embedded sample parses");
  Check(Marshal.SizeOf(typeof(Native.Input))==(IntPtr.Size==8?40:28),"SendInput ABI size");
  Check(Native.NormalizeKey(0x10,0x36)==0xA1,"generic Shift + right scan code");
  Check(Native.NormalizeKey(0x10,0x2A)==0xA0,"generic Shift + left scan code");
  Check(Native.NormalizeKey(0xA1,0x36)==0xA1,"side-specific right Shift preserved");
  Check(Native.NormalizeKey(0x4A,0x24)==0x4A,"J unchanged by normalization");
  reset();e.Layout=Layout.Parse(Layout.Sample);e.Left=32;e.Right=0xA1;e.RightHold=true;
  e.Handle(Native.NormalizeKey(0x10,0x36),true,0);e.Handle(0x4A,true,200);e.Handle(0x4A,false,220);e.Handle(Native.NormalizeKey(0x10,0x36),false,240);
  Check(result()=="お","held generic right Shift + J gives お without standalone Shift");
  events.Clear();e.Handle(0x4A,true,300);e.Handle(0x4A,false,320);Check(result()=="と","J after right Shift release gives と");
  reset();e.Layout=Layout.Parse(Layout.Sample);e.Left=32;e.Right=0xA1;e.RightHold=false;
  e.Handle(Native.NormalizeKey(0x10,0x36),true,0);e.Handle(0x4A,true,40);Check(result()=="お","right Shift + J within timing window");
  Check(Native.JapaneseMode(true,true,0x19),"hiragana mode enables remapping");
  Check(!Native.JapaneseMode(true,false,0x19),"IME off bypasses remapping");
  Check(!Native.JapaneseMode(true,true,0x10),"open IME in alphanumeric mode bypasses remapping");
  Check(!Native.JapaneseMode(true,true,0x18),"full-width alphanumeric mode bypasses remapping");
  Check(!Native.JapaneseMode(false,true,0x19),"unknown IME state bypasses remapping");
  Check(!Native.JapaneseMode(true,true,0x119),"no-conversion mode bypasses remapping");
  Check(!Native.IsJapaneseIme(IntPtr.Zero),"missing target bypasses safely");
  reset();e.Layout=Layout.Parse(Layout.Sample);e.Left=32;e.Right=0xA1;e.RightHold=true;var router=new InputRouter(e);
  Check(!router.Handle(0x4A,true,0,false)&&!router.Handle(0x4A,false,20,false)&&events.Count==0,"English J passes down and up without kana");
  router.Handle(0x4A,true,30,true);router.Handle(0x4A,false,40,true);Check(result()=="と","Japanese J produces kana");events.Clear();
  router.Handle(0xA1,true,100,true);router.Handle(0x4A,true,120,true);router.Handle(0x4A,false,130,true);router.Handle(0xA1,false,140,true);Check(result()=="お","Japanese right Shift J still works");events.Clear();
  router.Handle(0x4A,true,200,true);router.Tick(300,false);Check(events.Count==0,"switch to English discards pending kana before timer");
  Check(router.Handle(0x4A,false,310,false)&&events.Count==0,"suppressed down keeps matching release suppressed after mode change");
  router.Handle(0x4A,true,400,false);Check(!router.Handle(0x4A,true,410,true)&&!router.Handle(0x4A,false,420,true)&&events.Count==0,"English held key stays native when switching to Japanese");
  router.Handle(0xA1,true,500,true);router.Tick(510,false);router.Tick(520,true);router.Handle(0xA1,true,530,true);router.Handle(0xA1,false,540,true);Check(events.Count==0,"cancelled thumb does not replay or reactivate after toggling twice");
  router.Handle(0x4A,true,600,false);router.Handle(0x4A,false,610,false);router.Handle(0x4A,true,620,true);router.Handle(0x4A,false,630,true);Check(result()=="と","new presses remap again after re-enabling Japanese mode");
  var nicola=YabLayout.Parse(YabLayout.ReadBuiltIn());
  int[][] physical={new int[]{49,50,51,52,53,54,55,56,57,48,189,222,220},new int[]{81,87,69,82,84,89,85,73,79,80,192,219},new int[]{65,83,68,70,71,72,74,75,76,187,186,221},new int[]{90,88,67,86,66,78,77,188,190,191,226}};
  string[][] expected={
   new string[]{"１|２|３|４|５|６|７|８|９|０|－|{None}|{None}","。|か|た|こ|さ|ら|ち|く|つ|，|、|{None}","う|し|て|け|せ|は|と|き|い|ん|{Backspace}|{Escape}","．|ひ|す|ふ|へ|め|そ|ね|ほ|・|{None}"},
   new string[]{"？|／|～|「|」|［|］|（|）|｛|｝|{None}|{None}","ぁ|え|り|ゃ|れ|ぱ|ぢ|ぐ|づ|ぴ|{None}|{None}","を|あ|な|ゅ|も|ば|ど|ぎ|ぽ|よ|{Backspace}|{Escape}","ぅ|ー|ろ|や|ぃ|ぷ|ぞ|ぺ|ぼ|゛|{None}"},
   new string[]{"？|／|～|「|」|［|］|（|）|｛|｝|{None}|{None}","゜|が|だ|ご|ざ|よ|に|る|ま|ぇ|{None}|{None}","ゔ|じ|で|げ|ぜ|み|お|の|ょ|っ|{Backspace}|{Escape}","{None}|び|ず|ぶ|べ|ぬ|ゆ|む|わ|ぉ|{None}"},
   new string[]{"！|”|＃|＄|％|＆|’|（|）|：|＝|～|｜","Ｑ|Ｗ|Ｅ|Ｒ|Ｔ|Ｙ|Ｕ|Ｉ|Ｏ|Ｐ|‘|｛","Ａ|Ｓ|Ｄ|Ｆ|Ｇ|Ｈ|Ｊ|Ｋ|Ｌ|＋|＊|｝","Ｚ|Ｘ|Ｃ|Ｖ|Ｂ|Ｎ|Ｍ|＜|＞|？|＿"}};
  for(int layer=0;layer<4;layer++)for(int row=0;row<4;row++){string[] cells=expected[layer][row].Split('|');for(int col=0;col<cells.Length;col++){Mapping m=nicola.Rows[physical[row][col]];string actual=layer==0?m.Normal:layer==1?m.Left:layer==2?m.Right:m.Shift;Check(actual==cells[col],"NICOLA layer "+layer+" row "+row+" column "+col);}}
  Check(Layout.Parse(YabLayout.Export(nicola)).Rows[187].Right=="っ","NICOLA TSV export reload preserves small tsu");
  reset();e.Layout=nicola;e.Left=32;e.Right=161;
  e.Handle(187,true,0);e.Handle(187,false,10);Check(result()=="ん","semicolon produces missing ん");
  reset();e.Layout=nicola;e.Handle(161,true,0);e.Handle(187,true,10);e.Handle(187,false,20);e.Handle(161,false,30);Check(result()=="っ","right thumb semicolon produces small tsu");
  reset();e.Layout=nicola;e.Handle(161,true,0);e.Handle(90,true,10);e.Handle(90,false,20);e.Handle(161,false,30);Check(events.Count==0,"無 is explicit no output, never normal fallback");
  reset();e.Layout=nicola;e.Handle(186,true,0);e.Handle(186,false,10);Check(result()=="tap8","後 sends Backspace not text");
  reset();e.Layout=nicola;e.Handle(221,true,0);e.Handle(221,false,10);Check(result()=="tap27","逃 sends Escape not text");
  reset();e.Layout=nicola;e.Pinky=true;e.Handle(49,true,0);e.Handle(49,true,70);e.Handle(49,false,80);Check(result()=="！|！","pinky shift symbols and repeat");e.Pinky=false;
  bad=false;try{YabLayout.Parse(YabLayout.ReadBuiltIn().Replace("ｌａ,ｅ", "unknown,ｅ"));}catch(FormatException){bad=true;}Check(bad,"unsupported YAB token rejected");
  bad=false;try{YabLayout.Parse(YabLayout.ReadBuiltIn().Replace("１,２,", "１,"));}catch(FormatException){bad=true;}Check(bad,"malformed YAB row rejected");
  bad=false;try{YabLayout.Parse("[未対応]\nA");}catch(FormatException){bad=true;}Check(bad,"unsupported YAB section rejected");
  Check(YabLayout.ReadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layouts","NICOLA.yab")).Rows[187].Normal=="ん","provided UTF-16LE YAB loads directly from file");
  Check(YabLayout.ReadFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layouts","NICOLA.tsv")).Rows[187].Shift=="＋","exported five-column TSV loads directly from file");
  log.AppendLine("TOTAL: "+count+" passed; no real keyboard hook or input injection used.");File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),log.ToString(),new UTF8Encoding(true));return 0;
 }catch(Exception ex){log.AppendLine(ex.ToString());File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test.txt"),log.ToString());return 1;}}
}
}
