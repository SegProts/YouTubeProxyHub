self.addEventListener('install', (event) => self.skipWaiting());

const pendingRequests = new Map();

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    if (url.pathname.startsWith('/proxy-video')) {
        const targetUrl = url.searchParams.get('url');
        const range = event.request.headers.get('range') || "bytes=0-";

        event.respondWith(new Promise(async (resolve) => {
            const requestId = Math.random().toString(36).substring(7);
            const clientsList = await clients.matchAll();
            const client = clientsList.find(c => c.type === 'window');

            if (!client) return resolve(fetch(targetUrl)); // Fallback

            pendingRequests.set(requestId, resolve);

            client.postMessage({
                type: 'request-chunk',
                requestId: requestId,
                url: targetUrl,
                range: range
            });
        }));
    }
});

self.addEventListener('message', (event) => {
    if (event.data.type === 'deliver-chunk') {
        const resolve = pendingRequests.get(event.data.requestId);
        if (resolve) {
            const binary = atob(event.data.base64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

            // Если чанк пустой - это ошибка
            if (bytes.length === 0) {
                console.error("SW: Получен пустой чанк");
                resolve(new Response(null, { status: 404 }));
                return;
            }

            resolve(new Response(bytes, {
                status: 206,
                headers: {
                    'Content-Type': 'video/mp4',
                    'Content-Range': event.data.range,
                    'Accept-Ranges': 'bytes',
                    'Content-Length': bytes.length
                }
            }));
            pendingRequests.delete(event.data.requestId);
        }
    }
});