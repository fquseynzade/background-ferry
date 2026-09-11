import {Envelope,defaults,normalizeSettings} from "./mix.js";
const sources=new Map(), envelope=new Envelope();
let context=null,settings={...defaults},enabled=false,last=performance.now();
function state(){return {settings,enabled,gain:enabled?envelope.gain:1,phase:enabled?envelope.phase:"ready",sources:[...sources.values()].map(s=>({tabId:s.tabId,title:s.title,pageUrl:s.pageUrl,role:s.role,peak:s.peak||0}))};}
function restore(){enabled=false;envelope.reset();for(const s of sources.values())s.gain.gain.setTargetAtTime(1,context.currentTime,.03);}
function remove(tabId){const s=sources.get(tabId);if(!s)return;sources.delete(tabId);s.stream.getTracks().forEach(t=>t.stop());s.input.disconnect();s.analyser.disconnect();s.gain.disconnect();if(![...sources.values()].some(s=>s.role==="music")||![...sources.values()].some(s=>s.role==="priority"))restore();}
function role(tabId,value){
  if(!["music","priority"].includes(value))throw new Error("Invalid role");
  restore();
  if(value==="music")for(const s of [...sources.values()])if(s.role==="music"&&s.tabId!==tabId)remove(s.tabId);
  const s=sources.get(tabId);if(s)s.role=value;
}
async function handle(m){
  switch(m.type){
    case "STATE":break;
    case "ADD": {
      context??=new AudioContext(); await context.resume();
      let stream;
      try {
        stream=await navigator.mediaDevices.getUserMedia({audio:{mandatory:{chromeMediaSource:"tab",chromeMediaSourceId:m.streamId}},video:false});
        const input=context.createMediaStreamSource(stream),analyser=context.createAnalyser(),gain=context.createGain();
        analyser.fftSize=2048;input.connect(analyser);analyser.connect(gain);gain.connect(context.destination);
        // Chrome mutes its original route during capture; this route preserves playback.
        sources.set(m.tabId,{tabId:m.tabId,title:m.title,pageUrl:m.pageUrl,role:m.role,stream,input,analyser,gain,samples:new Float32Array(analyser.fftSize)});
        stream.getAudioTracks()[0].addEventListener("ended",()=>remove(m.tabId),{once:true});
        settings=normalizeSettings(m.settings);role(m.tabId,m.role);
      } catch(error){if(sources.has(m.tabId))remove(m.tabId);else stream?.getTracks().forEach(t=>t.stop());throw error;}
      break;
    }
    case "ROLE":role(m.tabId,m.role);break;
    case "REMOVE":remove(m.tabId);break;
    case "SETTINGS":settings=normalizeSettings(m.settings);envelope.reset();break;
    case "TOGGLE":
      if(enabled)restore();else {
        if(![...sources.values()].some(s=>s.role==="music")||![...sources.values()].some(s=>s.role==="priority"))throw new Error("SELECT_SOURCES");
        await context.resume();envelope.reset();enabled=true;
      }break;
    case "RELEASE":restore();for(const id of [...sources.keys()])remove(id);if(context){await context.close();context=null;}break;
    default:throw new Error("Unknown command");
  }
  return state();
}
let queue=Promise.resolve();
chrome.runtime.onMessage.addListener((m,sender,respond)=>{
  if(m.target!=="offscreen"||sender.id!==chrome.runtime.id)return false;
  queue=queue.catch(()=>{}).then(()=>handle(m));queue.then(state=>respond({ok:true,state}),error=>respond({ok:false,error:error.message}));return true;
});
setInterval(()=>{
  const now=performance.now(),ms=now-last;last=now;
  if(!context)return;
  let peak=0;
  for(const s of sources.values()){s.analyser.getFloatTimeDomainData(s.samples);s.peak=0;for(const x of s.samples)s.peak=Math.max(s.peak,Math.abs(x));if(s.role==="priority")peak=Math.max(peak,s.peak);}
  const gain=enabled?envelope.step(peak,ms,settings):1;
  for(const s of sources.values())s.gain.gain.setTargetAtTime(s.role==="music"?gain:1,context.currentTime,.015);
},50);
