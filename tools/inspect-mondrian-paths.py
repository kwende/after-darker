"""Reproduce the static Mondrian investigation; never execute the input.

This is a hash-specific research script, not the future general NE parser.
All generated listings and metadata stay under ignored artifacts/mondrian/.
Run from the repository root; see docs/research/mondrian-static-analysis.md.
"""
import argparse
import sys, struct, json, hashlib, csv
from pathlib import Path
sys.path.insert(0, str(Path('artifacts/mondrian/python').resolve()))
from capstone import Cs, CS_ARCH_X86, CS_MODE_16

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("input", type=Path, help="Local bring-your-own Mondrian.ad")
args = parser.parse_args()
b = args.input.read_bytes()
expected_hash = "781979da1a6a6fdf99eebec4dab67e7a645bfc8787be1671e20f13a8ca6b1aed"
if hashlib.sha256(b).hexdigest() != expected_hash:
    parser.error("Unsupported artifact: this analysis is specific to the documented SHA-256.")
output = Path("artifacts/mondrian")
output.mkdir(parents=True, exist_ok=True)
u16=lambda o:struct.unpack_from('<H',b,o)[0]
u32=lambda o:struct.unpack_from('<I',b,o)[0]
ne=u32(0x3c)
def pstr(o): return b[o+1:o+1+b[o]].decode('ascii',errors='replace')
names={}
for o in [ne+u16(ne+0x26),u32(ne+0x2c)]:
    while b[o]:
        text=pstr(o); ordinal=u16(o+1+b[o]); names.setdefault(ordinal,[]).append(text); o+=b[o]+3
entries={}; ordinal=1; o=ne+u16(ne+4)
while b[o]:
    count,kind=b[o:o+2]; o+=2
    for _ in range(count):
        if kind==0: pass
        elif kind==255:
            flags=b[o]; segment=b[o+3]; offset=u16(o+4);o+=6
            entries[ordinal]={'segment':segment,'offset':offset,'flags':flags,'names':names.get(ordinal,[])}
        else:
            flags=b[o];offset=u16(o+1);o+=3
            entries[ordinal]={'segment':kind,'offset':offset,'flags':flags,'names':names.get(ordinal,[])}
        ordinal+=1
modulebase=ne+u16(ne+0x28); importbase=ne+u16(ne+0x2a)
modules=[pstr(importbase+u16(modulebase+i*2)) for i in range(u16(ne+0x1e))]
apis={(r['Library'],int(r['Ordinal'])):r['Name'] for r in csv.DictReader(open('docs/research/ad-imports.csv',encoding='utf-8')) if r['Ordinal']}
segments=[]; relocations=[]
for i in range(u16(ne+0x1c)):
    sector,size,flags,minimum=struct.unpack_from('<4H',b,ne+u16(ne+0x22)+i*8)
    start=sector<<u16(ne+0x32);size=size or 65536
    s={'segment':i+1,'file_offset':start,'file_size':size,'minimum':minimum,'flags':flags}
    segments.append(s)
    if sector and flags&0x100:
        for j in range(u16(start+size)):
            rpos=start+size+2+j*8
            at,rf,source,t1,t2=struct.unpack_from('<BBHHH',b,rpos); typ=rf&3
            r={'segment':i+1,'address_type':at,'flags':rf,'head':source,'target1':t1,'target2':t2,'sites':[]}
            if typ==0:
                target=entries[t2] if t1&255==255 else {'segment':t1&255,'offset':t2}
                r['target']=target;r['symbol']=f"S{target['segment']}:{target['offset']:04X}"
            elif typ in (1,2):
                module=modules[t1-1]; name=apis.get((module,t2),f'#{t2}') if typ==1 else pstr(importbase+t2)
                r['symbol']=module+'!'+name
            else: r['symbol']=f'OS_FIXUP({t1},{t2})'
            if typ!=3:
                site=source;seen=set()
                while site!=0xffff:
                    assert site not in seen and site+2<=size,(i,site)
                    seen.add(site);r['sites'].append(site)
                    if rf&4: break
                    site=u16(start+site)
            else:r['sites']=[source]
            relocations.append(r)
summary={'sha256':hashlib.sha256(b).hexdigest(),'ne_offset':ne,'flags':u16(ne+0xc),
         'auto_data':u16(ne+0xe),'heap':u16(ne+0x10),'stack':u16(ne+0x12),
         'startup_ip':u16(ne+0x14),'startup_segment':u16(ne+0x16),
         'entries':entries,'names':names,'modules':modules,'segments':segments,'relocations':relocations}
Path('artifacts/mondrian/metadata.json').write_text(json.dumps(summary,indent=2))
md=Cs(CS_ARCH_X86,CS_MODE_16);md.detail=True
patched={}; all_annotations={}
for s in segments:
    if s['flags']&1:continue
    code=bytearray(b[s['file_offset']:s['file_offset']+s['file_size']]); annotations={}
    for r in relocations:
        if r['segment']!=s['segment']:continue
        for site in r['sites']:
            annotations[site]=r['symbol']
            if r['flags']&3==0:
                target=r['target']
                # Substitute NE segment numbers for display only; these are NOT selectors.
                if r['address_type']==3:struct.pack_into('<HH',code,site,target['offset'],target['segment'])
                elif r['address_type']==2:struct.pack_into('<H',code,site,target['segment'])
                elif r['address_type']==5:struct.pack_into('<H',code,site,target['offset'])
    lines=[]
    patched[s['segment']]=bytes(code);all_annotations[s['segment']]=annotations
    # Linear decode is a navigation aid; use entry points/branches to distinguish code from data.
    md.skipdata=True
    for ins in md.disasm(bytes(code),0):
        notes=[f'{p-ins.address:+}:{annotations[p]}' for p in range(ins.address,ins.address+ins.size) if p in annotations]
        lines.append(f"S{s['segment']}:{ins.address:04X}  {ins.mnemonic:8} {ins.op_str:32} {'; '+', '.join(notes) if notes else ''}")
    Path(f"artifacts/mondrian/segment-{s['segment']}.asm").write_text('\n'.join(lines))
