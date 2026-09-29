/** Named calls only, independently queryable without indirect data-flow analysis. */
import DependencyIds

from Call call
where exists(call.getEnclosingFunction()) and exists(call.getTarget())
select callId(call) as call_id, functionId(call.getTarget()) as target, "direct" as evidence
