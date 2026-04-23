# ─────────────────────────────────────────────────────────────
# Framework.Api Dockerfile
#
# [빌드 컨텍스트]
# docker-compose.yml에서 context: ./Framework 으로 설정되어 있음.
# 따라서 COPY 경로는 Framework/ 폴더 기준 상대 경로로 작성한다.
#
# [멀티 스테이지 빌드 이유]
# 1단계(SDK)에서 컴파일만 하고, 2단계(ASP.NET 런타임)에 결과물만 복사하여
# 최종 이미지에서 SDK를 제거한다. 이미지 용량이 크게 줄어든다.
# ─────────────────────────────────────────────────────────────

# ─── 1단계: 빌드 ───
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj 파일만 먼저 복사한 뒤 restore 실행
# → 코드만 바뀌고 의존성은 그대로일 때 restore 레이어를 캐시에서 재사용
COPY Framework.Api/Framework.Api.csproj Framework.Api/
COPY Framework.Application/Framework.Application.csproj Framework.Application/
COPY Framework.Domain/Framework.Domain.csproj Framework.Domain/
COPY Framework.Infrastructure/Framework.Infrastructure.csproj Framework.Infrastructure/
RUN dotnet restore Framework.Api/Framework.Api.csproj

# 전체 소스 복사 후 Release 빌드로 publish
COPY Framework.Api/ Framework.Api/
COPY Framework.Application/ Framework.Application/
COPY Framework.Domain/ Framework.Domain/
COPY Framework.Infrastructure/ Framework.Infrastructure/
RUN dotnet publish Framework.Api/Framework.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ─── 2단계: 런타임 ───
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 빌드 결과물만 복사 (SDK 제외)
COPY --from=build /app/publish .

# Kestrel을 컨테이너 내부 8080 포트에 바인딩
# HTTPS는 앞단 리버스 프록시(Caddy)가 처리하므로 내부는 HTTP만 사용
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# 컨테이너 시작 시 API 실행
ENTRYPOINT ["dotnet", "Framework.Api.dll"]
