import test from "node:test";
import assert from "node:assert/strict";
import {readFile} from "node:fs/promises";
import vm from "node:vm";
import {Envelope,defaults,normalizeSettings} from "../../chrome-extension/mix.js";

test("audio graph routes every source, ducks music only, releases and survives failed capture",async()=>{
 const nodes=[],streams=[],intervals=[];let listener,now=0,fail=false;
 class Node {
  constructor(kind){this.kind=kind;this.connections=[];this.disconnected=false;this.gain={value:1,setTargetAtTime:v=>this.gain.value=v};nodes.push(this);}
  connect(n){this.connections.push(n);return n;} disconnect(){this.disconnected=true;}
  getFloatTimeDomainData(a){a.fill(this.signal??.1);}
 }
 class Context {
  constructor(){this.currentTime=0;this.destination={kind:"destination"};}
  async resume(){} async close(){this.closed=true;}
  createMediaStreamSource(){return new Node("input");}createAnalyser(){return new Node("analyser");}createGain(){return new Node("gain");}
 }
 const sandbox={Envelope,defaults,normalizeSettings,Float32Array,AudioContext:Context,performance:{now:()=>now},
  setInterval:fn=>intervals.push(fn),
  navigator:{mediaDevices:{getUserMedia:async()=>{if(fail)throw Error("denied");const track={stopped:false,stop(){this.stopped=true;},addEventListener(_,fn){this.end=fn;}},stream={getTracks:()=>[track],getAudioTracks:()=>[track]};streams.push(stream);return stream;}}},
  chrome:{runtime:{id:"test",onMessage:{addListener:fn=>listener=fn}}}
 };
 vm.runInNewContext((await readFile(new URL("../../chrome-extension/offscreen.js",import.meta.url),"utf8")).replace(/^import .*;\r?\n/,""),sandbox);
 const send=(type,data={})=>new Promise(resolve=>listener({target:"offscreen",type,...data},{id:"test"},resolve));
 const ok=async(type,data)=>{const r=await send(type,data);assert.equal(r.ok,true,r.error);return r.state;};
 const step=n=>{for(let i=0;i<n;i++){now+=50;intervals[0]();}};
 await ok("ADD",{tabId:1,role:"music",title:"music",settings:defaults,streamId:"one"});
 await ok("ADD",{tabId:2,role:"priority",title:"video",settings:defaults,streamId:"two"});
 assert.equal(nodes[0].connections[0],nodes[1]);assert.equal(nodes[1].connections[0],nodes[2]);assert.equal(nodes[2].connections[0].kind,"destination");
 await ok("TOGGLE");step(40);assert.ok(Math.abs(nodes[2].gain.value-.2)<.001);assert.equal(nodes[5].gain.value,1);
 nodes[4].signal=0;step(150);assert.equal(nodes[2].gain.value,1);
 nodes[4].signal=.1;step(40);assert.ok(nodes[2].gain.value<.201);
 await ok("SETTINGS",{settings:{...defaults,language:"en"}});assert.equal((await ok("STATE")).enabled,true);
 await ok("TOGGLE");assert.equal(nodes[2].gain.value,1);
 await ok("ADD",{tabId:3,role:"music",title:"replacement",settings:defaults,streamId:"three"});
 assert.equal(streams[0].getTracks()[0].stopped,true);
 assert.deepEqual(Array.from((await ok("STATE")).sources,s=>s.tabId),[2,3]);
 await ok("TOGGLE");step(40);streams[1].getTracks()[0].end();
 assert.equal((await ok("STATE")).enabled,false);assert.equal(nodes[8].gain.value,1);
 fail=true;assert.equal((await send("ADD",{tabId:4,role:"priority",settings:defaults})).ok,false);
 assert.equal((await ok("STATE")).sources.length,1);
 await ok("RELEASE");assert.equal((await ok("STATE")).sources.length,0);
 assert.ok(streams.every(s=>s.getTracks()[0].stopped));assert.ok(nodes.every(n=>n.disconnected));
});
