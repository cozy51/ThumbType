using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ThumbShiftPortable {
// Importer for the four romaji sections present in the supplied NICOLA.yab.
// Not a general-purpose Yamabuki interpreter. Unknown syntax is rejected.
public static class YabLayout {
 static readonly int[][] Keys={
  new int[]{49,50,51,52,53,54,55,56,57,48,189,222,220},
  new int[]{81,87,69,82,84,89,85,73,79,80,192,219},
  new int[]{65,83,68,70,71,72,74,75,76,187,186,221},
  new int[]{90,88,67,86,66,78,77,188,190,191,226}};
 static readonly string[] Sections={"ローマ字シフト無し","ローマ字左親指シフト","ローマ字右親指シフト","ローマ字小指シフト"};
 static readonly Dictionary<string,string> Kana=MakeKana();
 static Dictionary<string,string> MakeKana(){var d=new Dictionary<string,string>();string[] pairs=("ka=か ta=た ko=こ sa=さ ra=ら ti=ち ku=く tu=つ u=う si=し te=て ke=け se=せ ha=は to=と ki=き i=い nn=ん hi=ひ su=す fu=ふ he=へ me=め so=そ ne=ね ho=ほ la=ぁ e=え ri=り lya=ゃ re=れ pa=ぱ di=ぢ gu=ぐ du=づ pi=ぴ wo=を a=あ na=な lyu=ゅ mo=も ba=ば do=ど gi=ぎ po=ぽ yo=よ lu=ぅ ro=ろ ya=や li=ぃ pu=ぷ zo=ぞ pe=ぺ bo=ぼ ga=が da=だ go=ご za=ざ ni=に ru=る ma=ま le=ぇ vu=ゔ zi=じ de=で ge=げ ze=ぜ mi=み o=お no=の lyo=ょ ltu=っ bi=び zu=ず bu=ぶ be=べ nu=ぬ yu=ゆ mu=む wa=わ lo=ぉ").Split(' ');foreach(string pair in pairs){var f=pair.Split('=');d.Add(f[0],f[1]);}return d;}
 public static string Decode(string cell){string value=cell.Trim();
  if(value.Length>=2&&value[0]=='\''&&value[value.Length-1]=='\'')return value.Substring(1,value.Length-2);
  if(value=="無")return "{None}";if(value=="後")return "{Backspace}";if(value=="逃")return "{Escape}";
  string roman=value.Normalize(NormalizationForm.FormKC),kana;if(Kana.TryGetValue(roman,out kana))return kana;
  switch(roman){case ".":return "。";case ",":return "、";case "/":return "・";case "-":return "ー";case "[":return "「";case "]":return "」";}
  // The supplied file's remaining unquoted outputs are single symbols/digits.
  if(value.Length==1&&!char.IsControl(value[0])&&!(roman.Length==1&&char.IsLower(roman[0])))return value;
  throw new FormatException("未対応のやまぶき配列トークン: "+value);
 }
 static string[] SplitRow(string row){var cells=new List<string>();var cell=new StringBuilder();bool quoted=false;foreach(char c in row){if(c=='\'')quoted=!quoted;if(c==','&&!quoted){cells.Add(cell.ToString());cell.Clear();}else cell.Append(c);}if(quoted)throw new FormatException("引用符が閉じていません。");cells.Add(cell.ToString());return cells.ToArray();}
 public static Layout Parse(string text){var data=new Dictionary<string,List<string[]>>();List<string[]> current=null;
  foreach(string raw in text.Replace("\r","").Split('\n')){string line=raw.Trim().TrimStart('\uFEFF');if(line.Length==0||line.StartsWith(";"))continue;
   if(line.StartsWith("[")&&line.EndsWith("]")){string section=line.Substring(1,line.Length-2);if(Array.IndexOf(Sections,section)<0)throw new FormatException("未対応のセクション: "+section);if(data.ContainsKey(section))throw new FormatException("セクションが重複しています。");current=new List<string[]>();data.Add(section,current);continue;}
   if(current==null)throw new FormatException("セクションの外に配列行があります。");int row=current.Count;if(row>=Keys.Length)throw new FormatException("配列行が多すぎます。");var cells=SplitRow(line);if(cells.Length!=Keys[row].Length)throw new FormatException("配列の列数がJISキーボードと一致しません。");for(int i=0;i<cells.Length;i++)cells[i]=Decode(cells[i]);current.Add(cells);
  }
  foreach(string section in Sections)if(!data.ContainsKey(section)||data[section].Count!=4)throw new FormatException("4行のセクションが必要です: "+section);
  var layout=new Layout();for(int row=0;row<Keys.Length;row++)for(int col=0;col<Keys[row].Length;col++)layout.Rows.Add(Keys[row][col],new Mapping(data[Sections[0]][row][col],data[Sections[1]][row][col],data[Sections[2]][row][col]){Shift=data[Sections[3]][row][col]});return layout;
 }
 public static string ReadBuiltIn(){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("ThumbType.NICOLA.yab")){if(s==null)throw new InvalidOperationException("内蔵NICOLA配列がありません。");using(var reader=new StreamReader(s,Encoding.UTF8,true))return reader.ReadToEnd();}}
 public static Layout ReadFile(string path){string text;try{text=File.ReadAllText(path,new UTF8Encoding(false,true));}catch(DecoderFallbackException){text=File.ReadAllText(path,Encoding.GetEncoding(932,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback));}return Path.GetExtension(path).Equals(".yab",StringComparison.OrdinalIgnoreCase)?Parse(text):Layout.Parse(text);}
 public static string Export(Layout layout){var text=new StringBuilder("# ThumbType / provided NICOLA.yab / JIS 109\n# Key\tNormal\tLeft\tRight\tPinkyShift\n");foreach(int[] row in Keys)foreach(int key in row){Mapping m=layout.Rows[key];text.Append(((System.Windows.Forms.Keys)key).ToString()).Append('\t').Append(m.Normal).Append('\t').Append(m.Left).Append('\t').Append(m.Right).Append('\t').Append(m.Shift).Append('\n');}return text.ToString();}
}
}
