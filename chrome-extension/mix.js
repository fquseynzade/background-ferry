export const defaults = Object.freeze({language:"ru", mode:"steady", duck:20, threshold:-42, attack:180, hold:900, release:1400});
export function normalizeSettings(input={}) {
  const number=(key,min,max)=>Number.isFinite(input[key])?Math.max(min,Math.min(max,input[key])):defaults[key];
  return {language:input.language==="en"?"en":"ru",mode:input.mode==="adaptive"?"adaptive":"steady",
    duck:number("duck",1,60),threshold:number("threshold",-70,-10),attack:number("attack",50,1000),hold:number("hold",200,5000),release:number("release",200,5000)};
}
export class Envelope {
  constructor(){this.reset();}
  reset(){this.gain=1;this.quiet=0;this.held=1;this.active=false;this.phase="ready";}
  step(peak,ms,s){
    const db=20*Math.log10(Math.max(Number.isFinite(peak)?peak:0,0.000001));
    ms=Number.isFinite(ms)?Math.max(0,Math.min(ms,250)):0;
    let target;
    if(db>=s.threshold-(this.active?4:0)){
      this.active=true;this.quiet=0;
      const floor=s.duck/100, ceiling=Math.min(.65,floor*1.65);
      target=s.mode==="steady"?floor:ceiling+(floor-ceiling)*Math.max(0,Math.min(1,(db-s.threshold)/30));
      this.held=target;this.phase="ducking";
    } else {this.active=false;this.quiet+=ms;target=this.quiet<s.hold?this.held:1;this.phase=target<1?"holding":"returning";}
    this.gain+=(target-this.gain)*(1-Math.exp(-3*ms/(target<this.gain?s.attack:s.release)));
    if(Math.abs(this.gain-target)<.0005)this.gain=target;
    if(this.gain===1)this.phase="ready";
    return this.gain;
  }
}
