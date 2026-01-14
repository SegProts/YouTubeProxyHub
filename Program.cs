using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace YouTubeProxyHub
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Читаем порт из переменной окружения Render (по умолчанию 8080)
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy.AllowAnyHeader()
                        .AllowAnyMethod()
                        .SetIsOriginAllowed(_ => true)
                        .AllowCredentials();
                });
            });

            builder.Services.AddSignalR();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Включаем Swagger всегда для тестов
            app.UseSwagger();
            app.UseSwaggerUI();

            // На Render HTTPS redirection не нужен внутри контейнера
            if (!app.Environment.IsDevelopment())
            {
                app.UseHsts();
            }

            app.UseCors("AllowAll");

            app.MapHub<YouTubeProxyHub>("/proxyhub");
            app.MapGet("/", () => "YouTube Proxy Hub is running on Render!");

            app.Run();
        }
    }
}