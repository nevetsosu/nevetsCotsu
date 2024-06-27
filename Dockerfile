FROM ubuntu:latest
WORKDIR /app

# initial dependencies
RUN apt update
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0 -y

# yt-dlp
RUN wget https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -O ~/.local/bin/yt-dlp \
chmod a+rx ~/.local/bin/yt-dlp  # Make executable
RUN printenv PATH

COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet build --configuration Release .

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "."]