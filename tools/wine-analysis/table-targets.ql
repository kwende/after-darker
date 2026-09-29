/** Structural dispatch candidates from field initializers and assignments.
 * This is flow-insensitive: a field can receive different targets in different
 * driver instances. Retain the candidate label, not a claim of runtime choice.
 */
import DependencyIds

Expr assignedExpression(Variable variable) {
  result = variable.getInitializer().getExpr()
  or
  exists(ClassAggregateLiteral aggregate | result = aggregate.getAFieldExpr(variable.(Field)))
  or
  exists(AssignExpr assignment |
    assignment.getLValue().(VariableAccess).getTarget() = variable and
    result = assignment.getRValue()
  )
}

// Follow the expression's value, not every descendant. In particular,
// dib.funcs->stretch_row refers to that slot, not every function in dib.funcs.
Expr valueOrigin(Expr expression) {
  result = expression.getUnconverted() and
  (result instanceof FunctionAccess or result instanceof VariableAccess)
  or
  exists(ConditionalExpr conditional |
    conditional = expression.getUnconverted() and
    (result = valueOrigin(conditional.getThen()) or result = valueOrigin(conditional.getElse()))
  )
  or
  exists(AddressOfExpr address |
    address = expression.getUnconverted() and result = valueOrigin(address.getOperand())
  )
}

Function assignedFunction(Variable variable) {
  result = valueOrigin(assignedExpression(variable)).(FunctionAccess).getTarget()
  or
  exists(Variable source |
    source = valueOrigin(assignedExpression(variable)).(VariableAccess).getTarget() and
    result = assignedFunction(source)
  )
}

from ExprCall call, Function callee
where
  exists(call.getEnclosingFunction()) and
  callee = assignedFunction(call.getExpr().(VariableAccess).getTarget())
select callId(call) as call_id, functionId(callee) as target,
  "structural_candidate" as evidence
