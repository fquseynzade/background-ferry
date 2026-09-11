// Isolated real Chromium test. Requires Playwright and its Chromium download.
// Usage: node tests/chrome/browser.cjs; optional PLAYWRIGHT_MODULE absolute package path.
const {chromium}=require(process.env.PLAYWRIGHT_MODULE||"playwright");
const {createServer}=require("node:http");
const {mkdtempSync}=require("node:fs");
const {tmpdir}=require("node:os");
const {resolve,join}=require("node:path");
const assert=require("node:assert/strict");
const delay=ms=>new Promise(r=>setTimeout(r,ms));
(async()=>{
 const server=createServer((req,res)=>{res.setHeader("Content-Type","text/html");res.end(`<!doctype html><title>${req.url.includes("music")?"Test music":"Test video"}</title><button id="play">Play quiet tone</button><script>
 let ctx,gain;document.querySelector('button').onclick=async()=>{ctx=new AudioContext();gain=ctx.createGain();gain.gain.value=.015;const o=ctx.createOscillator();o.frequency.value=220;o.connect(gain);gain.connect(ctx.destination);o.start();await ctx.resume();};window.silence=()=>gain.gain.value=0;window.sound=()=>gain.gain.value=.015;
 </script>`);});
 await new Promise(r=>server.listen(0,"127.0.0.1",r));
 const profile=mkdtempSync(join(tmpdir(),"ferry-chrome-test-"));
 let context;
 try{
  context=process.env.BROWSER_CDP
   ? (await chromium.connectOverCDP(process.env.BROWSER_CDP)).contexts()[0]
   : await chromium.launchPersistentContext(profile,{channel:"chromium",headless:true,ignoreDefaultArgs:["--mute-audio","--disable-extensions"],args:["--enable-unsafe-extension-debugging","--autoplay-policy=no-user-gesture-required"]});
  const cdp=await context.browser().newBrowserCDPSession();
  const {id}=await cdp.send("Extensions.loadUnpacked",{path:resolve(__dirname,"../../chrome-extension")});
  console.log("Loaded extension",id);
  const base="http://127.0.0.1:"+server.address().port;
  const music=await context.newPage();await music.goto(base+"/music");await music.locator("#play").click();
  const priority=await context.newPage();await priority.goto(base+"/video");await priority.locator("#play").click();
  const control=await context.newPage();await control.goto("chrome-extension://"+id+"/popup.html");
  const send=(type,data={})=>control.evaluate(async m=>{const r=await chrome.runtime.sendMessage({target:"worker",...m});if(!r?.ok)throw Error(r?.error);return r.state;},{type,...data});
  async function assign(page,role){
   await page.bringToFront();
   const pageCdp=await context.newCDPSession(page);
   const {targetInfo}=await pageCdp.send("Target.getTargetInfo");
   await cdp.send("Extensions.triggerAction",{id,targetId:targetInfo.targetId});
   const s=await send("ASSIGN",{role});assert.ok(s.sources.some(x=>x.role===role));console.log("PASS capture",role);return s;
  }
  await assign(music,"music");await assign(priority,"priority");
  await send("SETTINGS",{settings:{language:"en",mode:"steady",duck:20,threshold:-48,attack:100,hold:300,release:400}});
  await send("TOGGLE");
  let state;
  async function until(predicate,label){for(let i=0;i<80;i++){state=await send("STATE");if(predicate(state)){console.log("PASS",label);return;}await delay(100);}throw Error(label+" timed out: "+JSON.stringify(state));}
  await until(s=>s.enabled&&s.gain<.22&&s.sources.every(x=>x.peak>.001),"real captured audio ducks music to 20%");
  await priority.evaluate(()=>silence());
  await until(s=>s.gain>.99,"video silence restores music");
  await priority.evaluate(()=>sound());
  await until(s=>s.gain<.22,"resumed video ducks music again");
  await send("TOGGLE");state=await send("STATE");assert.equal(state.gain,1);assert.equal(state.enabled,false);console.log("PASS stop restores gain");
  await send("TOGGLE");await priority.close();
  await until(s=>!s.enabled&&s.gain===1&&s.sources.length===1,"closing priority restores music");
  state=await send("STOP");assert.equal(state.sources.length,0);assert.equal(state.settings.language,"en");
  console.log("PASS release removes captures and persists settings");
  await control.bringToFront();await control.reload();await control.waitForFunction(()=>document.documentElement.lang==="en");
  await control.locator("#language").selectOption("ru");await control.waitForFunction(()=>document.documentElement.lang==="ru");
  await control.locator(".info").first().hover();
  const help=await control.locator(".info").first().getAttribute("data-help");assert.ok(help.length>30);console.log("PASS popup RU/EN and help");
  await control.mouse.move(395,5);
  if(process.env.SCREENSHOT_PATH)await control.screenshot({path:process.env.SCREENSHOT_PATH,fullPage:true});
  console.log("All browser tests passed.");
 }finally{await context?.close();server.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
