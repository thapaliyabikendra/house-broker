// Minimal Chrome DevTools Protocol driver (Node 24 built-in WebSocket). Lives outside the repo.
import { writeFileSync } from 'node:fs';

export async function openPage() {
  const res = await fetch('http://localhost:9222/json/new?about:blank', { method: 'PUT' });
  const target = await res.json();
  const ws = new WebSocket(target.webSocketDebuggerUrl);
  await new Promise((ok, err) => { ws.onopen = ok; ws.onerror = err; });
  let id = 0;
  const pending = new Map();
  const listeners = [];
  ws.onmessage = (ev) => {
    const msg = JSON.parse(ev.data);
    if (msg.id && pending.has(msg.id)) {
      const { resolve, reject } = pending.get(msg.id);
      pending.delete(msg.id);
      msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
    } else if (msg.method) {
      listeners.forEach((l) => l(msg));
    }
  };
  const send = (method, params = {}) => new Promise((resolve, reject) => {
    const mid = ++id;
    pending.set(mid, { resolve, reject });
    ws.send(JSON.stringify({ id: mid, method, params }));
  });
  await send('Page.enable');
  await send('Runtime.enable');
  await send('Network.enable');

  const page = {
    send,
    on: (fn) => listeners.push(fn),
    async goto(url) {
      await send('Page.navigate', { url });
      await page.waitIdle();
    },
    async waitIdle(ms = 800) {
      await new Promise((r) => setTimeout(r, ms));
      for (let i = 0; i < 40; i++) {
        const { result } = await send('Runtime.evaluate', { expression: 'document.readyState', returnByValue: true });
        if (result.value === 'complete') break;
        await new Promise((r) => setTimeout(r, 250));
      }
      await new Promise((r) => setTimeout(r, ms));
    },
    async eval(expression) {
      const r = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
      if (r.exceptionDetails) throw new Error(JSON.stringify(r.exceptionDetails));
      return r.result.value;
    },
    async screenshot(path) {
      const { data } = await send('Page.captureScreenshot', { format: 'png' });
      writeFileSync(path, Buffer.from(data, 'base64'));
    },
    async close() {
      ws.close();
      await fetch(`http://localhost:9222/json/close/${target.id}`);
    },
  };
  return page;
}
