using System;
using System.IO;
using System.Text;
using System.Xml;

namespace ThumbShiftPortable {
// Only the last successfully loaded layout is persisted, never running state.
public sealed class LayoutSettings {
 readonly string file,root;
 public LayoutSettings(string fileName){file=Path.GetFullPath(fileName);root=Path.GetDirectoryName(file);}
 public string Load(){if(!File.Exists(file))return "";
  var options=new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null};
  var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(file,options))doc.Load(reader);
  var element=doc.DocumentElement;if(element==null||element.Name!="ThumbShiftPortable"||element.GetAttribute("version")!="1"||!element.HasAttribute("layout"))throw new FormatException("設定ファイルの形式が正しくありません。");
  string saved=element.GetAttribute("layout");return saved.Length==0?"":Path.GetFullPath(Path.Combine(root,saved));
 }
 public void Save(string layout){string saved="";if(!string.IsNullOrEmpty(layout)){
   string full=Path.GetFullPath(layout);string prefix=root+Path.DirectorySeparatorChar;
   saved=full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)?full.Substring(prefix.Length):full;
  }
  string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{var options=new XmlWriterSettings{Encoding=new UTF8Encoding(true),Indent=true};using(var writer=XmlWriter.Create(temporary,options)){writer.WriteStartElement("ThumbShiftPortable");writer.WriteAttributeString("version","1");writer.WriteAttributeString("layout",saved);writer.WriteEndElement();}
   if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
}
