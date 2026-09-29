-- Direct screensaver imports versus additional imports in reachable helpers.
SELECT a.module,
 count(DISTINCT CASE WHEN c.role='screensaver' THEN a.id END) AS direct_module_imports,
 count(DISTINCT a.id) AS including_reachable_helpers
FROM apis a JOIN api_consumers c ON c.api=a.id
WHERE c.reachable=1 GROUP BY a.module ORDER BY including_reachable_helpers DESC;

-- Root mapping gaps: distinguish proprietary helpers, constant/stub exports,
-- and Wine declarations/aliases that need further mapping.
SELECT a.identity,a.name,a.wine_target,a.export_kind
FROM apis a WHERE EXISTS(SELECT 1 FROM api_consumers c WHERE c.api=a.id AND c.reachable=1)
AND NOT EXISTS(SELECT 1 FROM roots r WHERE r.api=a.id)
ORDER BY a.module,a.ordinal;

-- Concrete dependencies of StretchBlt, with source links available from metadata.
-- Change the API identity or mode to compare other roots.
SELECT f.name,f.file,f.line,f.has_definition,r.depth
FROM reachability r JOIN functions f ON f.id=r.function_id JOIN apis a ON a.id=r.api
WHERE a.identity='GDI!#35' AND r.mode='direct'
ORDER BY r.depth,f.file,f.line;

-- Unknown dispatch points reachable from StretchBlt. A missing target is work
-- to investigate, not evidence of an empty implementation.
SELECT DISTINCT c.file,c.line,c.expression
FROM unresolved_calls c JOIN reachability r ON r.function_id=c.caller
JOIN apis a ON a.id=r.api WHERE a.identity='GDI!#35' AND r.mode='candidates';

-- Shared dependencies of APIs we have not registered/implemented. These counts
-- are source reachability and can include configuration/UI/error branches.
SELECT f.name,f.file,count(DISTINCT r.api) AS missing_api_roots
FROM reachability r JOIN functions f ON f.id=r.function_id JOIN apis a ON a.id=r.api
WHERE r.mode='direct' AND f.has_definition=1 AND a.implementation!='guarded_handler'
AND EXISTS(SELECT 1 FROM api_consumers c WHERE c.api=a.id AND c.reachable=1)
GROUP BY f.id HAVING count(DISTINCT r.api)>=5 ORDER BY missing_api_roots DESC LIMIT 50;

-- State dependencies under the stretching algorithm, independently of calls.
SELECT DISTINCT s.owner_type,s.member,s.type,s.kind
FROM state_access s JOIN functions f ON f.id=s.function_id
WHERE f.name='stretch_bitmapinfo' ORDER BY s.owner_type,s.member;

-- Extraction failures must accompany any claims about graph completeness.
SELECT * FROM extraction_units WHERE exit_code!=0 OR error_count!=0;

-- Constants passed to a handle adapter allow future branch specialization.
SELECT c.file,c.line,arg.position,arg.expression,arg.type,arg.constant
FROM calls c JOIN targets t ON t.call_id=c.id JOIN functions f ON f.id=t.target
JOIN arguments arg ON arg.call_id=c.id WHERE f.name='K32WOWHandle32';

-- Definitions that an unlinked source index merged into one symbol. This is
-- a warning against interpreting their union as a precise DLL execution path.
SELECT f.name,d.file,d.line,d.end_line FROM definitions d JOIN functions f ON f.id=d.function_id
WHERE d.function_id IN (SELECT function_id FROM definitions GROUP BY function_id HAVING count(*)>1)
ORDER BY f.name,d.file,d.line;

-- Missing API frequency is a triage aid; startup/error/dialog imports may never
-- execute in our color playback configuration.
SELECT a.identity,a.name,a.implementation,count(DISTINCT c.sha256) AS consumers
FROM apis a JOIN api_consumers c ON c.api=a.id WHERE c.role='screensaver'
AND a.spec_file IS NOT NULL AND a.implementation!='guarded_handler'
GROUP BY a.id ORDER BY consumers DESC;
