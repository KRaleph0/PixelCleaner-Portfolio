"""서버 전역 시각 기준.

DB에는 naive UTC로 저장한다 (SQLite의 CURRENT_TIMESTAMP도 UTC라 서버 default와 일치).
datetime.utcnow()는 3.12에서 deprecated라 감싸 둔다.
"""
from datetime import datetime, timezone


def utcnow() -> datetime:
    return datetime.now(timezone.utc).replace(tzinfo=None)
