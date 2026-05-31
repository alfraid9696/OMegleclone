FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY RandomVideoCallWebpage.csproj ./
COPY Program.cs ./
COPY appsettings*.json ./
COPY Hubs/ Hubs/
COPY Services/ Services/
COPY Models/ Models/
COPY Pages/ Pages/
COPY Properties/ Properties/
COPY wwwroot/ wwwroot/

RUN dotnet restore
RUN dotnet publish RandomVideoCallWebpage.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "RandomVideoCallWebpage.dll"]
