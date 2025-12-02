# queue-server

## 프로젝트 개요

게임 대기열 서버 시스템으로, 다음 구성요소로 이루어져 있습니다:
- **queue-api**: 대기열 API 서버 (Spring Boot)
- **queue-manager**: 대기열 관리 서버 (Spring Boot)
- **chat-server**: 채팅 서버 (.NET)
- **chat-client**: 채팅 클라이언트 (Node.js)

자세한 기술 명세는 [spec.md](./spec.md)를 참조하세요.

## 로컬 개발 환경

### Docker Compose로 실행

```bash
docker-compose up --build
```

### 서비스 엔드포인트

| 서비스 | URL | 설명 |
|--------|-----|------|
| Queue API | http://localhost:8080 | 대기열 API |
| Chat Server | http://localhost:8081 | 채팅 서버 |
| Valkey Dashboard | http://localhost:8082 | Redis 대시보드 |
| Grafana | http://localhost:3000 | 모니터링 대시보드 (로그인 불필요) |
| Prometheus | http://localhost:9090 | 메트릭 저장소 |
| Loki | http://localhost:3100 | 로그 저장소 |
| Alloy | http://localhost:12345 | OpenTelemetry Collector UI |

## 빌드

```bash
# Queue 서비스 빌드
cd queue
./gradlew build

# Chat Server 빌드
cd chat-server
dotnet build
```

## 라이센스

MIT License