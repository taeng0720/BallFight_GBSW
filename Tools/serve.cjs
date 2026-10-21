const http = require('http');
const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '../Builds/WebGL');
const types = { '.html': 'text/html; charset=utf-8', '.js': 'application/javascript', '.wasm': 'application/wasm', '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css', '.png': 'image/png' };
http.createServer((request, response) => {
  let filename;
  try { filename = path.resolve(root, '.' + decodeURIComponent(new URL(request.url, 'http://localhost').pathname)); }
  catch { response.writeHead(400); response.end(); return; }
  if (filename !== root && !filename.startsWith(root + path.sep)) { response.writeHead(403); response.end(); return; }
  if (filename === root || filename.endsWith(path.sep)) filename = path.join(filename, 'index.html');
  fs.stat(filename, (error, stat) => {
    if (error || !stat.isFile()) { response.writeHead(404); response.end('WebGL build not found'); return; }
    response.writeHead(200, { 'Content-Type': types[path.extname(filename)] || 'application/octet-stream', 'Content-Length': stat.size });
    fs.createReadStream(filename).pipe(response);
  });
}).listen(8080, '127.0.0.1', () => console.log('http://localhost:8080'));
