// 페이지 이탈 시 저장하지 않은 변경사항 경고 처리 (beforeunload 이벤트)
// Blazor의 DirtyGuardBase에서 InvokeVoidAsync("dirtyGuard.setDirty", bool)으로 호출됨
window.dirtyGuard = {
    // 현재 등록된 beforeunload 핸들러 참조 (null이면 미등록 상태)
    _handler: null,

    // isDirty가 true이면 beforeunload 리스너 등록, false이면 해제
    // 중복 등록을 방지하기 위해 _handler 유무를 확인
    setDirty: function (isDirty) {
        if (isDirty && !this._handler) {
            // 브라우저 탭 닫기 / 새로고침 / 주소 직접 입력 시 경고 표시
            this._handler = function (e) {
                e.preventDefault();
                e.returnValue = ''; // Chrome 등에서 경고 다이얼로그 표시
            };
            window.addEventListener('beforeunload', this._handler);
        } else if (!isDirty && this._handler) {
            // 저장 완료 또는 컴포넌트 해제 시 리스너 제거
            window.removeEventListener('beforeunload', this._handler);
            this._handler = null;
        }
    }
};
