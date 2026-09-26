from pydantic import BaseModel

# ── 플레이어 ─────────────────────────────────────────────
class PlayerRegisterRequest(BaseModel):
    # 길이 검증은 스키마가 아니라 라우터에서 한다.
    # Pydantic 제약을 걸면 FastAPI가 422를 내보내는데, 클라이언트가 등록 실패를
    # 400으로만 처리하고 있어 호환을 위해 400으로 통일한다.
    device_id: str
    display_name: str


class PlayerResponse(BaseModel):
    player_id: str
    display_name: str
    token: str = ""          # 신규 필드. 클라는 이 값을 저장해 제출 시 헤더로 보낸다.


# ── 납품 ─────────────────────────────────────────────────
class DeliverySubmitRequest(BaseModel):
    player_id: str
    score: int               # 클라이언트 누적 총점 (증분 아님)


class DeliverySubmitResponse(BaseModel):
    accepted_score: int
    rank: int


# ── 랭킹 ─────────────────────────────────────────────────
class RankEntry(BaseModel):
    rank: int
    display_name: str
    score: int
    is_me: bool = False      # 신규 필드. ?me=<player_id>를 준 경우에만 true가 될 수 있다.


class RankingResponse(BaseModel):
    entries: list[RankEntry]
    total: int
    me: RankEntry | None = None   # 신규 필드. ?me= 를 준 경우 내 순위를 함께 돌려준다.


# ── 이벤트 ───────────────────────────────────────────────
class EventInfo(BaseModel):
    event_id: str
    title: str
    description: str
    starts_at: str           # ISO8601 + 오프셋
    ends_at: str
    score_multiplier: float
    banner_color: str
    server_time: str         # 기기 시계를 믿을 수 없으므로 카운트다운은 이 값 기준으로 계산할 것


class EventListResponse(BaseModel):
    events: list[EventInfo]
