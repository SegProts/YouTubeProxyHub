self.addEventListener('fetch', (event) => {
    const url = new URL(event.request.url);

    // Если запрос идет через наш прокси-префикс
    if (url.pathname.startsWith('/proxy-video')) {
        const realUrl = url.searchParams.get('url');
        console.log("SW перехватил запрос к YouTube:", realUrl);

        // Пока просто пробрасываем запрос напрямую, но через SW
        event.respondWith(fetch(realUrl));
    }
});