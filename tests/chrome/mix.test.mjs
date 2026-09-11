import test from "node:test";
import assert from "node:assert/strict";
import {Envelope, defaults, normalizeSettings} from "../../chrome-extension/mix.js";
test("steady ducking reaches 20%, pauses do not pump, silence restores",()=>{
 const e=new Envelope();
 for(let i=0;i<40;i++)e.step(.1,50,defaults);
 assert.ok(Math.abs(e.gain-.2)<.001);
 for(let i=0;i<10;i++)e.step(0,50,defaults);
 assert.equal(e.phase,"holding");assert.ok(e.gain<.201);
 for(let i=0;i<120;i++)e.step(0,50,defaults);
 assert.equal(e.gain,1);assert.equal(e.phase,"ready");
});
test("adaptive gives quieter content a higher background, bounded by its band",()=>{
 const s={...defaults,mode:"adaptive"},quiet=new Envelope(),loud=new Envelope();
 for(let i=0;i<100;i++){quiet.step(.01,50,s);loud.step(.5,50,s);}
 assert.ok(loud.gain<quiet.gain);assert.ok(loud.gain>=.2&&quiet.gain<=.33);
});
test("threshold hysteresis and another signal interrupt a silence hold",()=>{
 const e=new Envelope();
 for(let i=0;i<30;i++)e.step(.1,50,defaults);
 e.step(10**(-44/20),50,defaults);assert.equal(e.phase,"ducking");
 e.step(0,50,defaults);assert.equal(e.phase,"holding");
 e.step(.1,50,defaults);assert.equal(e.phase,"ducking");
});
test("settings reject nonfinite input and constrain saved values",()=>{
 const s=normalizeSettings({duck:Infinity,attack:-5,release:99999,language:"xx",mode:"??"});
 assert.equal(s.duck,20);assert.equal(s.attack,50);assert.equal(s.release,5000);
 assert.equal(s.language,"ru");assert.equal(s.mode,"steady");
 const e=new Envelope();e.step(NaN,NaN,s);assert.equal(e.gain,1);
});
