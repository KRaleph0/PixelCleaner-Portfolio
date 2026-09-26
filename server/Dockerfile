FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

EXPOSE 8000

# 워커 1개. SQLite는 파일 단위로 쓰기를 직렬화하므로 워커를 여러 개 띄우면
# 동시 제출이 몰릴 때 'database is locked'로 500이 난다. FastAPI는 비동기라
# 이 규모 트래픽에는 워커 1개로 충분하다. PostgreSQL로 옮기면 그때 늘린다.
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
