#!/usr/bin/env python3
"""Verify secure/plain proxy package/auth parity for LWBridge 0.3.1."""
from __future__ import annotations
import argparse,hashlib,json
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_64
from capstone.x86_const import X86_OP_IMM,X86_OP_MEM,X86_REG_RIP
SEC_SHA='481c636b8d0fa9bf4b8f88cba77145054b33e3701ffa42737567b211859ac400'
PLAIN_SHA='c9a88ec449cc6a9929fb2f2800ebd443f9b9dcdac849e3d3e8da94281dea9794'
PUB=bytes.fromhex('00b0c33896bc67481de5fdc412d9b1549e70d736d9a5f7c1a3dbfeeccd8e135f7ae6887c7189787df7edaf33f52f3cac0849f1382b24e8d7aa6116152b425659')
class E(ValueError):pass
def req(v,m):
 if not v:raise E(m)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def load(p,expected):
 b=p.read_bytes();req(hashlib.sha256(b).hexdigest()==expected,f'hash drift: {p}');pe=pefile.PE(data=b,fast_load=False);md=Cs(CS_ARCH_X86,CS_MODE_64);md.detail=True;return b,pe,md

def ins_at(b,pe,md,rva):
 o=pe.get_offset_from_rva(rva);return next(md.disasm(b[o:o+15],pe.OPTIONAL_HEADER.ImageBase+rva))
def direct_target(b,pe,md,rva):
 i=ins_at(b,pe,md,rva);req(i.mnemonic=='call' and i.operands and i.operands[0].type==X86_OP_IMM,f'{rva:#x} not direct call');return int(i.operands[0].imm-pe.OPTIONAL_HEADER.ImageBase)
def rip_target(b,pe,md,rva):
 i=ins_at(b,pe,md,rva)
 for op in i.operands:
  if op.type==X86_OP_MEM and op.mem.base==X86_REG_RIP:return int(i.address+i.size+op.mem.disp-pe.OPTIONAL_HEADER.ImageBase)
 raise E(f'{rva:#x} no RIP target')
def call_branch_shape(b,pe,md,s=0x1c0a0,e=0x1d46f):
 o=pe.get_offset_from_rva(s);calls=[];branches=[]
 for i in md.disasm(b[o:o+e-s],pe.OPTIONAL_HEADER.ImageBase+s):
  if i.operands and i.operands[0].type==X86_OP_IMM:
   t=int(i.operands[0].imm-pe.OPTIONAL_HEADER.ImageBase)
   if i.mnemonic=='call':calls.append((int(i.address-pe.OPTIONAL_HEADER.ImageBase),t))
   elif i.mnemonic.startswith('j'):branches.append((int(i.address-pe.OPTIONAL_HEADER.ImageBase),i.mnemonic,t))
 return calls,branches

def inspect(sec,plain):
 sb,sp,sm=load(sec,SEC_SHA);pb,pp,pm=load(plain,PLAIN_SHA)
 sites={0x1c447:0x1b300,0x1c45b:0x1bd80,0x1c8e2:0x3f8e0,0x1cafd:0x1b910,0x1cc14:0x40a70,0x1cc3a:0x3d260,0x1cc54:0x125c0,0x15a3e:0x16c10,0x1618a:0x3f350}
 for r,t in sites.items():
  req(direct_target(sb,sp,sm,r)==t,f'secure call drift {r:#x}');req(direct_target(pb,pp,pm,r)==t,f'plain call drift {r:#x}')
 sc,sbr=call_branch_shape(sb,sp,sm);pc,pbr=call_branch_shape(pb,pp,pm);req(sc==pc,'outer loader direct-call sequence differs');req(sbr==pbr,'outer loader branch sequence differs');req(len(sc)==105 and len(sbr)==144,'outer loader shape count drifted')
 req(rip_target(sb,sp,sm,0x15a08)==0x8f170,'secure source global drift');req(rip_target(pb,pp,pm,0x15a08)==0x90170,'plain source global drift')
 req(b'LWAT1' not in sb and b'LWAT1' not in pb,'LWAT1 unexpectedly present in proxy');req(b'LWAT2' in sb and b'LWAT2' in pb,'LWAT2 missing')
 for b,pe in [(sb,sp),(pb,pp)]:
  o=pe.get_offset_from_rva(0x70040);req(b[o:o+64]==PUB,'hardcoded ECDSA public key drifted')
  req(b'ECS2' not in b and b'ECK2' not in b,'private ECC blob magic unexpectedly present')
 return {'findingId':'LWB-R8-118','date':'2026-09-27','status':'RECOVERED NEGATIVE/BYPASS CLASSIFICATION','secureSha256':SEC_SHA,'plainSha256':PLAIN_SHA,'outerLoader':{'range':'0x1C0A0-0x1D46F','directCalls':105,'branches':144,'semanticShapeEqual':True,'criticalCalls':{hex(k):hex(v) for k,v in sites.items()}},'sourceGlobals':{'secure':'0x8F170','plain':'0x90170'},'authorization':{'proxyTicketMagic':'LWAT2 only','LWAT1Present':False,'signaturePublicKeyXYHex':PUB.hex(),'privateBlobMagicPresent':False},'conclusion':'plain proxy does not bypass envelope/package decrypt; secure/plain package/auth control flow is identical at recovered loader boundary','limits':['does not inspect protected consumer body 0x3F8E0-0x40A6D','does not patch binaries or contact auth service','Map remains NOT WORKING']}

def main():
 ap=argparse.ArgumentParser();ap.add_argument('secure',type=Path);ap.add_argument('plain',type=Path);ap.add_argument('--output',type=Path);a=ap.parse_args()
 try:r=inspect(a.secure,a.plain)
 except (E,OSError,StopIteration) as e:ap.error(str(e))
 s=json.dumps(r,indent=2)+'\n'
 if a.output:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(s,encoding='utf-8')
 print(s,end='')
if __name__=='__main__':raise SystemExit(main())
