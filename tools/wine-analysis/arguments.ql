/** Preserve static arguments so later analysis can inspect branch selectors. */
import DependencyIds

string constantValue(Expr argument) {
  if exists(argument.getValue()) then result = argument.getValue() else result = ""
}

from Call call, Expr argument, int argumentIndex
where exists(call.getEnclosingFunction()) and argument = call.getArgument(argumentIndex)
select callId(call) as call_id, argumentIndex as position,
  argument.toString() as expression, argument.getType().toString() as type,
  constantValue(argument) as constant
