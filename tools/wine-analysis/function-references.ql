/** Address-taken functions are important evidence for dispatch tables/callbacks. */
import DependencyIds

string enclosingFunction(FunctionAccess reference) {
  if exists(reference.getEnclosingFunction())
  then result = functionId(reference.getEnclosingFunction())
  else result = ""
}

from FunctionAccess reference
select functionId(reference.getTarget()) as target,
  filePath(reference.getLocation().getFile()) as file,
  reference.getLocation().getStartLine() as line,
  enclosingFunction(reference) as enclosing_function
