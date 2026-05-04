using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Framework.Application.Common;

// PostgreSQL UNIQUE 제약 위반(SqlState 23505) 판별 확장 메서드
// 메시지 매칭(Npgsql 버전/언어팩 의존)을 SqlState 명시 비교로 전환 — M-46
// 5곳 중복 IsUniqueViolation private 메서드를 단일 헬퍼로 통합 — M-18
public static class DbUpdateExceptionExtensions
{
    // InnerException이 PostgresException이고 SqlState가 23505(UniqueViolation)인지 확인
    // SqlState 상수 비교 방식 — 메시지 패턴 매칭(버전/언어팩 의존) 대신 표준 에러 코드로 판별
    public static bool IsUniqueViolation(this DbUpdateException ex)
        => ex.InnerException is PostgresException pg
           && pg.SqlState == PostgresErrorCodes.UniqueViolation;
}
