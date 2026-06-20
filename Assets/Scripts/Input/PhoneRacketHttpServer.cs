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
        private bool hasStableOrientation;
        private Quaternion stableOrientation = Quaternion.identity;

        public int Port { get; private set; }
        public string Url { get; private set; } = string.Empty;
        public string Status { get; private set; } = "Phone server idle";
        public long Sequence => sequence;
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
<html lang=""en"">
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>VR Badminton Phone Racket</title>
<style>
body{margin:0;background:#0b1020;color:#eef2ff;font-family:system-ui,-apple-system,Segoe UI,sans-serif}
main{max-width:720px;margin:0 auto;padding:22px}
.card{background:#151b2f;border:1px solid #283149;border-radius:8px;padding:16px;margin:12px 0}
button{border:0;border-radius:8px;padding:12px 14px;margin:4px;background:#4f8cff;color:white;font-weight:700}
button.secondary{background:#334155}.ok{color:#86efac}.bad{color:#fca5a5}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:10px}.metric{background:#0f172a;border-radius:6px;padding:10px}
.metric span{display:block;color:#94a3b8;font-size:12px}.metric strong{font-size:18px}
code{word-break:break-all;color:#fde68a}
</style>
</head>
<body>
<main>
<h1>Phone Racket</h1>
<p>Hold the phone firmly as the racket handle. Start sensors, record the three static poses, then swing.</p>
<section class=""card"">
<button id=""start"">Start sensors</button>
<button id=""record"" class=""secondary"">Record pose</button>
<div class=""grid"">
<div class=""metric""><span>Sensor</span><strong id=""sensor"">idle</strong></div>
<div class=""metric""><span>Calibration</span><strong id=""calib"">0 / 3</strong></div>
<div class=""metric""><span>Angular speed</span><strong id=""speed"">0 deg/s</strong></div>
<div class=""metric""><span>Sent</span><strong id=""sent"">0</strong></div>
</div>
<p id=""hint"">Pose 1: upright, screen toward chest, charging port down.</p>
<p>Endpoint: <code>/racket-frame</code></p>
</section>
</main>
<script>
const clientId='phone-'+Math.random().toString(36).slice(2,8);
const poses=[
  'Pose 1: upright, screen toward chest, charging port down.',
  'Pose 2: flat, screen upward, charging port toward chest.',
  'Pose 3: side, charging port left, screen toward chest.'
];
let enabled=false, aligned=false, recording=false, step=0, sent=0, sending=false;
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
function avg(items,key){return items.reduce((s,x)=>s+Number(x[key]||0),0)/Math.max(1,items.length);}
function circularAvg(items,key){
  if(!items.length)return 0;
  let sx=0,sy=0;
  for(const item of items){const r=deg(item[key]);sx+=Math.cos(r);sy+=Math.sin(r);}
  return Math.atan2(sy/items.length,sx/items.length)*180/Math.PI;
}
async function start(){
  try{
    const api=DeviceOrientationEvent;
    if(api&&typeof api.requestPermission==='function'){
      const r=await api.requestPermission();
      if(r!=='granted')throw new Error('permission denied');
    }
    if(!enabled){
      addEventListener('deviceorientation',onOrientation);
      addEventListener('devicemotion',onMotion);
      setInterval(send,33);
    }
    enabled=true;$('sensor').textContent='running';$('sensor').className='ok';
  }catch(e){$('sensor').textContent=e.message||'failed';$('sensor').className='bad';}
}
function onOrientation(e){
  raw={alpha:Number(e.alpha||0),beta:Number(e.beta||0),gamma:Number(e.gamma||0)};
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
  $('speed').textContent=Math.hypot(...angularVelocity).toFixed(0)+' deg/s';
}
function record(){
  samples=[];
  recording=true;
  $('hint').textContent='Hold still...';
  setTimeout(()=>{
    recording=false;
    if(samples.length>2){
      const sample={alpha:circularAvg(samples,'alpha'),beta:avg(samples,'beta'),gamma:avg(samples,'gamma')};
      if(step===0){zero=sample;base=orientationMatrix(sample);}
      step=Math.min(3,step+1);
      aligned=step>=3;
      $('calib').textContent=step+' / 3';
      $('hint').textContent=aligned?'Calibration complete. Swing when ready.':poses[step];
    }else{
      $('hint').textContent='Not enough samples. Hold still and record again.';
    }
  },1000);
}
async function send(){
  if(!enabled||sending)return;
  sending=true;
  const body=JSON.stringify({
    type:'racket_frame',timestamp:Date.now(),clientId,aligned,sessionId:clientId,
    orientation:quat(relative),rotationMatrix:relative,angularVelocity,acceleration,
    angularSpeed:Math.hypot(...angularVelocity),
    raw:{alpha:rel(raw.alpha,zero.alpha),beta:rel(raw.beta,zero.beta),gamma:rel(raw.gamma,zero.gamma)}
  });
  try{
    await fetch('/racket-frame',{method:'POST',headers:{'content-type':'application/json'},body});
    $('sent').textContent=String(++sent);
  }catch(e){$('sensor').textContent='send failed';$('sensor').className='bad';}
  sending=false;
}
$('start').onclick=start;$('record').onclick=record;$('hint').textContent=poses[0];
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
