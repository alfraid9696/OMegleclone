# Strangers Call (Omegle Clone)

A random video chat web app where registered users are matched with strangers for live video calls and text chat.

Visit: https://omegleclone-xunt.onrender.com

## Features

- User registration and login
- Random matchmaking queue
- Peer-to-peer video and audio (WebRTC)
- Real-time text chat (SignalR)
- Online user count
- Profile page (name, age, gender, country)
- Skip to next person

## Tech Stack

- ASP.NET Core 8 (Razor Pages)
- ASP.NET Identity
- SignalR
- WebRTC
- Entity Framework Core (SQL Server / SQLite)
- Bootstrap
- Docker + Render

## Run Locally

**Requirements:** .NET 8 SDK, SQL Server LocalDB

```bash
cd RandomVideoCallWebpage
dotnet run
