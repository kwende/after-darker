/** Candidate targets. Indirect resolution is an approximation, not runtime proof. */
import DependencyIds
import semmle.code.cpp.ir.dataflow.ResolveCall

string evidence(Call call, Function target) {
  if target = call.getTarget() then result = "direct" else result = "codeql_candidate"
}

from Call call, Function callee
where
  exists(call.getEnclosingFunction()) and
  (callee = call.getTarget() or callee = resolveCall(call))
select callId(call) as call_id, functionId(callee) as target,
  evidence(call, callee) as evidence
