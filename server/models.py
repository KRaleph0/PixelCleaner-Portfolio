import secrets
import uuid
from datetime import datetime

from sqlalchemy import DateTime, Float, ForeignKey, Integer, String, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from database import Base


def _new_token() -> str:
    return secrets.token_urlsafe(32)


class Player(Base):
    __tablename__ = "players"

    id:           Mapped[str] = mapped_column(String, primary_key=True, default=lambda: str(uuid.uuid4()))
    device_id:    Mapped[str] = mapped_column(String, unique=True, index=True)
    display_name: Mapped[str] = mapped_column(String)
    # 점수 제출 인증용. JWT 대신 DB 저장 토큰 — 치팅 계정 하나만 즉시 무효화할 수 있다.
    auth_token:   Mapped[str] = mapped_column(String, unique=True, index=True, default=_new_token)
    created_at:   Mapped[datetime] = mapped_column(DateTime, server_default=func.now())

    record: Mapped["DeliveryRecord"] = relationship(back_populates="player", uselist=False)


class DeliveryRecord(Base):
    """플레이어당 1행. 제출된 누적 총점의 최댓값만 보관한다 (기존 동작 유지)."""

    __tablename__ = "delivery_records"

    id:         Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    player_id:  Mapped[str] = mapped_column(String, ForeignKey("players.id"), unique=True)
    score:      Mapped[int] = mapped_column(Integer, default=0, index=True)
    updated_at: Mapped[datetime] = mapped_column(DateTime, server_default=func.now(), onupdate=func.now())

    player: Mapped["Player"] = relationship(back_populates="record")


class DeliverySubmission(Base):
    """제출 이력 (append-only).

    DeliveryRecord는 최고점만 덮어써서 이력이 남지 않는다. 이 테이블이 있어야
      - 기간 한정 이벤트 랭킹의 '기간 내 획득 점수'를 계산할 수 있고
      - 치팅이 의심될 때 누가 언제 무엇을 냈는지 사후 추적이 가능하다.
    """

    __tablename__ = "delivery_submissions"

    id:           Mapped[int] = mapped_column(Integer, primary_key=True, autoincrement=True)
    player_id:    Mapped[str] = mapped_column(String, ForeignKey("players.id"), index=True)
    total_score:  Mapped[int] = mapped_column(Integer)   # 클라가 보낸 누적 총점 그대로
    delta:        Mapped[int] = mapped_column(Integer)   # 직전 최고점 대비 증가분 (서버 계산)
    submitted_at: Mapped[datetime] = mapped_column(DateTime, server_default=func.now(), index=True)


class Event(Base):
    """이벤트 정의. 등록/수정은 공개 API가 아니라 scripts/manage_events.py로 한다."""

    __tablename__ = "events"

    event_id:         Mapped[str] = mapped_column(String, primary_key=True)
    title:            Mapped[str] = mapped_column(String)
    description:      Mapped[str] = mapped_column(String, default="")
    # 저장은 naive UTC. 응답에서 표시 타임존으로 변환한다.
    starts_at:        Mapped[datetime] = mapped_column(DateTime, index=True)
    ends_at:          Mapped[datetime] = mapped_column(DateTime, index=True)
    score_multiplier: Mapped[float] = mapped_column(Float, default=1.0)
    banner_color:     Mapped[str] = mapped_column(String, default="#3aa76d")
    is_active:        Mapped[int] = mapped_column(Integer, default=1)
