# Codebase Structure

Top level: `backend/` (ASP.NET Core API, below), `frontend/` (Vue 3 + Vite dashboard), `shared/` (`docs/api`, `docs/architecture`, `openapi/`). Layer detail below is relative to `backend/`.

```
backend/
  src/
  FeedbackHub.Domain/
    Entities/        Account, ApiKey, RefreshToken, StoredFile, SystemSetting, BaseEntity
    Enums/            AccountRole
    Exceptions/       ApiException, DomainException, ConflictException, ForbiddenException,
                       FormattedDomainException, NotFoundException, TooManyRequestsException,
                       UnauthorizedException, DomainMessages
    Interfaces/       IRepository<T,TId>
  FeedbackHub.Application/
    Common/
      Interfaces/     ICacheService, ICurrentUserService, IDateTimeProvider, IJwtTokenService,
                       IPasswordHasher, ISecretProtector, IStorageService, ISystemSettingsService,
                       IUnitOfWork, IUserTimeZoneProvider
      Mappings/       EntityMapper.cs (Mapperly)
      Models/         PagedResult, PaginationRequest
      Settings/       AuthSettings, CacheSettings, JwtSettings, StorageSettings
    Services/         Accounts/, ApiKeys/, AuditLogs/, Auth/, Files/, SystemSettings/
    Resources/        ApplicationMessages.cs, Messages.resx (vi, default) / Messages.en.resx
    DependencyInjection.cs  (AddApplication)
  FeedbackHub.Infrastructure/
    Persistence/      AppDbContext, Configurations/, Repositories/, Seeding/, Migrations/,
                       PersistenceExtensions.cs, UnitOfWork.cs
    Services/         JwtTokenService, PasswordHasher, ApiKeyAuthenticationHandler, ApiKeyClaims,
                       CurrentUserService, DataProtectionSecretProtector, DateTimeProvider,
                       MemoryCacheService, UserTimeZoneProvider, AuthExtensions, Storage/, StorageExtensions.cs
    DependencyInjection.cs  (AddInfrastructure)
  FeedbackHub.API/
    Controllers/      AccountsController, ApiKeysController, AuditLogsController, AuthController,
                       HealthController, ProfileController, SystemSettingsController
    Extensions/        OpenApiExtensions.cs
    Middleware/        ExceptionHandlingMiddleware, UserTimeZoneMiddleware
    Program.cs
    appsettings.Example.json  (template — real appsettings.json/*.Development/*.Production are gitignored)
  tests/
    FeedbackHub.Domain.Tests/Entities/
    FeedbackHub.Application.Tests/Services/{Accounts?,Auth}/ + TestSupport/
```

Note: an earlier version of this repo (pre-2026-07-15 cleanup) had a chat/RAG feature set (Project multi-tenancy, Chat, ChatSessions, QaTesting, AgentTools, HydroSync, TokenQuota, AdminDashboard, and an `AI/` LLM+embedding+rerank+retrieval+vector-store stack). That was intentionally removed to turn this into a generic template — see `.claude/decisions.md` for the rationale.
