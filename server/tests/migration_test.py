"""구 스키마(실서버와 동일) DB에 신 코드를 올렸을 때를 재현한다."""
import asyncio, os, sqlite3, sys, tempfile, uuid
import pathlib

tmp = tempfile.mkdtemp(); dbf = f"{tmp}/legacy.db"

# ── 현재 운영 중인 스키마 그대로 만든다 (auth_token 없음, 신규 테이블 없음)
con = sqlite3.connect(dbf)
con.executescript("""
CREATE TABLE players (
  id VARCHAR NOT NULL PRIMARY KEY, device_id VARCHAR, display_name VARCHAR,
  created_at DATETIME DEFAULT CURRENT_TIMESTAMP);
CREATE UNIQUE INDEX ix_players_device_id ON players (device_id);
CREATE TABLE delivery_records (
  id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, player_id VARCHAR,
  score INTEGER, updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY(player_id) REFERENCES players (id));
CREATE UNIQUE INDEX ix_dr_player ON delivery_records (player_id);
""")
ids = {}
for name, dev, score in [("ZZTEST_삭제요망_2","claudecode-verify-0909-b",900),
                         ("ZZTEST_삭제요망_1","claudecode-verify-0909",500),
                         ("실유저","real-device-1",1200),
                         ("ZZTEST_삭제요망_3","cc-edge-empty",None),
                         ("ZZTEST_삭제요망_4","cc-edge-long",None)]:
    pid = str(uuid.uuid4()); ids[name] = pid
    con.execute("INSERT INTO players (id,device_id,display_name) VALUES (?,?,?)", (pid,dev,name))
    if score is not None:
        con.execute("INSERT INTO delivery_records (player_id,score) VALUES (?,?)", (pid,score))
# 고아 행 하나 심어 둔다 (예전 수동 삭제 잔재 재현)
con.execute("INSERT INTO delivery_records (player_id,score) VALUES ('ghost-player',777)")
con.commit(); con.close()

os.environ["DATABASE_URL"] = f"sqlite+aiosqlite:///{dbf}"
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
    print("\n[구 스키마 DB 기동 — 마이그레이션]")
    async with main.lifespan(main.app):
        con = sqlite3.connect(dbf)
        cols = {r[1] for r in con.execute("PRAGMA table_info(players)")}
        check("players.auth_token 컬럼이 추가됨", "auth_token" in cols, cols)
        nulls = con.execute("SELECT COUNT(*) FROM players WHERE auth_token IS NULL").fetchone()[0]
        check("기존 플레이어 전원에게 토큰 백필", nulls == 0)
        tables = {r[0] for r in con.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        check("신규 테이블 생성", {"delivery_submissions","events"} <= tables, tables)
        idx = {r[0] for r in con.execute("SELECT name FROM sqlite_master WHERE type='index'")}
        check("score 인덱스 생성", "ix_delivery_records_score" in idx, idx)
        jm = con.execute("PRAGMA journal_mode").fetchone()[0]
        check("WAL 모드 적용", jm.lower() == "wal", jm)
        con.close()

        tr = ASGITransport(app=main.app)
        async with AsyncClient(transport=tr, base_url="http://t") as c:
            print("\n[기존 데이터 보존]")
            r = (await c.get("/ranking", params={"limit":10})).json()
            names = [e["display_name"] for e in r["entries"]]
            check("기존 점수 그대로 노출", names[:3]==["실유저","ZZTEST_삭제요망_2","ZZTEST_삭제요망_1"], names)
            check("고아 행은 랭킹에서 제외 + total 일치",
                  r["total"]==3 and len(r["entries"])==3, (r["total"], len(r["entries"])))

            print("\n[구버전 클라이언트 호환 — 토큰 없이 그대로 호출]")
            reg = (await c.post("/players/register",
                   json={"device_id":"real-device-1","display_name":"실유저"})).json()
            check("기존 기기 재등록 → 같은 player_id", reg["player_id"]==ids["실유저"], reg)
            check("기존 계정도 토큰을 돌려받음", bool(reg["token"]))
            r = await c.post("/delivery/submit",
                             json={"player_id":ids["실유저"],"score":1300})   # 헤더 없음
            check("헤더 없는 제출도 200 (경고 모드)", r.status_code==200 and r.json()["accepted_score"]==1300, r.text)
            old = (await c.get("/ranking", params={"limit":10})).json()
            check("구버전이 읽던 필드 그대로 존재",
                  {"rank","display_name","score"} <= set(old["entries"][0]), old["entries"][0])

    print("\n[purge 스크립트]")
    import subprocess
    env = dict(os.environ)
    dry = subprocess.run([sys.executable, "scripts/purge_test_data.py"],
                         cwd=str(pathlib.Path(__file__).resolve().parent.parent), env=env, capture_output=True, text=True)
    check("dry-run이 대상 4건을 찾는다", "대상 계정 4건" in dry.stdout, dry.stdout+dry.stderr)
    check("dry-run은 지우지 않는다", "dry-run" in dry.stdout)
    app = subprocess.run([sys.executable, "scripts/purge_test_data.py", "--apply"],
                         cwd=str(pathlib.Path(__file__).resolve().parent.parent), env=env, capture_output=True, text=True)
    check("--apply 삭제 성공", "삭제 완료: 계정 4건" in app.stdout, app.stdout+app.stderr)

    con = sqlite3.connect(dbf)
    left = [r[0] for r in con.execute("SELECT display_name FROM players")]
    check("실유저만 남음", left==["실유저"], left)
    orph = con.execute(
        "SELECT COUNT(*) FROM delivery_records WHERE player_id NOT IN (SELECT id FROM players)"
    ).fetchone()[0]
    check("고아 행까지 정리됨", orph==0, orph)
    con.close()

    print("\n[manage_events 스크립트]")
    add = subprocess.run([sys.executable, "scripts/manage_events.py", "add", "autumn2026",
                          "가을 대청소 주간", "--starts","2026-09-15 00:00","--ends","2026-09-22 23:59",
                          "--multiplier","2.0"], cwd=str(pathlib.Path(__file__).resolve().parent.parent),
                         env=env, capture_output=True, text=True)
    check("이벤트 등록", "등록 완료" in add.stdout, add.stdout+add.stderr)
    lst = subprocess.run([sys.executable, "scripts/manage_events.py", "list"],
                         cwd=str(pathlib.Path(__file__).resolve().parent.parent), env=env, capture_output=True, text=True)
    check("목록 출력 (KST 표기)", "autumn2026" in lst.stdout and "2026-09-15 00:00" in lst.stdout, lst.stdout)
    off = subprocess.run([sys.executable, "scripts/manage_events.py", "deactivate", "autumn2026"],
                         cwd=str(pathlib.Path(__file__).resolve().parent.parent), env=env, capture_output=True, text=True)
    check("이벤트 종료 처리", "종료 처리" in off.stdout, off.stdout+off.stderr)

    print(f"\n결과: {ok} PASS / {fail} FAIL")
    return 1 if fail else 0

sys.exit(asyncio.run(run()))
