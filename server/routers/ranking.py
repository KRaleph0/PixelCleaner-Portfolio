from fastapi import APIRouter, Depends, Query
from sqlalchemy import select
from sqlalchemy.ext.asyncio import AsyncSession

from database import get_db
from models import DeliveryRecord, Player
from ranking_core import competition_rank, ranking_page, visible_total
from schemas import RankEntry, RankingResponse

router = APIRouter(prefix="/ranking", tags=["ranking"])


async def _my_entry(db: AsyncSession, player_id: str) -> RankEntry | None:
    row = (
        await db.execute(
            select(DeliveryRecord, Player)
            .join(Player, DeliveryRecord.player_id == Player.id)
            .where(DeliveryRecord.player_id == player_id)
        )
    ).one_or_none()
    if not row:
        return None
    record, player = row
    rank = await competition_rank(db, record.score)
    return RankEntry(
        rank=rank, display_name=player.display_name, score=record.score, is_me=True
    )


@router.get("", response_model=RankingResponse)
async def get_ranking(
    limit: int = Query(10, le=100),
    offset: int = Query(0, ge=0),
    me: str | None = Query(
        default=None,
        description="내 player_id. 주면 해당 줄에 is_me=true가 붙고 me 블록이 함께 온다.",
    ),
    db: AsyncSession = Depends(get_db),
):
    total = await visible_total(db)
    rows = await ranking_page(db, limit=limit, offset=offset)

    # 목록에서 '내 줄'을 찾는 문제를, player_id를 전부 공개하지 않고 해결한다.
    # 전원의 player_id를 응답에 넣으면 그 자체가 점수 조작 대상 목록이 된다.
    entries = [
        RankEntry(rank=rank, display_name=name, score=score, is_me=(me is not None and pid == me))
        for rank, name, score, pid in rows
    ]

    my_entry = await _my_entry(db, me) if me else None
    return RankingResponse(entries=entries, total=total, me=my_entry)


@router.get("/me", response_model=RankEntry)
async def get_my_rank(player_id: str, db: AsyncSession = Depends(get_db)):
    entry = await _my_entry(db, player_id)
    if entry is None:
        # 미등록/미제출은 404가 아니라 rank 0으로 답한다 (클라이언트가 의존하는 기존 동작).
        return RankEntry(rank=0, display_name="?", score=0, is_me=False)
    return entry
