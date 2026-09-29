/** Preserve all definitions when unlinked-object extraction merges a symbol. */
import DependencyIds

int definitionEnd(FunctionDeclarationEntry definition) {
  if exists(definition.getBlock()) then result = definition.getBlock().getLocation().getEndLine()
  else result = definition.getLocation().getEndLine()
}

from Function function, FunctionDeclarationEntry definition
where definition = function.getDefinition()
select functionId(function) as function_id,
  filePath(definition.getLocation().getFile()) as file,
  definition.getLocation().getStartLine() as line,
  definitionEnd(definition) as end_line
