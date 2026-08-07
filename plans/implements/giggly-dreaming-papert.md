# Docker hóa bản đơn giản nhất (single-instance, local volume, nginx)

## Context

`docker-compose.yml` hiện tại chỉ chạy hạ tầng phụ trợ cho local dev (Postgres, Mailpit, Prometheus, Grafana) — API và frontend vẫn chạy bằng `dotnet run`/`vite dev`, chưa được containerize (đúng như quyết định ngày 2026-08-03: "chưa containerize API cho production"). Người dùng muốn bắt đầu triển khai production bằng Docker, chọn phương án đơn giản nhất đã đề xuất: **single-instance, lưu file local qua volume, dùng nginx làm reverse proxy**. Redis (cho distributed cache/SignalR backplane) và storage S3-compatible được hoãn lại cho các bước mở rộng sau.

Phạm vi lần này: chỉ thêm file hạ tầng Docker mới, **không sửa code C#/TS** — đã xác nhận qua đọc `Program.cs` rằng không cần thay đổi gì:
- Migration tự động chạy khi `!Environment.IsDevelopment()` (dòng 145-150) — không cần bước migrate riêng.
- `UseHttpsRedirection()` không có HTTPS port cấu hình sẽ tự bỏ qua redirect (chỉ log warning một lần) — chạy HTTP thuần sau nginx vẫn an toàn, không cần đổi code để tắt nó.
- `/hangfire` và `/metrics` đã được thiết kế "mở, chặn ở network layer" (comment sẵn trong code) — nginx đơn giản là không proxy hai path này, giữ nguyên nguyên tắc cũ.

## Kiến trúc

```
                    ┌──────────────┐
   :80  ──────────▶ │  web (nginx) │
                    │  - serves    │
                    │    dist/     │──/api/, /hubs/──▶ ┌──────────┐       ┌──────────┐
                    │  - proxy     │                    │   api    │──────▶│ postgres │
                    └──────────────┘                    │ (.NET)   │       └──────────┘
                                                          │ :8080    │
                                                          └──────────┘
                                                          volumes: uploads/, logs/
```

Một nginx đứng biên vừa serve static frontend vừa reverse-proxy `/api/` và `/hubs/` sang container `api` — tránh phải cấu hình CORS cho production (same-origin) và không cần thêm container reverse-proxy riêng.

## Các file sẽ tạo

### 1. `backend/Dockerfile`
Multi-stage:
- Stage `build` (`mcr.microsoft.com/dotnet/sdk:10.0`): copy `Directory.Build.props` + 4 `.csproj` trong `src/` trước để cache layer restore, sau đó copy toàn bộ `src/`, `dotnet publish src/StarterKit.API/StarterKit.API.csproj -c Release -o /app/publish`. Không copy `tests/` — image chỉ cần build API + project reference (Application/Infrastructure/Domain tự kéo theo).
- Stage cuối (`mcr.microsoft.com/dotnet/aspnet:10.0`, đã chạy non-root theo mặc định từ .NET 8): `WORKDIR /app`, copy publish output, `EXPOSE 8080`, `ENTRYPOINT ["dotnet", "StarterKit.API.dll"]`.
- Build context: `./backend`. Lưu ý: MinVer sẽ không thấy `.git` trong context này nên version sẽ fallback về giá trị mặc định — chấp nhận được cho bản đơn giản nhất (frontend cũng đã có fallback tương tự trong `vite.config.ts`), không mở rộng context ra root chỉ vì việc này.

### 2. `backend/.dockerignore`
Loại `**/bin/`, `**/obj/`, `**/logs/`, `**/uploads/`, `tests/`, và mọi `appsettings*.json` thật (`appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`, `appsettings.Backup.json`) — không được bake secret vào image.

### 3. `frontend/Dockerfile`
Multi-stage:
- Stage `build` (`oven/bun:1`): build context là **repo root** (không phải `frontend/`) vì `orval.config.ts` đọc `../shared/openapi/openapi.json` — cần copy cả `frontend/` lẫn `shared/` giữ nguyên cấu trúc tương đối. `WORKDIR /build/frontend`, `bun install` (chạy `postinstall: orval` — không cần network, chỉ đọc file local đã commit sẵn), build arg `VITE_API_BASE_URL` (mặc định rỗng — xem mục 5), `bun run build` → `dist/`.
- Stage cuối (`nginx:alpine`): copy `dist/` vào `/usr/share/nginx/html`, copy `frontend/nginx.conf` vào `/etc/nginx/conf.d/default.conf`.

### 4. `frontend/.dockerignore`
Loại `node_modules/`, `dist/`, `.env*` (trừ khi cần thiết) ở cả root lẫn `frontend/`.

