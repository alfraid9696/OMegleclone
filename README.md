

A random video chat web app where registered users are matched with strangers for live video calls and text chat.

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
<<<<<<< HEAD
=======

## Run Locally

**Requirements:** .NET 8 SDK, SQL Server LocalDB

```bash
cd RandomVideoCallWebpage
dotnet run
```

Open `https://localhost:5001` (or the URL shown in the terminal).

Register an account, click **Start**, and allow camera/microphone access. Open a second browser window (or incognito) with another account to test matching.

## Deploy

The project includes a `Dockerfile` and `render.yaml` for deployment on [Render](https://render.com).

Production uses SQLite (`/app/data/app.db`). For persistent user data, use a hosted database such as Render PostgreSQL.

## Project Structure

```
RandomVideoCallWebpage/
├── Hubs/ChatHub.cs          # SignalR hub (matchmaking, chat, signaling)
├── Services/                # Matchmaking & online presence
├── Pages/                   # Razor Pages UI
└── wwwroot/js/chat.js       # WebRTC + SignalR client
```

## Author

Developed by **Ankit Chuarasiya**
>>>>>>> 5df2791 (added new features and auth system)
