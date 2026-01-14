FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Устанавливаем зависимости для yt-dlp
RUN apt-get update && apt-get install -y python3 python3-pip curl
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp
RUN chmod a+rx /usr/local/bin/yt-dlp

COPY ["YouTubeProxyHub.csproj", "./"]
RUN dotnet restore "YouTubeProxyHub.csproj"

COPY . .
RUN dotnet publish "YouTubeProxyHub.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
# Копируем yt-dlp из этапа сборки
COPY --from=build /usr/local/bin/yt-dlp /usr/local/bin/yt-dlp
RUN apt-get update && apt-get install -y python3 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "YouTubeProxyHub.dll"]