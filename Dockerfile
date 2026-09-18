FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY VideoEncoder/VideoEncoder.csproj VideoEncoder/
RUN dotnet restore VideoEncoder/VideoEncoder.csproj

COPY VideoEncoder/ VideoEncoder/
RUN dotnet publish VideoEncoder/VideoEncoder.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install --no-install-recommends --yes ffmpeg \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/Videos

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "VideoEncoder.dll"]
