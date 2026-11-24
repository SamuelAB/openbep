// One-off: node compliance/model/tools/build_residence.js   (needs Playwright and a static server on the repo root)
// Opens build_residence.html in a headless browser, which reads VIM's public sample model, and writes ../example-residence.json.
const {chromium}=require('playwright'),fs=require('fs'),path=require('path'),http=require('http');
const root=path.resolve(__dirname,'../../..');
const srv=http.createServer((q,r)=>{const f=path.join(root,decodeURIComponent(q.url.split('?')[0]));fs.readFile(f,(e,d)=>{if(e){r.writeHead(404);r.end()}else{r.writeHead(200,{'content-type':f.endsWith('.html')?'text/html':'application/octet-stream'});r.end(d)}})}).listen(0,async()=>{
  const b=await chromium.launch({args:['--use-gl=swiftshader','--enable-unsafe-swiftshader','--ignore-gpu-blocklist']}),p=await b.newPage();
  // Behind a proxy the headless browser may not reach the CDNs itself; fetch those files with curl instead.
  const got={},get=u=>got[u]||(got[u]=new Promise((ok,no)=>require('child_process').execFile('curl',['-sL',u],{encoding:'buffer',maxBuffer:1<<28},(e,d)=>e?no(e):ok(d))));
  if(process.env.HTTPS_PROXY) await p.route(/^https:/,r=>get(r.request().url()).then(d=>r.fulfill({body:d,headers:{'access-control-allow-origin':'*'},contentType:/\.js$/.test(r.request().url())?'text/javascript':'application/octet-stream'}),()=>r.abort()));
  p.on('pageerror',e=>console.error('page error:',e.message));
  await p.goto(`http://localhost:${srv.address().port}/compliance/model/tools/build_residence.html${process.argv[2]?'?vim='+encodeURIComponent(process.argv[2]):''}`);
  await p.waitForFunction('window.done',null,{timeout:300000});
  const {err,out,stats}=await p.evaluate(()=>({err:window.err,out:window.out,stats:window.stats}));
  await b.close();srv.close();if(err){console.error(err);process.exit(1)}
  const lines=['{'];for(const [k,v] of Object.entries(out)){lines.push(k==='elements'?`  "elements": [\n${v.map(r=>'    '+JSON.stringify(r)).join(',\n')}\n  ]`:`  ${JSON.stringify(k)}: ${JSON.stringify(v)},`)}
  fs.writeFileSync(path.join(__dirname,'../example-residence.json'),lines.join('\n')+'\n}\n');console.log(stats)});
