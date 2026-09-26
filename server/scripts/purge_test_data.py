#!/usr/bin/env python3
"""테스트/리허설 계정 정리.

랭킹 전체를 지울 수 있는 엔드포인트는 공개 API로 두지 않는다 — 인증이 뚫리면
한 번에 전 기록이 날아간다. 대신 서버에서 직접 실행하는 스크립트로 처리한다.

사용법:
    python scripts/purge_test_data.py                       # 대상만 출력 (기본: dry-run)
    python scripts/purge_test_data.py --apply               # 실제 삭제
    python scripts/purge_test_data.py --prefix demo- --apply

부모(players)만 지우면 delivery_records에 고아 행이 남는다. SQLite는 기본적으로
외래 키를 강제하지 않아 에러 없이 조용히 남고, 그러면 랭킹의 total과 entries 개수가
어긋난다. 반드시 자식부터 지운다.
"""
import argparse
import asyncio
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from sqlalchemy import delete, or_, select                     # noqa: E402

from database import SessionLocal                              # noqa: E402
from models import DeliveryRecord, DeliverySubmission, Player  # noqa: E402

DEFAULT_PREFIXES = ["claudecode-verify-", "cc-edge-", "demo-"]


async def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--prefix", action="append", dest="prefixes",
                    help="대상 device_id 접두사 (여러 번 지정 가능)")
    ap.add_argument("--apply", action="store_true", help="실제로 삭제한다")
    args = ap.parse_args()

    prefixes = args.prefixes or DEFAULT_PREFIXES

    async with SessionLocal() as db:
        cond = or_(*[Player.device_id.like(f"{p}%") for p in prefixes])
        targets = (await db.execute(select(Player).where(cond))).scalars().all()

        print(f"접두사: {', '.join(prefixes)}")
        print(f"대상 계정 {len(targets)}건")
        for p in targets:
            rec = (
                await db.execute(
                    select(DeliveryRecord.score).where(DeliveryRecord.player_id == p.id)
                )
            ).scalar_one_or_none()
            print(f"  - {p.display_name!r} device_id={p.device_id} score={rec}")

        orphans = (
            await db.execute(
                select(DeliveryRecord.id).where(
                    DeliveryRecord.player_id.notin_(select(Player.id))
                )
            )
        ).scalars().all()
        if orphans:
            print(f"고아 delivery_records {len(orphans)}건도 함께 정리 대상")

        if not args.apply:
            print("\ndry-run 입니다. 실제로 지우려면 --apply 를 붙이세요.")
            print("실행 전 DB 백업을 반드시 먼저 뜨십시오.")
            return 0

        ids = [p.id for p in targets]
        if ids:
            await db.execute(
                delete(DeliverySubmission).where(DeliverySubmission.player_id.in_(ids))
            )
            await db.execute(delete(DeliveryRecord).where(DeliveryRecord.player_id.in_(ids)))
            await db.execute(delete(Player).where(Player.id.in_(ids)))
        await db.execute(
            delete(DeliveryRecord).where(DeliveryRecord.player_id.notin_(select(Player.id)))
        )
        await db.commit()
        print(f"\n삭제 완료: 계정 {len(ids)}건")
        return 0


if __name__ == "__main__":
    raise SystemExit(asyncio.run(main()))
