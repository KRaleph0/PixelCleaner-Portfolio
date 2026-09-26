"""이벤트 API.

설계상 두 가지를 지킨다.
  1) 기간 판정은 전적으로 서버가 한다. 기기 시계는 사용자가 바꿀 수 있다.
  2) score_multiplier는 이벤트 집계에만 곱한다. 상시 랭킹 점수에 곱하면 안 된다 —
     클라가 보내는 값이 누적 총점이고 서버는 최댓값을 유지하므로, 곱한 값이 저장되면
     이벤트가 끝난 뒤에도 부풀린 점수가 남아 한동안 점수가 멈춰 보인다.
"""
from datetime import datetime, timezone
from zoneinfo import ZoneInfo

from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy import Integer, cast, func, select
from sqlalchemy.ext.asyncio import AsyncSession

from config import DISPLAY_TIMEZONE
from database import get_db
from models import DeliverySubmission, Event, Player
from ranking_core import visibility_clause
from schemas import EventInfo, EventListResponse, RankEntry, RankingResponse
from timeutil import utcnow

router = APIRouter(prefix="/events", tags=["events"])

try:
    DISPLAY_TZ = ZoneInfo(DISPLAY_TIMEZONE)
except Exception:                                    # tzdata가 없는 환경 대비
    DISPLAY_TZ = timezone.utc


def _to_display(dt: datetime) -> str:
    """DB에는 naive UTC로 저장한다. 응답에서만 표시 타임존으로 바꾼다."""
    return dt.replace(tzinfo=timezone.utc).astimezone(DISPLAY_TZ).isoformat()


def _event_info(ev: Event, now: datetime) -> EventInfo:
    return EventInfo(
        event_id=ev.event_id,
        title=ev.title,
        description=ev.description or "",
        starts_at=_to_display(ev.starts_at),
        ends_at=_to_display(ev.ends_at),
        score_multiplier=ev.score_multiplier,
        banner_color=ev.banner_color,
        server_time=_to_display(now),
    )


@router.get("/active", response_model=EventListResponse)
async def active_events(db: AsyncSession = Depends(get_db)):
    now = utcnow()
    rows = (
        await db.execute(
            select(Event)
            .where(Event.is_active == 1, Event.starts_at <= now, Event.ends_at >= now)
            .order_by(Event.starts_at.asc())
        )
    ).scalars().all()
    # 진행 중인 이벤트가 없으면 빈 배열. 클라는 배너를 감춘다.
    return EventListResponse(events=[_event_info(ev, now) for ev in rows])


@router.get("/{event_id}/ranking", response_model=RankingResponse)
async def event_ranking(
    event_id: str,
    limit: int = Query(10, le=100),
    offset: int = Query(0, ge=0),
    me: str | None = Query(default=None),
    db: AsyncSession = Depends(get_db),
):
    ev = await db.get(Event, event_id)
    if not ev or not ev.is_active:
        raise HTTPException(status_code=404, detail="Event not found")

    # 기간 내 증가분 합계. DeliveryRecord는 최고점만 갖고 있어 기간 집계를 못 하므로
    # 제출 이력(delta)을 쓴다.
    agg = (
        select(
            DeliverySubmission.player_id.label("pid"),
            func.sum(DeliverySubmission.delta).label("raw"),
            func.min(DeliverySubmission.submitted_at).label("first_at"),
        )
        .where(
            DeliverySubmission.submitted_at >= ev.starts_at,
            DeliverySubmission.submitted_at <= ev.ends_at,
            DeliverySubmission.delta > 0,
        )
        .group_by(DeliverySubmission.player_id)
        .subquery()
    )

    scaled = agg.c.raw * ev.score_multiplier
    ranked = (
        select(
            agg.c.pid.label("pid"),
            cast(scaled, Integer).label("score"),
            agg.c.first_at.label("first_at"),
            Player.display_name.label("display_name"),
            func.rank().over(order_by=scaled.desc()).label("rank"),
        )
        .join(Player, Player.id == agg.c.pid)
    )
    clause = visibility_clause()
    if clause is not None:
        ranked = ranked.where(clause)
    sub = ranked.subquery()

    total = (await db.execute(select(func.count()).select_from(sub))).scalar() or 0

    rows = (
        await db.execute(
            select(sub.c.rank, sub.c.display_name, sub.c.score, sub.c.pid)
            .order_by(sub.c.score.desc(), sub.c.first_at.asc(), sub.c.pid.asc())
            .offset(offset)
            .limit(limit)
        )
    ).all()

    entries = [
        RankEntry(
            rank=int(r[0]), display_name=r[1], score=int(r[2]),
            is_me=(me is not None and r[3] == me),
        )
        for r in rows
    ]

    my_entry = None
    if me:
        row = (
            await db.execute(
                select(sub.c.rank, sub.c.display_name, sub.c.score).where(sub.c.pid == me)
            )
        ).one_or_none()
        if row:
            my_entry = RankEntry(
                rank=int(row[0]), display_name=row[1], score=int(row[2]), is_me=True
            )

    return RankingResponse(entries=entries, total=total, me=my_entry)
