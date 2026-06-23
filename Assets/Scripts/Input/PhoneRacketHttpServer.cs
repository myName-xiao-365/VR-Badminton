using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace VRBadminton.Input
{
    public sealed class PhoneRacketHttpServer : IDisposable
    {
        private readonly object sync = new object();
        private TcpListener listener;
        private Thread thread;
        private volatile bool running;
        private string latestPayload;
        private string parsedPayload;
        private BadmintonRacketFrame parsedFrame = BadmintonRacketFrame.Default("phone");
        private long latestReceivedMs;
        private long sequence;
        private long readySequence;
        private bool hasStableOrientation;
        private Quaternion stableOrientation = Quaternion.identity;

        public int Port { get; private set; }
        public string Url { get; private set; } = string.Empty;
        public string Status { get; private set; } = "Phone server idle";
        public long Sequence => sequence;
        public long ReadySequence => readySequence;
        public long LatestReceivedMs => latestReceivedMs;

        public void Start(int preferredPort)
        {
            Stop();
            for (int port = preferredPort; port < preferredPort + 20; port++)
            {
                try
                {
                    listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    Port = port;
                    running = true;
                    Url = $"http://{LocalIpv4Address()}:{Port}/phone.html";
                    Status = $"Phone server listening at {Url}";
                    thread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = "VRBadmintonPhoneHttp"
                    };
                    thread.Start();
                    return;
                }
                catch (SocketException)
                {
                    listener = null;
                }
            }

            Status = $"Could not bind phone server from port {preferredPort}";
        }

        public void Stop()
        {
            running = false;
            try
            {
                listener?.Stop();
            }
            catch (SocketException)
            {
            }

            listener = null;
            thread = null;
        }

        public void Dispose()
        {
            Stop();
        }

        public bool TryGetLatestFrame(out BadmintonRacketFrame frame)
        {
            string payload;
            long receivedAt;
            lock (sync)
            {
                payload = latestPayload;
                receivedAt = latestReceivedMs;
            }

            if (string.IsNullOrEmpty(payload))
            {
                frame = parsedFrame;
                return false;
            }

            if (!string.Equals(parsedPayload, payload, StringComparison.Ordinal))
            {
                if (TryParseRacketFrameJson(payload, receivedAt, out BadmintonRacketFrame parsed))
                {
                    parsed.Orientation = StabilizeOrientation(parsed.Orientation);
                    parsedFrame = parsed;
                    parsedPayload = payload;
                    Status = $"Phone connected {parsedFrame.ClientId}";
                }
                else
                {
                    Status = "Bad phone frame";
                    frame = parsedFrame;
                    return false;
                }
            }

            frame = parsedFrame;
            return true;
        }

        private Quaternion StabilizeOrientation(Quaternion orientation)
        {
            orientation = BadmintonInputMath.Normalize(orientation);
            if (hasStableOrientation && Quaternion.Dot(stableOrientation, orientation) < 0f)
            {
                orientation = new Quaternion(
                    -orientation.x,
                    -orientation.y,
                    -orientation.z,
                    -orientation.w);
            }

            stableOrientation = orientation;
            hasStableOrientation = true;
            return orientation;
        }

        public static bool TryParseRacketFrameJson(
            string json,
            long receivedAtMs,
            out BadmintonRacketFrame frame)
        {
            try
            {
                PhoneRacketFrameDto dto = JsonUtility.FromJson<PhoneRacketFrameDto>(json);
                frame = dto.ToFrame(receivedAtMs);
                return true;
            }
            catch (Exception)
            {
                frame = BadmintonRacketFrame.Default("phone");
                return false;
            }
        }

        private void ListenLoop()
        {
            while (running)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch (SocketException)
                {
                    if (running)
                    {
                        Status = "Phone server socket error";
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            {
                client.ReceiveTimeout = 2500;
                client.SendTimeout = 2500;
                NetworkStream stream = client.GetStream();
                using StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                string requestLine = reader.ReadLine();
                if (string.IsNullOrEmpty(requestLine))
                {
                    return;
                }

                string[] parts = requestLine.Split(' ');
                string method = parts.Length > 0 ? parts[0] : "GET";
                string path = parts.Length > 1 ? parts[1] : "/";
                int contentLength = 0;
                string line;
                while (!string.IsNullOrEmpty(line = reader.ReadLine()))
                {
                    int separator = line.IndexOf(':');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    string name = line.Substring(0, separator).Trim();
                    string value = line.Substring(separator + 1).Trim();
                    if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(value, out contentLength);
                    }
                }

                if (method == "OPTIONS")
                {
                    WriteResponse(stream, "application/json", "{\"ok\":true}");
                    return;
                }

                if (method == "POST" && path.StartsWith("/racket-frame", StringComparison.OrdinalIgnoreCase))
                {
                    char[] buffer = new char[Mathf.Max(0, contentLength)];
                    int read = 0;
                    while (read < buffer.Length)
                    {
                        int count = reader.Read(buffer, read, buffer.Length - read);
                        if (count <= 0)
                        {
                            break;
                        }

                        read += count;
                    }

                    lock (sync)
                    {
                        latestPayload = new string(buffer, 0, read);
                        latestReceivedMs = BadmintonInputClock.NowMs();
                        sequence++;
                    }

                    WriteResponse(stream, "application/json", "{\"ok\":true}");
                    return;
                }

                if (method == "POST" && path.StartsWith("/racket-ready", StringComparison.OrdinalIgnoreCase))
                {
                    lock (sync)
                    {
                        readySequence++;
                    }

                    Status = "Phone ready";
                    WriteResponse(stream, "application/json", "{\"ok\":true}");
                    return;
                }

                if (method == "GET" && (path == "/" || path.StartsWith("/phone.html", StringComparison.OrdinalIgnoreCase)))
                {
                    WriteResponse(stream, "text/html; charset=utf-8", PhoneHtml());
                    return;
                }

                WriteResponse(stream, "application/json", "{\"ok\":false,\"message\":\"not found\"}", "404 Not Found");
            }
        }

        private static void WriteResponse(
            Stream stream,
            string contentType,
            string body,
            string status = "200 OK")
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header =
                $"HTTP/1.1 {status}\r\n" +
                $"Content-Type: {contentType}\r\n" +
                "Cache-Control: no-store\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Headers: content-type\r\n" +
                "Access-Control-Allow-Methods: GET,POST,OPTIONS\r\n" +
                $"Content-Length: {bodyBytes.Length}\r\n" +
                "Connection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
        }

        private static string LocalIpv4Address()
        {
            try
            {
                IPAddress[] addresses = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
                for (int i = 0; i < addresses.Length; i++)
                {
                    IPAddress address = addresses[i];
                    if (address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch (SocketException)
            {
            }

            return "127.0.0.1";
        }

        private static string PhoneHtml()
        {
            return @"<!doctype html>
<html lang=""zh-CN"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>方块羽球 手机球拍</title>
<style>
*{box-sizing:border-box}
body{margin:0;min-height:100vh;background:#5b8f3a;color:#fff7d6;font-family:system-ui,-apple-system,Segoe UI,sans-serif;image-rendering:pixelated}
body:before{content:"""";position:fixed;inset:0;background:
linear-gradient(90deg,rgba(255,255,255,.06) 1px,transparent 1px),
linear-gradient(rgba(255,255,255,.06) 1px,transparent 1px);
background-size:24px 24px;pointer-events:none}
main{max-width:520px;margin:0 auto;padding:18px 18px 150px}
.panel{background:#3f2b18;border:4px solid #1e160d;box-shadow:0 6px 0 #1e160d,0 0 0 4px #7a5630 inset;padding:16px}
h1{margin:0 0 8px;font-size:28px;line-height:1.1;text-shadow:3px 3px 0 #1e160d}
.hint{margin:10px 0 14px;color:#f7e7a7;font-size:15px;line-height:1.45}
.bar{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin:12px 0}
.tile{background:#243516;border:3px solid #111b0b;box-shadow:0 3px 0 #111b0b;padding:10px;min-height:62px}
.tile span{display:block;color:#b7e08d;font-size:12px}.tile strong{display:block;margin-top:4px;font-size:20px}
.steps{display:grid;grid-template-columns:1fr 1fr 1fr;gap:6px;margin:8px 0}
.step{background:#243516;border:3px solid #111b0b;color:#b7e08d;text-align:center;padding:8px 2px;font-weight:900}
.step.done{background:#78a83a;color:#111b0b}
button{width:100%;border:4px solid #111b0b;box-shadow:0 5px 0 #111b0b;background:#4f8f2e;color:#fff7d6;font-weight:900;font-size:18px;padding:13px 10px;margin:8px 0;text-shadow:2px 2px 0 #1e160d}
button:active{transform:translateY(4px);box-shadow:0 1px 0 #111b0b}
.readyDock{position:fixed;left:50%;bottom:24px;transform:translateX(-50%);width:min(480px,calc(100vw - 36px));z-index:2}
#ready{background:#d69b2d;color:#211608;font-size:24px;text-shadow:none;margin:0}
.ok{color:#a7f27a}.bad{color:#ffb0a8}
</style>
</head>
<body>
<main>
<section class=""panel"">
<h1>方块球拍</h1>
<p class=""hint"" id=""hint"">先完成三步位姿初始化。回合准备时摆准备式，再按屏幕下方的准备开球。</p>
<button id=""start"">启动传感器</button>
<button id=""record"">记录位姿</button>
<div class=""steps"">
<div class=""step"" id=""step0"">1 竖直</div>
<div class=""step"" id=""step1"">2 平放</div>
<div class=""step"" id=""step2"">3 侧向</div>
</div>
<div class=""bar"">
<div class=""tile""><span>状态</span><strong id=""sensor"">未启动</strong></div>
<div class=""tile""><span>初始化</span><strong id=""calib"">0/3</strong></div>
</div>
</section>
<div class=""readyDock""><button id=""ready"">准备开球</button></div>
</main>
<script>
const clientId='phone-'+Math.random().toString(36).slice(2,8);
const poses=[
  '位姿 1：竖直拿拍，手机屏幕朝左，充电口朝下。',
  '位姿 2：手机平放，屏幕朝上，充电口朝向身体。',
  '位姿 3：侧向拿拍，充电口朝左，屏幕朝向身体。'
];
let enabled=false, aligned=false, recording=false, step=0, sending=false, hasOrientation=false, sendTimer=null;
let raw={alpha:0,beta:0,gamma:0};
let zero={alpha:0,beta:0,gamma:0};
let base=null, relative=identity();
let angularVelocity=[0,0,0], acceleration=[0,0,0];
let samples=[];
const $=id=>document.getElementById(id);
function identity(){return [1,0,0,0,1,0,0,0,1];}
function deg(v){return (Number(v)||0)*Math.PI/180;}
function rel(a,b){let x=(Number(a)||0)-(Number(b)||0);while(x>180)x-=360;while(x<-180)x+=360;return x;}
function orientationMatrix(o){
  const A=deg(o.alpha),B=deg(o.beta),G=deg(o.gamma);
  const cA=Math.cos(A),sA=Math.sin(A),cB=Math.cos(B),sB=Math.sin(B),cG=Math.cos(G),sG=Math.sin(G);
  return [cA*cG-sA*sB*sG,-cB*sA,cA*sG+cG*sA*sB,cG*sA+cA*sB*sG,cA*cB,sA*sG-cA*cG*sB,-cB*sG,sB,cB*cG];
}
function transpose(m){return [m[0],m[3],m[6],m[1],m[4],m[7],m[2],m[5],m[8]];}
function multiply(a,b){return [
 a[0]*b[0]+a[1]*b[3]+a[2]*b[6],a[0]*b[1]+a[1]*b[4]+a[2]*b[7],a[0]*b[2]+a[1]*b[5]+a[2]*b[8],
 a[3]*b[0]+a[4]*b[3]+a[5]*b[6],a[3]*b[1]+a[4]*b[4]+a[5]*b[7],a[3]*b[2]+a[4]*b[5]+a[5]*b[8],
 a[6]*b[0]+a[7]*b[3]+a[8]*b[6],a[6]*b[1]+a[7]*b[4]+a[8]*b[7],a[6]*b[2]+a[7]*b[5]+a[8]*b[8]
];}
function quat(m){
  const tr=m[0]+m[4]+m[8];let x=0,y=0,z=0,w=1;
  if(tr>0){const s=Math.sqrt(tr+1)*2;w=.25*s;x=(m[7]-m[5])/s;y=(m[2]-m[6])/s;z=(m[3]-m[1])/s;}
  else if(m[0]>m[4]&&m[0]>m[8]){const s=Math.sqrt(1+m[0]-m[4]-m[8])*2;w=(m[7]-m[5])/s;x=.25*s;y=(m[1]+m[3])/s;z=(m[2]+m[6])/s;}
  else if(m[4]>m[8]){const s=Math.sqrt(1+m[4]-m[0]-m[8])*2;w=(m[2]-m[6])/s;x=(m[1]+m[3])/s;y=.25*s;z=(m[5]+m[7])/s;}
  else{const s=Math.sqrt(1+m[8]-m[0]-m[4])*2;w=(m[3]-m[1])/s;x=(m[2]+m[6])/s;y=(m[5]+m[7])/s;z=.25*s;}
  const l=Math.hypot(x,y,z,w)||1;return [x/l,y/l,z/l,w/l];
}
function setStatus(text,ok){
  $('sensor').textContent=text;
  $('sensor').className=ok?'ok':'bad';
}
function sleep(ms){return new Promise(resolve=>setTimeout(resolve,ms));}
function avg(items,key){return items.reduce((s,x)=>s+Number(x[key]||0),0)/Math.max(1,items.length);}
function circularAvg(items,key){
  if(!items.length)return 0;
  let sx=0,sy=0;
  for(const item of items){const r=deg(item[key]);sx+=Math.cos(r);sy+=Math.sin(r);}
  return Math.atan2(sy/items.length,sx/items.length)*180/Math.PI;
}
function refreshSteps(){
  $('calib').textContent=step+'/3';
  for(let i=0;i<3;i++)$('step'+i).className=i<step?'step done':'step';
}
async function start(){
  try{
    const api=window.DeviceOrientationEvent;
    if(api&&typeof api.requestPermission==='function'){
      const r=await api.requestPermission();
      if(r!=='granted')throw new Error('permission denied');
    }
    if(!enabled){
      addEventListener('deviceorientation',onOrientation);
      addEventListener('devicemotion',onMotion);
      sendTimer=setInterval(send,16);
    }
    enabled=true;
    setStatus('运行中',true);
    $('hint').textContent=aligned?'保持准备式，等回合准备时按「准备开球」。':poses[step];
  }catch(e){setStatus(e.message||'启动失败',false);}
}
function onOrientation(e){
  raw={alpha:Number(e.alpha||0),beta:Number(e.beta||0),gamma:Number(e.gamma||0)};
  hasOrientation=true;
  if(recording)samples.push(raw);
  const current=orientationMatrix(raw);
  if(!base)base=current;
  relative=multiply(transpose(base),current);
}
function onMotion(e){
  const r=e.rotationRate||{};
  angularVelocity=[Number(r.beta||0),Number(r.alpha||0),Number(r.gamma||0)];
  const a=e.acceleration||e.accelerationIncludingGravity||{};
  acceleration=[Number(a.x||0),Number(a.y||0),Number(a.z||0)];
}
function recenter(){
  zero={alpha:raw.alpha,beta:raw.beta,gamma:raw.gamma};
  base=orientationMatrix(raw);
  relative=identity();
  aligned=true;
}
async function recordPose(){
  if(!enabled)await start();
  if(!hasOrientation)await sleep(450);
  if(!hasOrientation){
    setStatus('等待姿态',false);
    $('hint').textContent='手机还没给出姿态数据，稍等半秒再记录。';
    return;
  }
  samples=[];
  recording=true;
  $('hint').textContent='保持不动...';
  setTimeout(()=>{
    recording=false;
    if(samples.length>2){
      const sample={alpha:circularAvg(samples,'alpha'),beta:avg(samples,'beta'),gamma:avg(samples,'gamma')};
      if(step===0){zero=sample;base=orientationMatrix(sample);relative=identity();}
      step=Math.min(3,step+1);
      aligned=step>=3;
      refreshSteps();
      setStatus(aligned?'已初始化':'记录中',true);
      $('hint').textContent=aligned?'初始化完成。准备式：竖直拿拍，屏幕朝左，充电口朝下。':poses[step];
    }else{
      $('hint').textContent='采样太少，请保持不动再记录一次。';
    }
  },1000);
}
function frameBody(){
  return JSON.stringify({
    type:'racket_frame',timestamp:Date.now(),clientId,aligned,sessionId:clientId,
    orientation:quat(relative),rotationMatrix:relative,angularVelocity,acceleration,
    angularSpeed:Math.hypot(...angularVelocity),
    raw:{alpha:rel(raw.alpha,zero.alpha),beta:rel(raw.beta,zero.beta),gamma:rel(raw.gamma,zero.gamma)}
  });
}
async function sendFrame(){
  await fetch('/racket-frame',{method:'POST',headers:{'content-type':'application/json'},body:frameBody()});
}
async function ready(){
  try{
    if(!enabled)await start();
    if(!hasOrientation)await sleep(450);
    if(!hasOrientation){
      setStatus('等待姿态',false);
      $('hint').textContent='手机还没给出姿态数据，稍等半秒再按。';
      return;
    }
    if(!aligned){
      setStatus('未初始化',false);
      $('hint').textContent='请先完成三步位姿初始化。';
      return;
    }
    recenter();
    await sendFrame();
    await fetch('/racket-ready',{method:'POST'});
    setStatus('已准备',true);
    $('hint').textContent='校正完成。保持准备式，等待对方发球。';
  }catch(e){
    setStatus('发送失败',false);
    $('hint').textContent='请确认手机和电脑在同一网络，再重新按准备。';
  }
}
async function send(){
  if(!enabled||sending)return;
  sending=true;
  try{
    await sendFrame();
  }catch(e){setStatus('连接中断',false);}
  sending=false;
}
$('start').onclick=start;$('record').onclick=recordPose;$('ready').onclick=ready;refreshSteps();$('hint').textContent=poses[0];
</script>
</body>
</html>";
        }

#pragma warning disable 0649
        [Serializable]
        private sealed class PhoneRacketFrameDto
        {
            public string type;
            public long timestamp;
            public string clientId;
            public bool aligned;
            public string sessionId;
            public float[] orientation;
            public float[] rotationMatrix;
            public float[] angularVelocity;
            public float[] acceleration;
            public float angularSpeed;
            public PhoneRacketRawDto raw;

            public BadmintonRacketFrame ToFrame(long receivedAtMs)
            {
                bool hasRotationMatrix = rotationMatrix != null && rotationMatrix.Length >= 9;
                Matrix4x4 matrix = hasRotationMatrix
                    ? BadmintonInputMath.MirrorForwardBack(BadmintonInputMath.MatrixFromRowMajor(rotationMatrix))
                    : Matrix4x4.identity;
                return new BadmintonRacketFrame
                {
                    Timestamp = receivedAtMs > 0 ? receivedAtMs : BadmintonInputClock.NowMs(),
                    ClientId = string.IsNullOrEmpty(clientId) ? "phone" : clientId,
                    Aligned = aligned,
                    SessionId = sessionId ?? string.Empty,
                    Orientation = hasRotationMatrix
                        ? BadmintonInputMath.QuaternionFromMatrix(matrix)
                        : BadmintonInputMath.MirrorForwardBack(BadmintonInputMath.QuaternionFromArray(orientation)),
                    RotationMatrix = matrix,
                    AngularVelocity = BadmintonInputMath.Vector3FromArray(angularVelocity),
                    Acceleration = BadmintonInputMath.Vector3FromArray(acceleration),
                    AngularSpeed = angularSpeed,
                    RawEuler = raw == null ? Vector3.zero : new Vector3(raw.beta, raw.alpha, raw.gamma)
                };
            }
        }

        [Serializable]
        private sealed class PhoneRacketRawDto
        {
            public float alpha;
            public float beta;
            public float gamma;
        }
#pragma warning restore 0649
    }
}
