FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/Api/AiSupportAgent.Api/*.csproj ./
RUN dotnet restore
COPY src/Api/AiSupportAgent.Api/. ./
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
ENV DOTNET_gcServer=0
ENV DOTNET_GCConserveMemory=9
ENTRYPOINT ["sh","-c","ASPNETCORE_URLS=http://0.0.0.0:$PORT dotnet AiSupportAgent.Api.dll"]