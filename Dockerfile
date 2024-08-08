FROM mcr.microsoft.com/dotnet/sdk:6.0  AS build

WORKDIR /build

COPY *.csproj .

RUN dotnet restore

COPY . .

RUN dotnet publish --no-restore -o /app

FROM mcr.microsoft.com/dotnet/sdk:6.0

WORKDIR /app

COPY --from=build /app .

COPY ./Data/HTML/ ./Data/HTML
COPY ./Data/Configs ./Data/Configs
COPY ./App.config ./App.config

RUN mkdir -p ./Data/Logs

EXPOSE 2020

ENTRYPOINT [ "./Faira" ]