self.addEventListener('install', () => self.skipWaiting());

const requests = new Map();

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    if (url.pathname.startsWith('/proxy-video')) {
        const targetUrl = url.searchParams.get('url');
        const range = event.request.headers.get('range') || "bytes=0-";

        event.respondWith(new Promise(async (resolve) => {
            const requestId = Math.random().toString(36).substring(2, 11);
            requests.set(requestId, resolve);

            const allClients = await clients.matchAll();
            const client = allClients[0];

            if (client) {
                console.log(`[SW] Запрос чанка ${range} (ID: ${requestId})`);
                client.postMessage({
                    type: 'request-chunk',
                    requestId: requestId,
                    url: targetUrl,
                    range: range
                });
            } else {
                resolve(new Response("No active window", { status: 503 }));
            }
        }));
    }
});

self.addEventListener('message', (event) => {
    if (event.data.type === 'deliver-chunk') {
        const { requestId, base64, range } = event.data;
        const resolve = requests.get(requestId);

        if (resolve) {
            const binary = atob(base64);
            const bytes = new Uint8Array(binary.length);
            for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

            console.log(`[SW] Чанк получен для ID: ${requestId}`);
            resolve(new Response(bytes, {
                status: 206,
                headers: {
                    'Content-Type': 'video/mp4',
                    'Content-Range': range,
                    'Accept-Ranges': 'bytes',
                    'Content-Length': bytes.length
                }
            }));
            requests.delete(requestId);
        }
    }
});