import logging

from sqlalchemy import event, text
from sqlalchemy.ext.asyncio import AsyncSession, async_sessionmaker, create_async_engine
from sqlalchemy.orm import DeclarativeBase

from config import DATABASE_URL

log = logging.getLogger("pixelcleaners.db")

IS_SQLITE = DATABASE_URL.startswith("sqlite")

engine = create_async_engine(DATABASE_URL, echo=False)
SessionLocal = async_sessionmaker(engine, expire_on_commit=False)


if IS_SQLITE:
    @event.listens_for(engine.sync_engine, "connect")
    def _sqlite_pragmas(dbapi_connection, _record):
        """SQLite 기본값은 동시 쓰기에 취약하고 외래 키도 강제하지 않는다.

        - WAL: 읽기와 쓰기가 서로를 막지 않게 한다 (랭킹 조회 중 제출이 대기하지 않음)
        - busy_timeout: 쓰기 경합 시 즉시 'database is locked'로 죽지 않고 5초 재시도
        - foreign_keys: 부모만 지웠을 때 고아 행이 조용히 남는 것을 막는다
        """
        cursor = dbapi_connection.cursor()
        cursor.execute("PRAGMA journal_mode=WAL")
        cursor.execute("PRAGMA busy_timeout=5000")
        cursor.execute("PRAGMA foreign_keys=ON")
        cursor.close()


class Base(DeclarativeBase):
    pass


async def get_db():
    async with SessionLocal() as session:
        yield session


async def _existing_columns(conn, table: str) -> set[str]:
    if IS_SQLITE:
        rows = await conn.execute(text(f"PRAGMA table_info({table})"))
        return {r[1] for r in rows}
    rows = await conn.execute(
        text("SELECT column_name FROM information_schema.columns WHERE table_name = :t"),
        {"t": table},
    )
    return {r[0] for r in rows}


async def run_migrations(conn) -> None:
    """create_all()은 없는 테이블만 만들고 기존 테이블에 컬럼은 추가하지 않는다.

    Alembic을 들이기엔 이른 규모라, 필요한 ALTER만 여기서 멱등하게 처리한다.
    새 컬럼을 추가할 때는 이 함수에 한 줄씩 덧붙이면 된다.
    """
    columns = await _existing_columns(conn, "players")
    if columns and "auth_token" not in columns:
        log.info("migration: players.auth_token 컬럼 추가")
        await conn.execute(text("ALTER TABLE players ADD COLUMN auth_token VARCHAR"))

    await conn.execute(
        text("CREATE UNIQUE INDEX IF NOT EXISTS ix_players_auth_token ON players (auth_token)")
    )
    # 랭킹 정렬용. 없으면 조회마다 풀스캔.
    await conn.execute(
        text("CREATE INDEX IF NOT EXISTS ix_delivery_records_score ON delivery_records (score)")
    )


async def backfill_auth_tokens(conn) -> int:
    """인증 도입 이전에 등록된 플레이어에게 토큰을 채워 넣는다."""
    import secrets

    rows = await conn.execute(
        text("SELECT id FROM players WHERE auth_token IS NULL OR auth_token = ''")
    )
    ids = [r[0] for r in rows]
    for player_id in ids:
        await conn.execute(
            text("UPDATE players SET auth_token = :t WHERE id = :i"),
            {"t": secrets.token_urlsafe(32), "i": player_id},
        )
    if ids:
        log.info("migration: 기존 플레이어 %d명에게 auth_token 발급", len(ids))
    return len(ids)
