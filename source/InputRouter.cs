using System.Collections.Generic;

namespace ThumbShiftPortable {
// Every physical press/release pair stays on the same side of the filter.
public sealed class InputRouter {
 readonly Engine engine;readonly HashSet<int> consumed=new HashSet<int>(),passed=new HashSet<int>(),cancelled=new HashSet<int>();bool enabled;
 public InputRouter(Engine value){engine=value;}
 public void SetEnabled(bool value){if(enabled!=value){foreach(int key in consumed)cancelled.Add(key);engine.Reset();enabled=value;}}
 public void Tick(long now,bool eligible){SetEnabled(eligible);if(enabled)engine.Tick(now);}
 public bool Handle(int key,bool down,long now,bool eligible){SetEnabled(eligible);
  if(!down){passed.Remove(key);bool discard=cancelled.Remove(key);if(consumed.Remove(key)){if(enabled&&!discard)engine.Handle(key,false,now);return true;}return false;}
  if(passed.Contains(key))return false;
  if(consumed.Contains(key)){if(enabled&&!cancelled.Contains(key))engine.Handle(key,true,now);return true;}
  if(enabled&&engine.Handle(key,true,now)){consumed.Add(key);return true;}
  passed.Add(key);return false;
 }
}
}
