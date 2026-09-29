/** Field/global accesses expose dependencies absent from a function-only graph.
 * This is an access inventory, not proof of read/write effects or pointer lifetime.
 */
import DependencyIds

string variableKind(Variable variable) {
  if variable instanceof Field then result = "field" else result = "global"
}

string ownerType(Variable variable) {
  if variable instanceof Field then result = variable.(Field).getDeclaringType().toString()
  else result = ""
}

from VariableAccess access, Variable variable
where
  variable = access.getTarget() and
  exists(access.getEnclosingFunction()) and
  (variable instanceof Field or variable instanceof GlobalVariable)
select functionId(access.getEnclosingFunction()) as function_id,
  filePath(access.getLocation().getFile()) as file,
  access.getLocation().getStartLine() as line,
  variable.getName() as member,
  variable.getType().toString() as type,
  variableKind(variable) as kind,
  ownerType(variable) as owner_type
