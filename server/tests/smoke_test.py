"""서버 변경분 스모크 테스트. 임시 DB에 대해 전 시나리오를 돌린다."""
import asyncio, os, sys, tempfile, pathlib
import pathlib

tmpdir = tempfile.mkdtemp()
os.environ["DATABASE_URL"] = f"sqlite+aiosqlite:///{tmpdir}/t.db"
os.environ["AUTH_ENFORCE"] = "false"
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from httpx import ASGITransport, AsyncClient   # noqa: E402
import main                                    # noqa: E402

ok = fail = 0
def check(label, cond, extra=""):
    global ok, fail
    if cond: ok += 1; print(f"  PASS  {label}")
    else:    fail += 1; print(f"  FAIL  {label}  {extra}")

async def run():
    async with main.lifespan(main.app):
        tr = ASGITransport(app=main.app)
        async with AsyncClient(transport=tr, base_url="http://t") as c:
            print("\n[기존 동작 보존]")
            r = await c.post("/players/register", json={"device_id":"dev-a","display_name":"A"})
            a = r.json(); check("register 200 + player_id", r.status_code==200 and a["player_id"], r.text)
            check("register가 토큰을 발급한다", bool(a.get("token")))
            r2 = await c.post("/players/register", json={"device_id":"dev-a","display_name":"A2"})
            check("device_id upsert — 같은 player_id", r2.json()["player_id"]==a["player_id"])
            check("재등록해도 토큰은 유지", r2.json()["token"]==a["token"])

            H = {"Authorization": f"Bearer {a['token']}"}
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":300}, headers=H)
            check("제출 300 → accepted 300", r.json()["accepted_score"]==300, r.text)
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":100}, headers=H)
            check("최댓값 유지 (100 제출해도 300)", r.json()["accepted_score"]==300)
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":500}, headers=H)
            check("제출 500 → accepted 500", r.json()["accepted_score"]==500)

            r = await c.post("/delivery/submit", json={"player_id":"nonexistent","score":100})
            check("없는 player_id → 404 (클라 자동 재등록 트리거)",
                  r.status_code==404 and r.json()["detail"]=="Player not found", r.text)
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":-999}, headers=H)
            check("음수 점수 → 400", r.status_code==400)
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":150}, headers=H)
            check("100의 배수 아님 → 400 (기존 규칙 유지)", r.status_code==400)
            r = await c.get("/ranking/me", params={"player_id":"nonexistent"})
            check("없는 id로 /ranking/me → 200 rank:0", r.status_code==200 and r.json()["rank"]==0)
            r = await c.post("/players/register", json={"device_id":"dev-kr","display_name":"한글테스트"})
            check("한글 닉네임 왕복", r.json()["display_name"]=="한글테스트")

            print("\n[§7 빈 닉네임 / 길이 검증]")
            r = await c.post("/players/register", json={"device_id":"x1","display_name":""})
            check("빈 닉네임 → 400 (기존엔 200 통과)", r.status_code==400, r.text)
            r = await c.post("/players/register", json={"device_id":"x2","display_name":"   "})
            check("공백만 있는 닉네임 → 400", r.status_code==400)
            r = await c.post("/players/register", json={"device_id":"x3","display_name":"가"*200})
            check("200자 닉네임 → 400 (422 아님)", r.status_code==400, r.text)
            r = await c.post("/players/register", json={"device_id":"x4","display_name":"열두자까지는통과함"})
            check("12자 이내는 통과", r.status_code==200)

            print("\n[§2 동점자 rank 일치 + is_me]")
            b = (await c.post("/players/register", json={"device_id":"dev-b","display_name":"B"})).json()
            HB = {"Authorization": f"Bearer {b['token']}"}
            await c.post("/delivery/submit", json={"player_id":b["player_id"],"score":500}, headers=HB)
            lst = (await c.get("/ranking", params={"limit":10,"offset":0})).json()
            ranks = {e["display_name"]: e["rank"] for e in lst["entries"]}
            check("동점자 A·B가 목록에서 같은 순위", ranks.get("A2")==ranks.get("B")==1, ranks)
            mea = (await c.get("/ranking/me", params={"player_id":a["player_id"]})).json()
            meb = (await c.get("/ranking/me", params={"player_id":b["player_id"]})).json()
            check("목록 rank와 /ranking/me rank 일치",
                  mea["rank"]==ranks.get("A2") and meb["rank"]==ranks.get("B"), (mea,meb,ranks))
            sub = (await c.post("/delivery/submit", json={"player_id":b["player_id"],"score":500}, headers=HB)).json()
            check("submit 응답 rank도 목록과 일치", sub["rank"]==ranks.get("B"), sub)

            lst = (await c.get("/ranking", params={"limit":10,"me":b["player_id"]})).json()
            mine = [e for e in lst["entries"] if e["is_me"]]
            check("is_me가 정확히 내 줄 하나에만 붙는다",
                  len(mine)==1 and mine[0]["display_name"]=="B", lst["entries"])
            check("me 블록이 함께 온다", lst["me"] and lst["me"]["display_name"]=="B", lst.get("me"))
            check("player_id는 응답에 노출되지 않는다", "player_id" not in lst["entries"][0])
            lst0 = (await c.get("/ranking", params={"limit":10})).json()
            check("me 없이 호출하면 is_me 전부 false (하위 호환)",
                  all(not e["is_me"] for e in lst0["entries"]) and lst0["me"] is None)

            print("\n[동명이인·동점 구분]")
            d1 = (await c.post("/players/register", json={"device_id":"dup-1","display_name":"홍길동"})).json()
            d2 = (await c.post("/players/register", json={"device_id":"dup-2","display_name":"홍길동"})).json()
            for p in (d1,d2):
                await c.post("/delivery/submit", json={"player_id":p["player_id"],"score":900},
                             headers={"Authorization": f"Bearer {p['token']}"})
            l1 = (await c.get("/ranking", params={"limit":10,"me":d1["player_id"]})).json()
            l2 = (await c.get("/ranking", params={"limit":10,"me":d2["player_id"]})).json()
            i1 = [i for i,e in enumerate(l1["entries"]) if e["is_me"]]
            i2 = [i for i,e in enumerate(l2["entries"]) if e["is_me"]]
            check("동명이인+동점도 서로 다른 줄로 구분", i1!=i2 and len(i1)==len(i2)==1, (i1,i2))

            print("\n[S8 페이징 안정성]")
            p1 = (await c.get("/ranking", params={"limit":3,"offset":0})).json()["entries"]
            p2 = (await c.get("/ranking", params={"limit":3,"offset":3})).json()["entries"]
            names = [e["display_name"] for e in p1+p2]
            for _ in range(15):
                q1 = (await c.get("/ranking", params={"limit":3,"offset":0})).json()["entries"]
                q2 = (await c.get("/ranking", params={"limit":3,"offset":3})).json()["entries"]
                if [e["display_name"] for e in q1+q2] != names:
                    names = None; break
            check("반복 조회에도 페이지 경계가 흔들리지 않는다", names is not None)
            total = (await c.get("/ranking", params={"limit":100})).json()
            check("total과 entries 개수 일치", total["total"]==len(total["entries"]), total["total"])

            print("\n[§3 인증]")
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":900},
                             headers={"Authorization":"Bearer WRONG"})
            check("경고 모드에서는 잘못된 토큰도 통과 (구버전 클라 보호)", r.status_code==200, r.text)
            import config, auth
            config.AUTH_ENFORCE = auth.AUTH_ENFORCE = True
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":1000},
                             headers={"Authorization":"Bearer WRONG"})
            check("강제 모드에서 잘못된 토큰 → 403", r.status_code==403, r.text)
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":1000})
            check("강제 모드에서 토큰 없음 → 401", r.status_code==401, r.text)
            r = await c.post("/delivery/submit", json={"player_id":"nonexistent","score":100})
            check("강제 모드에서도 없는 id는 401이 아니라 404", r.status_code==404, r.text)
            r = await c.post("/delivery/submit", json={"player_id":a["player_id"],"score":1000}, headers=H)
            check("올바른 토큰은 통과", r.status_code==200 and r.json()["accepted_score"]==1000, r.text)
            config.AUTH_ENFORCE = auth.AUTH_ENFORCE = False

            print("\n[§5 이벤트]")
            r = await c.get("/events/active")
            check("이벤트 없으면 빈 배열", r.json()=={"events":[]}, r.text)
            from datetime import datetime, timedelta
            from models import Event
            from database import SessionLocal
            now = datetime.utcnow()
            async with SessionLocal() as db:
                db.add(Event(event_id="autumn2026", title="가을 대청소 주간",
                             description="2배 집계", starts_at=now-timedelta(hours=1),
                             ends_at=now+timedelta(days=7), score_multiplier=2.0))
                db.add(Event(event_id="past", title="지난 이벤트", starts_at=now-timedelta(days=10),
                             ends_at=now-timedelta(days=3), score_multiplier=3.0))
                await db.commit()
            ev = (await c.get("/events/active")).json()
            check("진행 중인 이벤트만 반환", [e["event_id"] for e in ev["events"]]==["autumn2026"], ev)
            check("server_time 포함", bool(ev["events"][0]["server_time"]))
            check("표시 타임존 오프셋 적용(+09:00)", "+09:00" in ev["events"][0]["starts_at"],
                  ev["events"][0]["starts_at"])

            e = (await c.get("/events/autumn2026/ranking", params={"limit":10,"me":b["player_id"]})).json()
            bb = [x for x in e["entries"] if x["display_name"]=="B"]
            check("이벤트 랭킹에 2배가 적용된다", bb and bb[0]["score"]==1000, e["entries"])
            gl = (await c.get("/ranking", params={"limit":100})).json()
            gb = [x for x in gl["entries"] if x["display_name"]=="B"][0]
            check("상시 랭킹 점수는 배율 영향을 받지 않는다", gb["score"]==500, gb)
            check("이벤트 랭킹에도 is_me/me가 온다",
                  e["me"] and e["me"]["display_name"]=="B" and any(x["is_me"] for x in e["entries"]))
            r = await c.get("/events/nope/ranking")
            check("없는 이벤트 → 404", r.status_code==404)

            print("\n[운영]")
            r = await c.get("/health")
            check("/health 200", r.status_code==200 and r.json()["status"]=="ok", r.text)
            paths = (await c.get("/openapi.json")).json()["paths"]
            check("엔드포인트 목록", set(paths) == {
                "/players/register","/delivery/submit","/ranking","/ranking/me",
                "/events/active","/events/{event_id}/ranking","/health"}, sorted(paths))

    print(f"\n결과: {ok} PASS / {fail} FAIL")
    return 1 if fail else 0

sys.exit(asyncio.run(run()))
