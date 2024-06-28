FROM ubuntu:latest
WORKDIR /app

# initial dependencies
RUN apt update
RUN apt install wget libopus-dev ffmpeg dotnet-sdk-8.0 -y

# yt-dlp
<<<<<<< HEAD
RUN wget https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -O ~/.local/bin/yt-dlp
RUN chmod a+rx ~/.local/bin/yt-dlp  # Make executable
=======
RUN wget https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -O ~/.local/bin/yt-dlp &&\
chmod a+rx ~/.local/bin/yt-dlp  # Make executable
>>>>>>> 2e94d77d0966213648aabac05c68af02b5473d33
RUN printenv PATH

COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs .

RUN dotnet build --configuration Release .

ENTRYPOINT ["dotnet", "run", "--configuration", "Release", "."]