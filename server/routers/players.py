from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from config import DEVICE_ID_MAX, DISPLAY_NAME_MAX
from database import get_db
from models import Player
from schemas import PlayerRegisterRequest, PlayerResponse

router = APIRouter(prefix="/players", tags=["players"])


@router.post("/register", response_model=PlayerResponse)
async def register(body: PlayerRegisterRequest, db: AsyncSession = Depends(get_db)):
    # 검증을 스키마가 아니라 여기서 하는 이유는 schemas.py 주석 참조 (422 대신 400).
    device_id = body.device_id.strip()
    display_name = body.display_name.strip()

    if not device_id or len(device_id) > DEVICE_ID_MAX:
        raise HTTPException(status_code=400, detail="Invalid device_id")
    # 예전에는 빈 닉네임도 200으로 통과했다. 공백만 넣은 경우도 빈 닉네임으로 본다.
    if not display_name:
        raise HTTPException(status_code=400, detail="display_name must not be empty")
    if len(display_name) > DISPLAY_NAME_MAX:
        raise HTTPException(
            status_code=400, detail=f"display_name must be at most {DISPLAY_NAME_MAX} characters"
        )

    result = await db.execute(select(Player).where(Player.device_id == device_id))
    player = result.scalar_one_or_none()

    if player:
        # 이름 변경 허용. 토큰은 재발급하지 않는다 — 다른 기기·세션에 남은 토큰을
        # 무효화할 이유가 없고, 클라가 재등록을 자주 호출해도 안전해야 한다.
        player.display_name = display_name
    else:
        player = Player(device_id=device_id, display_name=display_name)
        db.add(player)

    await db.commit()
    await db.refresh(player)
    return PlayerResponse(
        player_id=player.id, display_name=player.display_name, token=player.auth_token or ""
    )
