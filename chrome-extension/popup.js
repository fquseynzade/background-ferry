import {t} from "./strings.js";
import {defaults} from "./mix.js";
let state={settings:{...defaults},sources:[],enabled:false,gain:1,phase:"ready"},pending=0,queue=Promise.resolve();
const $=id=>document.getElementById(id),keys=["duck","threshold","attack","hold","release"];
for(const [key,min,max,step] of [["attack",50,1000,50],["hold",200,5000,100],["release",200,5000,100]]){
 const row=document.createElement("div");row.className="setting";
 row.innerHTML=`<label for="${key}" data-t="${key}"></label><button class="info" data-tip="${key}Tip">i</button><output id="${key}Value"></output><input id="${key}" type="range" min="${min}" max="${max}" step="${step}">`;
 $("timing").append(row);
}
function render(){
 const lang=state.settings.language;document.documentElement.lang=lang;
 document.querySelectorAll("[data-t]").forEach(e=>e.textContent=t(e.dataset.t,lang));
 document.querySelectorAll("[data-tip]").forEach(e=>{e.dataset.help=t(e.dataset.tip,lang);e.setAttribute("aria-label",e.dataset.help);});
 $("language").value=lang;$("language").setAttribute("aria-label",t("language",lang));
 $("mode").value=state.settings.mode;$("mode").setAttribute("aria-label",t("mode",lang));
 for(const k of keys){if(document.activeElement!==$(k))$(k).value=state.settings[k];$(k+"Value").textContent=state.settings[k]+(k==="duck"?"%":k==="threshold"?" dB":" "+(lang==="ru"?"мс":"ms"));}
 $("sources").replaceChildren();
 for(const source of state.sources){
  const row=document.createElement("div");row.className="source";
  const name=document.createElement("div");name.className="name";name.textContent=source.title;name.title=source.title;
  const role=document.createElement("small");role.textContent=t(source.role,lang);name.append(role);
  const remove=document.createElement("button");remove.textContent="×";remove.setAttribute("aria-label",t("remove",lang));remove.onclick=()=>act("REMOVE",{tabId:source.tabId});
  row.append(name,remove);$("sources").append(row);
 }
 if(!state.sources.length){const p=document.createElement("span");p.className="empty";p.textContent=t("empty",lang);$("sources").append(p);}
 $("toggle").textContent=t(state.enabled?"stop":"start",lang);$("status").textContent=t(state.phase,lang);$("gain").textContent=Math.round(state.gain*100)+"%";
}
function act(type,data={}){
 pending++;
 queue=queue.catch(()=>{}).then(()=>perform(type,data)).finally(()=>pending--);
 return queue;
}
async function perform(type,data){
 if(type!=="STATE")$("error").textContent="";
 try{const response=await chrome.runtime.sendMessage({target:"worker",type,...data});if(!response?.ok)throw new Error(response?.error||"captureError");state=response.state;render();}
 catch(e){const key=["OPEN_WEB_TAB","SELECT_SOURCES"].includes(e.message)?e.message:"captureError";$("error").textContent=t(key,state.settings.language);}
}
$("music").onclick=()=>act("ASSIGN",{role:"music"});$("priority").onclick=()=>act("ASSIGN",{role:"priority"});
$("toggle").onclick=()=>act("TOGGLE");$("release").onclick=()=>act("STOP");
for(const key of [...keys,"mode","language"]){$(key).onchange=()=>act("SETTINGS",{changes:{[key]:keys.includes(key)?Number($(key).value):$(key).value}});}
render();await act("STATE");setInterval(()=>{if(!pending&&!keys.some(k=>document.activeElement===$(k)))act("STATE");},1000);
