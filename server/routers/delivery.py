import logging
from datetime import timedelta

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy import func, select
from sqlalchemy.ext.asyncio import AsyncSession

from auth import authorization_header, verify_player_token
from config import POINTS_PER_ITEM, SCORE_RATE_LIMIT_PER_HOUR
from database import get_db
from models import DeliveryRecord, DeliverySubmission, Player
from ranking_core import competition_rank
from schemas import DeliverySubmitRequest, DeliverySubmitResponse
from timeutil import utcnow

router = APIRouter(prefix="/delivery", tags=["delivery"])
log = logging.getLogger("pixelcleaners.delivery")


async def _flag_suspicious_rate(db: AsyncSession, player_id: str, delta: int) -> None:
    """시간당 획득량에는 물리적 한계가 있다. 넘으면 기록만 남긴다 (거절하지 않음).

    임계값은 리허설에서 정상 플레이가 걸리는지 보고 조정할 것.
    """
    if delta <= 0:
        return
    since = utcnow() - timedelta(hours=1)
    recent = (
        await db.execute(
            select(func.coalesce(func.sum(DeliverySubmission.delta), 0)).where(
                DeliverySubmission.player_id == player_id,
                DeliverySubmission.submitted_at >= since,
            )
        )
    ).scalar() or 0
    if recent + delta > SCORE_RATE_LIMIT_PER_HOUR:
        log.warning(
            "rate: player=%s 최근 1시간 증가분 %d (+%d) — 임계값 %d 초과",
            player_id, recent, delta, SCORE_RATE_LIMIT_PER_HOUR,
        )


@router.post("/submit", response_model=DeliverySubmitResponse)
async def submit(
    body: DeliverySubmitRequest,
    db: AsyncSession = Depends(get_db),
    authorization: str | None = Depends(authorization_header),
):
    # 플레이어 확인이 먼저다. 없는 player_id에 404가 나가야 클라이언트가 자동
    # 재등록 후 재제출한다. 인증 오류(401/403)가 이 404를 가리면 그 흐름이 깨진다.
    player = await db.get(Player, body.player_id)
    if not player:
        raise HTTPException(status_code=404, detail="Player not found")

    verify_player_token(player, authorization)

    # score 검증: 음수 불가 + 납품 단가의 배수여야 함
    if body.score < 0:
        raise HTTPException(status_code=400, detail="Invalid score")
    if POINTS_PER_ITEM > 0 and body.score % POINTS_PER_ITEM != 0:
        raise HTTPException(status_code=400, detail="Invalid score")

    result = await db.execute(
        select(DeliveryRecord).where(DeliveryRecord.player_id == body.player_id)
    )
    record = result.scalar_one_or_none()

    previous = record.score if record else 0
    # 클라는 누적 총점을 보낸다. 증가분은 서버가 직전 최고점과의 차로 계산한다.
    delta = max(0, body.score - previous)

    if record:
        if body.score > record.score:   # 최댓값 유지 — 기존 동작
            record.score = body.score
    else:
        record = DeliveryRecord(player_id=body.player_id, score=body.score)
        db.add(record)

    # 제출 이력은 최고점 갱신 여부와 무관하게 항상 남긴다 (사후 추적·이벤트 집계용).
    db.add(
        DeliverySubmission(player_id=body.player_id, total_score=body.score, delta=delta)
    )
    await _flag_suspicious_rate(db, body.player_id, delta)

    await db.commit()
    await db.refresh(record)

    rank = await competition_rank(db, record.score)
    return DeliverySubmitResponse(accepted_score=record.score, rank=rank)
