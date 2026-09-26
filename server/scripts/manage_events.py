#!/usr/bin/env python3
"""이벤트 등록/조회/종료.

이벤트가 한두 개인 단계에서 관리 API를 인터넷에 노출할 이유가 없어 스크립트로 둔다.
시각은 표시 타임존(기본 Asia/Seoul) 기준으로 입력받아 UTC로 저장한다.

사용법:
    python scripts/manage_events.py list
    python scripts/manage_events.py add autumn2026 "가을 대청소 주간" \
        --starts "2026-09-15 00:00" --ends "2026-09-22 23:59" \
        --multiplier 2.0 --description "기간 내 납품 점수가 2배로 집계됩니다"
    python scripts/manage_events.py deactivate autumn2026
"""
import argparse
import asyncio
import sys
from datetime import datetime, timezone
from pathlib import Path
from zoneinfo import ZoneInfo

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from sqlalchemy import select          # noqa: E402

from config import DISPLAY_TIMEZONE    # noqa: E402
from database import SessionLocal      # noqa: E402
from models import Event               # noqa: E402

TZ = ZoneInfo(DISPLAY_TIMEZONE)


def parse_local(s: str) -> datetime:
    dt = datetime.strptime(s, "%Y-%m-%d %H:%M").replace(tzinfo=TZ)
    return dt.astimezone(timezone.utc).replace(tzinfo=None)


def fmt(dt: datetime) -> str:
    return dt.replace(tzinfo=timezone.utc).astimezone(TZ).strftime("%Y-%m-%d %H:%M %Z")


async def main() -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)

    sub.add_parser("list")

    add = sub.add_parser("add")
    add.add_argument("event_id")
    add.add_argument("title")
    add.add_argument("--starts", required=True, help="YYYY-MM-DD HH:MM (현지 시각)")
    add.add_argument("--ends", required=True, help="YYYY-MM-DD HH:MM (현지 시각)")
    add.add_argument("--multiplier", type=float, default=1.0)
    add.add_argument("--description", default="")
    add.add_argument("--color", default="#3aa76d")

    off = sub.add_parser("deactivate")
    off.add_argument("event_id")

    args = ap.parse_args()

    async with SessionLocal() as db:
        if args.cmd == "list":
            rows = (await db.execute(select(Event).order_by(Event.starts_at))).scalars().all()
            if not rows:
                print("등록된 이벤트가 없습니다.")
            for ev in rows:
                state = "활성" if ev.is_active else "종료"
                print(f"[{state}] {ev.event_id}  {ev.title}")
                print(f"        {fmt(ev.starts_at)} ~ {fmt(ev.ends_at)}  x{ev.score_multiplier}")
            return 0

        if args.cmd == "add":
            starts, ends = parse_local(args.starts), parse_local(args.ends)
            if ends <= starts:
                print("종료 시각이 시작 시각보다 빠릅니다.")
                return 1
            if await db.get(Event, args.event_id):
                print(f"이미 존재하는 event_id: {args.event_id}")
                return 1
            db.add(Event(
                event_id=args.event_id, title=args.title, description=args.description,
                starts_at=starts, ends_at=ends, score_multiplier=args.multiplier,
                banner_color=args.color, is_active=1,
            ))
            await db.commit()
            print(f"등록 완료: {args.event_id}  {fmt(starts)} ~ {fmt(ends)}  x{args.multiplier}")
            return 0

        ev = await db.get(Event, args.event_id)
        if not ev:
            print(f"없는 event_id: {args.event_id}")
            return 1
        ev.is_active = 0
        await db.commit()
        print(f"종료 처리: {args.event_id}")
        return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