### 5. `frontend/nginx.conf`
```
location /api/  → proxy_pass http://api:8080;   (kèm X-Forwarded-For/Proto)
location /hubs/ → proxy_pass http://api:8080;   (kèm Upgrade/Connection cho SignalR)
location /      → try_files $uri /index.html;   (SPA fallback)
```
Không có location nào cho `/hangfire` hay `/metrics` — cố ý, giữ nguyên nguyên tắc "chặn ở network layer" đã có. Vì mọi request tới API đều same-origin qua nginx, build frontend với `VITE_API_BASE_URL=` (rỗng) để axios gọi path tương đối (`/api/...`) — khớp với route `[Route("api/...")]` đã khai báo sẵn ở backend, không cần biết domain thật lúc build image.

### 6. `docker-compose.prod.yml` (root, tách khỏi `docker-compose.yml` dev hiện tại)
3 service: `postgres` (giữ nguyên image `postgres:16-alpine`, credentials qua `.env.prod`), `api` (build từ `backend/Dockerfile`, `ASPNETCORE_ENVIRONMENT=Production`, bind-mount `appsettings.json` thật — xem mục 7, volume `api-uploads:/app/uploads` + `api-logs:/app/logs`), `web` (build từ `frontend/Dockerfile` với context root, publish port `80:80`, `depends_on: api`). Mạng nội bộ có subnet cố định (vd. `172.28.0.0/16`) để cấu hình `ForwardedHeadersSettings:KnownNetworks` cho api tin tưởng header `X-Forwarded-*` từ nginx (nếu không set, rate limiter sẽ nhận nhầm mọi request đều đến từ IP của nginx thay vì IP client thật — đã có tiền lệ y hệt trong quyết định CORS/proxy cũ). Không đưa Mailpit/Prometheus/Grafana vào file này — email cần SMTP thật, monitoring đã được quyết định là phạm vi local-dev only.

### 7. Không tạo file appsettings mới trong git — dùng đúng convention hiện có
Thay vì bake secret vào image hay liệt kê lại toàn bộ config qua env var (rườm rà vì có mảng lồng nhau như `Serilog:WriteTo`), giữ đúng pattern hiện tại của repo: `cp appsettings.Example.json → appsettings.json` (gitignored), điền giá trị thật (connection string trỏ `Host=postgres`, JWT secret thật, SMTP thật, CORS origin là domain thật...), rồi bind-mount file này vào container tại `/app/appsettings.json:ro` trong `docker-compose.prod.yml`. Nhất quán với ghi chú trong `serena.md`: "`appsettings*.json` ... contain real secrets".

### 8. `.env.prod.example` (root, commit vào git)
Chỉ chứa các biến ở cấp docker-compose (không phải cấp app): `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `VITE_API_BASE_URL` (build arg, mặc định rỗng). Thêm ngoại lệ `!.env.prod.example` vào `.gitignore` (đang có sẵn pattern `!.env.example` / `!.env.e2e.example` — làm tương tự).

### 9. `README.md`
Thêm mục ngắn "Production (Docker)" mô tả 4 bước: copy + điền `appsettings.json` và `.env.prod`, `docker compose -f docker-compose.prod.yml up -d --build`, và lưu ý `/hangfire`/`/metrics` không được expose qua nginx.

## Việc cố ý bỏ qua (nói rõ để tránh hiểu nhầm là thiếu sót)

- **Không TLS** — nginx nghe HTTP thuần ở bước đầu này; thêm chứng chỉ (Let's Encrypt/Traefik) là bước mở rộng sau, không thuộc "bản đơn giản nhất".
- **Không healthcheck cho `api`/`web` trong compose** — repo chưa có endpoint `/health` (`MapHealthChecks` không tồn tại trong `Program.cs`); thêm health endpoint là thay đổi code, ngoài phạm vi thuần hạ tầng của kế hoạch này.
- **Không Redis** — cache và SignalR vẫn in-memory, đúng như đã thống nhất "single-instance" (RẤT quan trọng: không được scale `api` lên >1 replica với cấu hình này, sẽ vỡ cache/session/SignalR).

## Verification

Không có thay đổi code C#/TS nên không cần chạy `dotnet test` / `bun run test:run`. Verify bằng cách chạy thực tế:
```bash
cp backend/src/StarterKit.API/appsettings.Example.json backend/src/StarterKit.API/appsettings.json
# sửa appsettings.json: ConnectionStrings, JwtSettings, CorsSettings, EmailSettings...
cp .env.prod.example .env.prod   # điền POSTGRES_*
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
docker compose -f docker-compose.prod.yml logs api   # xác nhận migration chạy thành công, không lỗi
curl -i http://localhost/                              # frontend load được (index.html)
curl -i http://localhost/api/auth/login -X POST ...     # xác nhận nginx proxy tới api, nhận response từ backend (không phải 404 từ nginx)
curl -i http://localhost/hangfire                        # xác nhận KHÔNG proxy được (nginx 404), đúng thiết kế
```
