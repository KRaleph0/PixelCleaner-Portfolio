"""서버 설정 — 전부 환경변수로 주입 가능하게 모아 둠 (하드코딩 상수 제거)."""
import os

from dotenv import load_dotenv

load_dotenv()

DATABASE_URL = os.getenv("DATABASE_URL", "sqlite+aiosqlite:///./pixelcleaners.db")

# 납품 1개당 점수. 클라이언트 PointsPerDelivery와 반드시 동일해야 함.
# 0 이하로 두면 "100의 배수" 검증을 끄는 의미가 된다.
POINTS_PER_ITEM = int(os.getenv("POINTS_PER_ITEM", "100"))

# 점수 제출 인증.
#   false — 토큰을 발급/검증하되 불일치해도 통과시키고 경고만 남긴다 (구버전 클라 보호)
#   true  — 401/403으로 거절한다. 신버전 클라 배포 확인 후 켠다.
AUTH_ENFORCE = os.getenv("AUTH_ENFORCE", "false").lower() in ("1", "true", "yes")

# 닉네임 길이 제한 (클라 입력 제한과 동일하게 12자).
DISPLAY_NAME_MAX = int(os.getenv("DISPLAY_NAME_MAX", "12"))
DEVICE_ID_MAX = int(os.getenv("DEVICE_ID_MAX", "128"))

# 랭킹에서 감출 device_id 접두사 (쉼표 구분). 리허설 계정을 랭킹에 안 올리는 용도.
#   예: RANKING_EXCLUDE_PREFIXES=demo-,claudecode-verify-,cc-edge-
RANKING_EXCLUDE_PREFIXES = [
    p.strip() for p in os.getenv("RANKING_EXCLUDE_PREFIXES", "").split(",") if p.strip()
]

# 치팅 의심 임계값 — 시간당 증가분이 이 값을 넘으면 경고 로그를 남긴다 (거절하지는 않음).
# 정상 플레이 상한은 납품 1개=100pt, 제작 사이클 30~60초 기준 시간당 약 12,000pt.
SCORE_RATE_LIMIT_PER_HOUR = int(os.getenv("SCORE_RATE_LIMIT_PER_HOUR", "30000"))

# 이벤트 시각 표기에 쓰는 타임존.
DISPLAY_TIMEZONE = os.getenv("DISPLAY_TIMEZONE", "Asia/Seoul")
