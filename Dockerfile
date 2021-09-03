FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["JSDXTS.csproj", "./"]
RUN dotnet restore "JSDXTS.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "JSDXTS.csproj" -c Release -o /app/build
    
FROM build AS publish
RUN dotnet publish "JSDXTS.csproj" -c Release  -o /app/publish \
    --self-contained false \
    /p:PublishSingleFile=true

FROM mcr.microsoft.com/dotnet/runtime:5.0-alpine AS final
MAINTAINER "lpyedge"
LABEL version="0.5"
LABEL name="js.189.cn-speedup"
LABEL url="https://hub.docker.com/repository/docker/lpyedge/js.189.cn-speedup"
LABEL email="lpyedge#163.com"

WORKDIR /app
COPY --from=publish /app/publish .
ENV TZ=Asia/Shanghai
ENV delay=30

ENTRYPOINT ["/app/JSDXTS"]