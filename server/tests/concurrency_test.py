"""동시 제출 / 리허설 계정 숨김 / 이벤트 delta 경계."""
import asyncio, os, sys, tempfile
import pathlib
tmp = tempfile.mkdtemp()
os.environ["DATABASE_URL"] = f"sqlite+aiosqlite:///{tmp}/x.db"
os.environ["RANKING_EXCLUDE_PREFIXES"] = "demo-"
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
from httpx import ASGITransport, AsyncClient
import main

ok = fail = 0
def check(l, c, e=""):
    global ok, fail
    if c: ok += 1; print(f"  PASS  {l}")
    else: fail += 1; print(f"  FAIL  {l}  {e}")

async def run():
    async with main.lifespan(main.app):
        tr = ASGITransport(app=main.app)
        async with AsyncClient(transport=tr, base_url="http://t") as c:
            print("\n[S1 동시 제출 — database is locked 재현 시도]")
            players = []
            for i in range(20):
                p = (await c.post("/players/register",
                     json={"device_id":f"load-{i}","display_name":f"P{i}"})).json()
                players.append(p)
            async def hammer(p, n):
                out = []
                for k in range(1, n+1):
                    r = await c.post("/delivery/submit",
                        json={"player_id":p["player_id"],"score":k*100},
                        headers={"Authorization": f"Bearer {p['token']}"})
                    out.append(r.status_code)
                return out
            results = await asyncio.gather(*[hammer(p, 10) for p in players])
            codes = [s for r in results for s in r]
            check(f"동시 제출 200건 전부 200 (실패 {sum(1 for s in codes if s!=200)}건)",
                  all(s == 200 for s in codes), set(codes))
            reads = await asyncio.gather(*[c.get("/ranking", params={"limit":10}) for _ in range(30)])
            check("쓰기와 동시에 읽기 30건 전부 200", all(r.status_code==200 for r in reads))

            print("\n[리허설 계정 숨김 — RANKING_EXCLUDE_PREFIXES=demo-]")
            d = (await c.post("/players/register",
                 json={"device_id":"demo-rehearsal","display_name":"리허설"})).json()
            await c.post("/delivery/submit", json={"player_id":d["player_id"],"score":999900},
                         headers={"Authorization": f"Bearer {d['token']}"})
            r = (await c.get("/ranking", params={"limit":100})).json()
            check("demo- 계정은 랭킹에 안 나옴",
                  all(e["display_name"] != "리허설" for e in r["entries"]))
            check("total에도 안 잡힘", r["total"] == len(r["entries"]) == 20, (r["total"], len(r["entries"])))
            top = (await c.get("/ranking/me", params={"player_id":players[0]["player_id"]})).json()
            check("숨긴 계정이 남의 순위를 밀어내지 않음", top["rank"] == 1, top)

            print("\n[이벤트 delta 경계]")
            from datetime import timedelta
            from models import Event
            from database import SessionLocal
            from timeutil import utcnow
            # SQLite CURRENT_TIMESTAMP는 초 단위라, 기간 경계를 확실히 넘기려고 대기한다.
            ne = (await c.post("/players/register",
                  json={"device_id":"ev-user","display_name":"EV"})).json()
            H = {"Authorization": f"Bearer {ne['token']}"}
            await c.post("/delivery/submit", json={"player_id":ne["player_id"],"score":1000}, headers=H)
            await asyncio.sleep(1.2)
            async with SessionLocal() as db:
                db.add(Event(event_id="ev", title="t", starts_at=utcnow(),
                             ends_at=utcnow()+timedelta(days=1), score_multiplier=2.0))
                await db.commit()
            await asyncio.sleep(1.2)
            await c.post("/delivery/submit", json={"player_id":ne["player_id"],"score":1500}, headers=H)
            ev = (await c.get("/events/ev/ranking", params={"limit":10,"me":ne["player_id"]})).json()
            mine = ev["me"]
            check("이벤트 전 누적분은 제외, 기간 내 증가분 500 x2 = 1000", mine and mine["score"]==1000, mine)
            g = (await c.get("/ranking/me", params={"player_id":ne["player_id"]})).json()
            check("상시 점수는 원본 1500 그대로 (배율 미적용)", g["score"]==1500, g)
            await c.post("/delivery/submit", json={"player_id":ne["player_id"],"score":100}, headers=H)
            ev2 = (await c.get("/events/ev/ranking", params={"limit":10,"me":ne["player_id"]})).json()
            check("최고점 미달 재제출은 이벤트 점수를 올리지 않음", ev2["me"]["score"]==1000, ev2["me"])
            check("이벤트 전부터 있던 플레이어가 목록에 잘못 끼지 않음",
                  all(e["display_name"] in ("EV",) for e in ev2["entries"]), ev2["entries"])
    print(f"\n결과: {ok} PASS / {fail} FAIL")
    return 1 if fail else 0
sys.exit(asyncio.run(run()))
