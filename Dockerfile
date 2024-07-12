FROM ubuntu:latest
WORKDIR /app
RUN mkdir /app/bin
ENV PATH="$PATH:/app/bin"

# initial dependencies
RUN apt update
RUN apt install libopus-dev ffmpeg dotnet-sdk-8.0 -y

# rebuild project
COPY dotnetDiscordBot.csproj .
COPY dotnetDiscordBot.sln .
COPY *.cs ./
RUN dotnet publish -c Release -o dotnetDiscordBot

# remove source
RUN rm *.cs

CMD ["./dotnetDiscordBot"]