import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from sqlalchemy import text

from config import AUTH_ENFORCE
from database import Base, SessionLocal, backfill_auth_tokens, engine, run_migrations
from routers import delivery, events, players, ranking

logging.basicConfig(
    level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s"
)
log = logging.getLogger("pixelcleaners")


@asynccontextmanager
async def lifespan(app: FastAPI):
    async with engine.begin() as conn:
        await conn.run_sync(Base.metadata.create_all)
        # create_all은 기존 테이블에 컬럼을 추가하지 않는다. 나머지는 여기서.
        await run_migrations(conn)
        await backfill_auth_tokens(conn)
    log.info("점수 제출 인증: %s", "강제(401/403)" if AUTH_ENFORCE else "경고 모드 — 검증 실패도 통과")
    yield


app = FastAPI(title="Pixel Cleaners API", version="1.1.0", lifespan=lifespan)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],   # Unity 네이티브 클라는 CORS 무관. WebGL 빌드를 내면 도메인으로 제한할 것.
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(players.router)
app.include_router(delivery.router)
app.include_router(ranking.router)
app.include_router(events.router)


@app.get("/health", tags=["ops"])
async def health():
    """터널만 살아 있고 앱이 죽은 상태를 구분하기 위한 점검용. DB까지 확인한다."""
    async with SessionLocal() as db:
        await db.execute(text("SELECT 1"))
    return {"status": "ok", "auth_enforce": AUTH_ENFORCE}