print(json.dumps({k:v for k,v in summary.items() if k!='relocations'},indent=2))
print('Relocation types:', {t:sum(r['flags']&3==t for r in relocations) for t in range(4)})

# Decode reachable blocks from actual entries, not across switch-table data.
from capstone import CS_GRP_JUMP, CS_GRP_CALL, CS_GRP_RET, CS_GRP_INT
from capstone.x86 import X86_OP_IMM
from iced_x86 import Decoder, Formatter, FormatterSyntax
md.skipdata=False
switch=list(struct.unpack_from('<13H',patched[1],0x1af))
print('Dispatcher table:',dict(enumerate(f'S1:{x:04X}' for x in switch)))
roots={(e['segment'],e['offset']) for e in entries.values()}|{(4,0),(1,0)}
functions={}; comparisons=[]
def follow_function(root):
    seg,start=root;pending=[start];seen=set();calls=[];imports=[];interrupts=[];unknown=[];lines=[]
    while pending:
        pc=pending.pop()
        while pc not in seen:
            seen.add(pc)
            code=patched[seg];ins=next(md.disasm(code[pc:],pc,count=1),None)
            if ins is None: raise ValueError(('Undecodable instruction',seg,pc))
            independent=Decoder(16,code[pc:],ip=pc).decode()
            if independent.is_invalid: raise ValueError(('Invalid Iced instruction',seg,pc))
            if independent.len != ins.size: comparisons.append((seg,pc,ins.size,independent.len))
            # Use Iced formatting to avoid Capstone 5's CWD/CDQ naming ambiguity.
            display=Formatter(FormatterSyntax.INTEL).format(independent)
            notes=[all_annotations[seg][p] for p in range(pc,pc+ins.size) if p in all_annotations[seg]]
            lines.append((pc,f'S{seg}:{pc:04X}  {display:<45} '+('; '+', '.join(notes) if notes else '')))
            following=pc+ins.size
            if ins.group(CS_GRP_CALL):
                targets=[x for x in notes if x.startswith('S') and ':' in x]
                external=[x for x in notes if '!' in x]
                if targets:
                    for x in targets:
                        a,z=x[1:].split(':');target=(int(a),int(z,16));calls.append({'site':pc,'target':target});roots.add(target)
                elif external: imports.extend({'site':pc,'target':x} for x in external)
                elif ins.mnemonic=='call' and ins.operands[0].type==X86_OP_IMM:
                    target=(seg,ins.operands[0].imm);calls.append({'site':pc,'target':target});roots.add(target)
                else:unknown.append({'site':pc,'instruction':display})
            if ins.group(CS_GRP_INT):interrupts.append({'site':pc,'instruction':display})
            if ins.group(CS_GRP_RET):break
            if ins.group(CS_GRP_JUMP) or ins.mnemonic.startswith('loop'):
                if ins.operands[0].type==X86_OP_IMM:pending.append(ins.operands[0].imm)
                elif (seg,pc)==(1,0x1aa):pending.extend(switch)
                else:unknown.append({'site':pc,'instruction':display})
                if ins.mnemonic in ('jmp','ljmp'):break
            pc=following
    Path(f'artifacts/mondrian/function-{seg}-{start:04X}.asm').write_text('\n'.join(v for _,v in sorted(lines)))
    return {'instructions':len(seen),'calls':calls,'imports':imports,'interrupts':interrupts,'unresolved_transfers':unknown}
while roots-set(functions):
    root=min(roots-set(functions)); functions[root]=follow_function(root)
print('Decoded functions:',len(functions),'instruction length disagreements:',comparisons)
Path('artifacts/mondrian/callgraph.json').write_text(json.dumps({f'S{s}:{o:04X}':v for (s,o),v in functions.items()},indent=2))
for root,info in functions.items():
    if info['imports'] or info['interrupts'] or info['unresolved_transfers']:
        print(root, {k:v for k,v in info.items() if k not in ['instructions','calls']})

# These invariants reproduce structural findings, not execution or pixel fidelity.
assert len(functions) == 30
assert not comparisons, "The independent decoders disagree on instruction boundaries."
assert not any(f["unresolved_transfers"] for f in functions.values())
assert sum(len(f["interrupts"]) for f in functions.values()) == 3
all_imports = {r["symbol"] for r in relocations if "!" in r["symbol"]}
reachable_imports = {i["target"] for f in functions.values() for i in f["imports"]}
assert len(all_imports) == 17 and len(reachable_imports) == 14
assert all_imports - reachable_imports == {
    "KERNEL!FatalExit", "KERNEL!FatalAppExit", "KERNEL!OutputDebugString"
}
print("Structural checks passed; lifecycle branch interpretation is documented separately.")
