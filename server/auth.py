"""점수 제출 인증.

player_id는 UUID라 추측은 어렵지만 APK나 트래픽에서 바로 노출된다.
그것만으로 임의 점수를 제출할 수 있으면 랭킹은 집계로서 의미가 없다.

배포는 두 단계로 나눈다.
  1단계 AUTH_ENFORCE=false — 토큰을 발급하되 검증 실패는 경고 로그만. 구버전 클라가 계속 동작한다.
  2단계 AUTH_ENFORCE=true  — 401/403으로 거절.
"""
import logging

from fastapi import Header, HTTPException

from config import AUTH_ENFORCE
from models import Player

log = logging.getLogger("pixelcleaners.auth")


def extract_bearer(authorization: str | None) -> str | None:
    if not authorization:
        return None
    parts = authorization.split(None, 1)
    if len(parts) != 2 or parts[0].lower() != "bearer":
        return None
    return parts[1].strip() or None


def verify_player_token(player: Player, authorization: str | None) -> None:
    """player가 이미 조회된 뒤에 호출할 것.

    없는 player_id는 이 함수에 오기 전에 404가 나가야 한다. 클라이언트가 그 404를
    받아 자동 재등록하는 흐름에 의존하고 있으므로, 인증 오류가 404를 가리면 안 된다.
    """
    token = extract_bearer(authorization)

    if token is None:
        if AUTH_ENFORCE:
            raise HTTPException(status_code=401, detail="Missing bearer token")
        log.warning("auth: 토큰 없는 제출 (player=%s) — 경고 모드라 통과시킴", player.id)
        return

    if not player.auth_token or token != player.auth_token:
        if AUTH_ENFORCE:
            raise HTTPException(status_code=403, detail="Token/player mismatch")
        log.warning("auth: 토큰 불일치 (player=%s) — 경고 모드라 통과시킴", player.id)


async def authorization_header(authorization: str | None = Header(default=None)) -> str | None:
    return authorization
