using Framework.Domain.Interfaces;

namespace Framework.Tests.Infrastructure;

// IUnitOfWork NSubstitute 헬퍼 — ExecuteInTransactionAsync 두 오버로드를 패스스루로 설정
// 실제 트랜잭션 없이 람다를 그대로 실행하도록 대리자를 구성
public static class UnitOfWorkSubstitute
{
    // 패스스루가 완전히 구성된 IUnitOfWork 대리자를 생성·반환
    public static IUnitOfWork CreatePassthrough()
    {
        var uow = Substitute.For<IUnitOfWork>();

        // 제네릭 오버로드와 void 오버로드를 모두 패스스루로 초기화
        ConfigurePassthroughVoid(uow);

        return uow;
    }

    // Task<T> 오버로드 패스스루 설정 — 호출된 람다를 그대로 실행하여 결과 반환
    // 호출자 코드에서 반환 타입이 확정된 뒤 개별 호출로 설정
    public static void ConfigurePassthrough<T>(IUnitOfWork uow)
    {
        // NSubstitute 패스스루: 인수로 전달된 Func<Task<T>>를 직접 호출하여 결과 반환
        uow.ExecuteInTransactionAsync(Arg.Any<Func<Task<T>>>())
           .Returns(ci => ci.Arg<Func<Task<T>>>()());
    }

    // Task(반환값 없음) 오버로드 패스스루 설정 — 호출된 람다를 그대로 실행
    public static void ConfigurePassthroughVoid(IUnitOfWork uow)
    {
        // NSubstitute 패스스루: 인수로 전달된 Func<Task>를 직접 호출
        uow.ExecuteInTransactionAsync(Arg.Any<Func<Task>>())
           .Returns(ci => ci.Arg<Func<Task>>()());
    }
}
