/** Keep every call site, even when no target can be resolved. */
import DependencyIds

from Call call
where exists(call.getEnclosingFunction())
select callId(call) as id, functionId(call.getEnclosingFunction()) as caller,
  filePath(call.getLocation().getFile()) as file,
  call.getLocation().getStartLine() as line,
  call.getLocation().getStartColumn() as column,
  callDescription(call) as expression,
  callKind(call) as kind
