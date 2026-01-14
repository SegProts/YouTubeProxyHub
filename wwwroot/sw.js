// v2 - Принудительное обновление
self.addEventListener('install', (event) => {
    self.skipWaiting();
});

const pendingRequests = new Map();

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    if (url.pathname.startsWith('/proxy-video')) {
        const targetUrl = url.searchParams.get('url');
        const range = event.request.headers.get('range') || "bytes=0-";

        event.respondWith(new Promise(async (resolve) => {
            const requestId = Math.random().toString(36).substring(7);
            const allClients = await clients.matchAll();
            const client = allClients.find(c => c.type === 'window');

            if (!client) {
                // Если нет окна, пробуем обычный запрос (но это не сработает из-за CORS)
                return resolve(fetch(event.request));
            }

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
            const binaryString = atob(event.data.base64);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
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