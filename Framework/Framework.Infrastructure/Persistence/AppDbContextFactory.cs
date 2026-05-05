using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Framework.Infrastructure.Persistence;

// EF Core 디자인타임 DbContext 팩토리 — migrations add/update 실행 시 사용
// [배경] DI 컨테이너를 통한 DbContext 생성 불가 시(circular dependency 등) 이 팩토리로 대체
// [참고] 프로덕션 런타임에서는 사용되지 않으며, 오직 dotnet ef 도구에서만 호출됨
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // 디자인타임 전용 연결 문자열 — 개발 환경 로컬 PostgreSQL 기준
        // 실제 연결 정보는 Framework.Api/appsettings.Development.json 참조
        var connectionString = "Host=localhost;Port=5432;Database=framework_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
