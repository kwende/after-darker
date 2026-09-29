/** Stable, source-linked identities for the pinned extraction. */
import cpp

string filePath(File file) {
  if exists(file.getRelativePath()) then result = file.getRelativePath()
  else result = "@external/" + file.getBaseName()
}

string locationFile(Location location) {
  if exists(location.getFile()) then result = filePath(location.getFile())
  else result = "@builtin"
}

int locationLine(Location location) {
  if exists(location.getStartLine()) then result = location.getStartLine() else result = 0
}

int locationColumn(Location location) {
  if exists(location.getStartColumn()) then result = location.getStartColumn() else result = 0
}

string locationKey(Location location) {
  result = locationFile(location) + ":" +
    locationLine(location).toString() + ":" + locationColumn(location).toString()
}

// Declarations can appear in many translation units. Bind one location rather
// than forming a Cartesian product of independent getLocation() expressions.
Location preferredLocation(Function function) {
  if exists(function.getDefinitionLocation()) then result = function.getDefinitionLocation()
  else result = function.getLocation()
}

Location functionLocation(Function function) {
  result = preferredLocation(function) and
  locationKey(result) = min(Location candidate |
    candidate = preferredLocation(function) | locationKey(candidate))
}

string functionId(Function function) {
  if function instanceof BuiltInFunction and not exists(function.getDefinition())
  then result = function.getName() + "@builtin"
  else result = function.getQualifiedName() + "@" + locationKey(functionLocation(function))
}

string functionFile(Function function) {
  if function instanceof BuiltInFunction and not exists(function.getDefinition())
  then result = "@builtin"
  else result = locationFile(functionLocation(function))
}

int functionLine(Function function) {
  if function instanceof BuiltInFunction and not exists(function.getDefinition()) then result = 0
  else result = locationLine(functionLocation(function))
}

string callId(Call call) {
  result = filePath(call.getLocation().getFile()) + ":" +
    call.getLocation().getStartLine().toString() + ":" +
    call.getLocation().getStartColumn().toString() + ":" +
    functionId(call.getEnclosingFunction()) + ":" + callDescription(call)
}

string callDescription(Call call) {
  if call instanceof ExprCall
  then result = "call through " + call.(ExprCall).getExpr().toString()
  else result = call.toString()
}

int definitionFlag(Function function) {
  if exists(function.getDefinition()) then result = 1 else result = 0
}

int endLine(Function function) {
  if exists(function.getBlock()) then
    result = max(BlockStmt block | block = function.getBlock() | block.getLocation().getEndLine())
  else if exists(functionLocation(function).getEndLine())
  then result = functionLocation(function).getEndLine()
  else result = 0
}

string callKind(Call call) {
  if exists(call.getTarget()) then result = "direct" else result = "indirect"
}
