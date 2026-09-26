"""랭킹 계산 공통 로직.

기존에는 세 곳이 rank를 제각각 계산했다.
  GET  /ranking       → 정렬된 행의 순번 (offset + i + 1)   ... 동점자에게 1, 2, 3
  GET  /ranking/me    → count(score > 내점수) + 1            ... 동점자 모두 같은 순위
  POST /delivery/submit → /ranking/me와 동일
그래서 같은 플레이어가 목록에서는 2위, 내 순위 조회에서는 1위로 보였다.
전부 경쟁 순위(동점자 같은 순위, 다음 순위는 건너뜀 — 1, 1, 3)로 통일한다.
"""
from sqlalchemy import Integer, and_, func, not_, or_, select
from sqlalchemy.ext.asyncio import AsyncSession

from config import RANKING_EXCLUDE_PREFIXES
from models import DeliveryRecord, Player


def visibility_clause():
    """랭킹에서 감출 계정 조건. 리허설용 device_id 접두사를 걸러낸다."""
    if not RANKING_EXCLUDE_PREFIXES:
        return None
    return not_(or_(*[Player.device_id.like(f"{p}%") for p in RANKING_EXCLUDE_PREFIXES]))


def _base_join():
    stmt = select(DeliveryRecord, Player).join(Player, DeliveryRecord.player_id == Player.id)
    clause = visibility_clause()
    return stmt if clause is None else stmt.where(clause)


async def visible_total(db: AsyncSession) -> int:
    """랭킹에 노출되는 기록 수.

    기존 코드는 join 없이 delivery_records를 세었다. 그래서 플레이어가 지워진
    고아 행이 있으면 total과 entries 개수가 어긋났다. 항상 같은 조건으로 센다.
    """
    stmt = select(func.count()).select_from(DeliveryRecord).join(
        Player, DeliveryRecord.player_id == Player.id
    )
    clause = visibility_clause()
    if clause is not None:
        stmt = stmt.where(clause)
    return (await db.execute(stmt)).scalar() or 0


async def competition_rank(db: AsyncSession, score: int) -> int:
    """해당 점수의 경쟁 순위. 나보다 높은 점수의 수 + 1."""
    stmt = (
        select(func.count())
        .select_from(DeliveryRecord)
        .join(Player, DeliveryRecord.player_id == Player.id)
        .where(DeliveryRecord.score > score)
    )
    clause = visibility_clause()
    if clause is not None:
        stmt = stmt.where(clause)
    return ((await db.execute(stmt)).scalar() or 0) + 1


async def ranking_page(db: AsyncSession, limit: int, offset: int) -> list[tuple[int, str, int, str]]:
    """(rank, display_name, score, player_id) 목록.

    정렬 키가 score뿐이면 동점자 사이의 순서가 정의되지 않아, 페이징할 때 같은
    플레이어가 두 페이지에 나오거나 아예 빠질 수 있다. updated_at과 player_id를
    타이브레이커로 두어 순서를 결정적으로 고정한다 (먼저 그 점수에 도달한 사람이 위).
    """
    ranked = (
        select(
            DeliveryRecord.player_id.label("player_id"),
            DeliveryRecord.score.label("score"),
            DeliveryRecord.updated_at.label("updated_at"),
            Player.display_name.label("display_name"),
            func.rank().over(order_by=DeliveryRecord.score.desc()).label("rank"),
        )
        .join(Player, DeliveryRecord.player_id == Player.id)
    )
    clause = visibility_clause()
    if clause is not None:
        ranked = ranked.where(clause)
    sub = ranked.subquery()

    rows = (
        await db.execute(
            select(sub.c.rank, sub.c.display_name, sub.c.score, sub.c.player_id)
            .order_by(sub.c.score.desc(), sub.c.updated_at.asc(), sub.c.player_id.asc())
            .offset(offset)
            .limit(limit)
        )
    ).all()
    return [(int(r[0]), r[1], int(r[2]), r[3]) for r in rows]
