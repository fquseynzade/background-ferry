import {defaults,normalizeSettings} from "./mix.js";
let queue=Promise.resolve();
async function exists(){return (await chrome.runtime.getContexts({contextTypes:["OFFSCREEN_DOCUMENT"],documentUrls:[chrome.runtime.getURL("offscreen.html")]})).length>0;}
async function ensure(){
  if(!await exists())await chrome.offscreen.createDocument({url:"offscreen.html",reasons:["USER_MEDIA"],justification:"Process explicitly selected tab audio locally for background music ducking."});
}
async function call(type,data={}){
  const result=await chrome.runtime.sendMessage({target:"offscreen",type,...data});
  if(!result?.ok)throw new Error(result?.error||"Audio processor did not respond.");
  return result.state;
}
async function handle(message){
  const saved=normalizeSettings((await chrome.storage.local.get("settings")).settings||defaults);
  switch(message.type){
    case "STATE": return await exists()?call("STATE"):{settings:saved,sources:[],enabled:false,gain:1,phase:"ready"};
    case "ASSIGN": {
      if(!["music","priority"].includes(message.role))throw new Error("Invalid role");
      // Always capture the active tab authorized by opening the action popup.
      const [tab]=await chrome.tabs.query({active:true,currentWindow:true});
      if(!tab?.id || !/^https?:/.test(tab.url||""))throw new Error("OPEN_WEB_TAB");
      await ensure();
      const state=await call("STATE");
      if(state.sources.some(s=>s.tabId===tab.id))return call("ROLE",{tabId:tab.id,role:message.role});
      const streamId=await chrome.tabCapture.getMediaStreamId({targetTabId:tab.id});
      return call("ADD",{streamId,tabId:tab.id,title:tab.title||"Tab",role:message.role,settings:saved});
    }
    case "SETTINGS": {
      const settings=normalizeSettings(message.changes?{...saved,...message.changes}:message.settings);await chrome.storage.local.set({settings});
      return await exists()?call("SETTINGS",{settings}):{settings,sources:[],enabled:false,gain:1,phase:"ready"};
    }
    case "TOGGLE": if(!await exists())throw new Error("SELECT_SOURCES");return call("TOGGLE");
    case "REMOVE": return await exists()?call("REMOVE",{tabId:message.tabId}):{settings:saved,sources:[],enabled:false,gain:1,phase:"ready"};
    case "STOP": {
      if(await exists()){await call("RELEASE");await chrome.offscreen.closeDocument();}
      await chrome.action.setBadgeText({text:""});
      return {settings:saved,sources:[],enabled:false,gain:1,phase:"ready"};
    }
    default: throw new Error("Unknown command");
  }
}
chrome.runtime.onMessage.addListener((message,sender,respond)=>{
  if(message.target!=="worker"||sender.id!==chrome.runtime.id)return false;
  queue=queue.catch(()=>{}).then(()=>handle(message));
  queue.then(async state=>{await chrome.action.setBadgeText({text:state.enabled?"ON":""});await chrome.action.setBadgeBackgroundColor({color:"#356c57"});respond({ok:true,state});},error=>respond({ok:false,error:error.message}));
  return true;
});
chrome.tabs.onRemoved.addListener(tabId=>{queue=queue.catch(()=>{}).then(async()=>{if(await exists())await call("REMOVE",{tabId});}).catch(()=>{});});
