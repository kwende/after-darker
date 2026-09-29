/** Functions, definitions and source locations, including declaration-only boundaries. */
import DependencyIds

from Function function
select functionId(function) as id, function.getName() as name,
  functionFile(function) as file,
  functionLine(function) as line,
  endLine(function) as end_line,
  definitionFlag(function) as has_definition,
  function.toString() as signature
