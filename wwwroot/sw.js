const pendingRequests = new Map();

self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    if (url.pathname.startsWith('/proxy-video')) {
        const targetUrl = url.searchParams.get('url');
        const range = event.request.headers.get('range') || "bytes=0-";

        event.respondWith(new Promise(async (resolve) => {
            const requestId = Math.random().toString(36).substring(7);

            // Создаем канал связи с основной страницей (index.html)
            const client = await clients.get(event.clientId);
            if (!client) return resolve(fetch(event.request));

            pendingRequests.set(requestId, resolve);

            // Просим страницу скачать чанк через SignalR
            client.postMessage({
                type: 'request-chunk',
                requestId: requestId,
                url: targetUrl,
                range: range
            });
        }));
    }
});

// Получаем байты от страницы и отдаем их браузеру
self.addEventListener('message', (event) => {
    if (event.data.type === 'deliver-chunk') {
        const resolve = pendingRequests.get(event.data.requestId);
        if (resolve) {
            const binaryString = atob(event.data.base64);
            const bytes = new Uint8Array(binaryString.length);
            for (let i = 0; i < binaryString.length; i++) {
                bytes[i] = binaryString.charCodeAt(i);
            }

            const response = new Response(bytes, {
                status: 206,
                statusText: 'Partial Content',
                headers: {
                    'Content-Type': 'video/webm',
                    'Content-Range': event.data.range,
                    'Accept-Ranges': 'bytes'
                }
            });
            resolve(response);
            pendingRequests.delete(event.data.requestId);
        }
    }
});