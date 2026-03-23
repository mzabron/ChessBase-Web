# ChessXiv Backend Documentation

This document describes the backend implementation under `backend/`. The schema diagram companion for this document is `docs/database-schema.png`.

## 1. Scope

This documentation covers:

1. Solution structure and layer responsibilities
2. Runtime bootstrap, dependency injection, middleware, and configuration
3. Authentication and security model
4. HTTP API surface and endpoint behavior
5. Application services and repository behavior
6. Domain entities and database mapping
7. Chess engine and position processing
8. Import, draft, promotion, and explorer workflows
9. CLI import path
10. Testing strategy and coverage map
11. Operations and developer commands

This document intentionally excludes generated files and build artifacts such as `bin/` and `obj/`.

## 2. Solution Topology

### 2.1 Projects

Primary solution:

- `backend/ChessXiv.sln`

Backend projects:

1. `backend/src/ChessXiv.Api`
2. `backend/src/ChessXiv.Application`
3. `backend/src/ChessXiv.Domain`
4. `backend/src/ChessXiv.Infrastructure`
5. `backend/src/ChessXiv.Cli`

Test projects:

1. `backend/tests/ChessXiv.UnitTests`
2. `backend/tests/ChessXiv.IntegrationTests`

### 2.2 Layer Responsibilities

- `ChessXiv.Api`: ASP.NET Core host, controllers, auth endpoints, JWT, CORS, exception handling.
- `ChessXiv.Application`: use-case orchestration (PGN import, draft import/promotion, explorer, position play, hashing/normalization).
- `ChessXiv.Domain`: chess entities and engine primitives (board state, SAN transition, FEN serialization, Zobrist hashing).
- `ChessXiv.Infrastructure`: EF Core DbContext, migrations, repository implementations, unit-of-work, quota service.
- `ChessXiv.Cli`: headless importer for marking imported games as master data.

### 2.3 Platform and Packages

- .NET target framework: `net10.0` (all projects).
- Database: PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL`.
- ORM: EF Core 10.
- Identity: ASP.NET Core Identity (`IdentityDbContext<ApplicationUser>`).
- Auth: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`).
- Tests: xUnit + `Microsoft.NET.Test.Sdk` + `coverlet.collector`.
- Integration tests: `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers.PostgreSql`.

## 3. Runtime Bootstrap

Source: `backend/src/ChessXiv.Api/Program.cs`

### 3.1 Startup Sequence

1. Build web app host.
2. Register MVC controllers and problem details.
3. Register CORS policy `Frontend` with:
   - `AllowAnyOrigin`
   - `AllowAnyMethod`
   - `AllowAnyHeader`
4. Bind JWT options from `Jwt` section.
5. Enforce presence of `Jwt:SigningKey` (throws `InvalidOperationException` if missing).
6. Register `ChessXivDbContext` (Npgsql, `ConnectionStrings:DefaultConnection`).
7. Configure IdentityCore and token providers.
8. Configure JWT bearer validation parameters.
9. Register authorization services.
10. Register application/infrastructure/domain services.
11. Build app.
12. Install global exception handler.
13. Enable authentication.
14. Enable CORS policy `Frontend`.
15. Enable authorization.
16. Map controllers and run.

### 3.2 Dependency Injection Registration

Registered as scoped:

- `IPgnParser -> PgnService`
- `IGameRepository -> GameRepository`
- `IGameExplorerRepository -> GameExplorerRepository`
- `IPlayerRepository -> PlayerRepository`
- `IDraftImportRepository -> DraftImportRepository`
- `IDraftPromotionRepository -> DraftPromotionRepository`
- `IPositionImportCoordinator -> PositionImportCoordinator`
- `IBoardStateSerializer -> FenBoardStateSerializer`
- `IBoardStateFactory -> BoardStateFactory`
- `IBoardStateTransition -> BitboardBoardStateTransition`
- `IPositionHasher -> ZobristPositionHasher`
- `IUnitOfWork -> EfUnitOfWork`
- `IPgnImportService -> PgnImportService`
- `IDraftImportService -> DraftImportService`
- `IDraftPromotionService -> DraftPromotionService`
- `IGameExplorerService -> GameExplorerService`
- `IPositionPlayService -> PositionPlayService`
- `IQuotaService -> UserQuotaService`
- `IJwtTokenService -> JwtTokenService`
- `IEmailSender -> LoggingEmailSender`

### 3.3 Middleware Behavior

Global exception handler:

- Logs exception with category `GlobalExceptionHandler`.
- Returns HTTP `500` with JSON `ProblemDetails`:
  - `Title = Internal Server Error`
  - `Detail = An unexpected error occurred. Please try again later.`
  - `Status = 500`
  - `Instance = request path`

Pipeline order:

1. `UseExceptionHandler(...)`
2. `UseAuthentication()`
3. `UseCors("Frontend")`
4. `UseAuthorization()`
5. `MapControllers()`

## 4. Configuration

Primary files:

- `backend/src/ChessXiv.Api/appsettings.json`
- `backend/src/ChessXiv.Api/appsettings.Example.json`

### 4.1 Connection Strings

- Key: `ConnectionStrings:DefaultConnection`
- Format: PostgreSQL connection string

### 4.2 JWT Options (`Jwt`)

- `Issuer` (default: `ChessXiv.Api`)
- `Audience` (default: `ChessXiv.Web`)
- `SigningKey` (required)
- `ExpirationMinutes` (default: `60`)

### 4.3 CLI Configuration

CLI (`backend/src/ChessXiv.Cli/Program.cs`) resolves connection string from:

1. `CHESSXIV_CONNECTION_STRING` environment variable
2. `ConnectionStrings:DefaultConnection`

PGN path resolution order:

1. first positional CLI argument
2. `CHESSXIV_PGN_PATH` env var
3. ancestor search fallback to `backend/tests/TestData/games_sample.pgn`

## 5. Security Model

### 5.1 Identity User

Source: `backend/src/ChessXiv.Infrastructure/Data/ApplicationUser.cs`

`ApplicationUser` extends `IdentityUser` with:

- `CreatedAtUtc: DateTime`
- `UserTier: string` (default `Free`)

Tier semantics note:

- `Free` and `Premium` are internal/admin indicators used to control storage and quota limits.
- They do not represent public paid subscription plans.

### 5.2 Password Policy

Configured in `Program.cs`:

- minimum length `8`
- require digit `true`
- require uppercase `true`
- require lowercase `true`
- require non-alphanumeric `false`
- unique email `true`

### 5.3 JWT Claims and Validation

Token generation source: `backend/src/ChessXiv.Api/Authentication/JwtTokenService.cs`

Claims added:

- `sub` (`JwtRegisteredClaimNames.Sub`) = user id
- `unique_name` = username
- `email` = email
- `ClaimTypes.NameIdentifier` = user id
- `ClaimTypes.Name` = username

Token validation settings:

- issuer, audience, signing key, lifetime all validated
- clock skew = 1 minute

### 5.4 Password Reset

Source: `backend/src/ChessXiv.Api/Controllers/AuthController.cs`

- reset token generated via Identity
- token is Base64Url-encoded before send
- reset endpoint Base64Url-decodes received token
- forgot-password always returns success text even if user does not exist
- email transport is currently logging only (`LoggingEmailSender`)

## 6. API Surface

### 6.1 Auth (`api/auth`)

Source: `backend/src/ChessXiv.Api/Controllers/AuthController.cs`

1. `POST /api/auth/register`
   - request: `AuthRegisterRequest(login, email, password)`
   - response: `AuthTokenResponse`
   - validation: all fields required

2. `POST /api/auth/login`
   - request: `AuthLoginRequest(login, password)`
   - lookup: username first, then email
   - response: `AuthTokenResponse`

3. `POST /api/auth/forgot-password`
   - request: `ForgotPasswordRequest(email)`
   - always returns `200` generic message

4. `POST /api/auth/reset-password`
   - request: `ResetPasswordRequest(email, token, newPassword)`
   - validates token format and Identity reset result

### 6.2 PGN Import (`api/pgn`)

Source: `backend/src/ChessXiv.Api/Controllers/PgnImportController.cs`

1. `POST /api/pgn/import` (anonymous)
   - request: `PgnImportRequest(pgn)`
   - response: `PgnImportResult(parsedCount, importedCount, skippedCount)`

2. `POST /api/pgn/drafts/import` (`[Authorize]`)
   - request: `DraftImportRequest(pgn, importSessionId?)`
   - response: `DraftImportResult(importSessionId, parsedCount, importedCount, skippedCount, expiresAtUtc)`

3. `POST /api/pgn/drafts/{importSessionId}/promote` (`[Authorize]`)
   - request: `DraftPromotionRequest(userDatabaseId, duplicateHandling)`
   - duplicate handling enum:
     - `KeepExisting`
     - `OverrideMetadata`
   - response: `DraftPromotionResult(importSessionId, promotedCount, duplicateCount, overriddenCount, skippedCount)`

Current user id extraction in draft endpoints:

- `ClaimTypes.NameIdentifier` fallback to `sub`

### 6.3 Game Explorer (`api/games/explorer`)

Source: `backend/src/ChessXiv.Api/Controllers/GameExplorerController.cs`

1. `POST /api/games/explorer/search` (`[AllowAnonymous]`)
   - request: `GameExplorerSearchRequest`
   - response: `PagedResult<GameExplorerItemDto>`
   - maps `ForbiddenException` to `403`
   - maps `KeyNotFoundException` to `404`

2. `POST /api/games/explorer/position/move` (no auth attribute; currently anonymous)
   - request: `PositionMoveRequest(fen, from, to, promotion?)`
   - response: `PositionMoveResponse(isValid, fen?, san?, error?)`

3. `POST /api/games/explorer/move-tree` (`[Authorize]`)
   - request: `MoveTreeRequest(fen, source, userDatabaseId?, importSessionId?, maxMoves)`
   - response: `MoveTreeResponse(totalGamesInPosition, moves[])`
   - source enum:
     - `UserDatabase`
     - `StagingSession`

### 6.4 User Databases (`api/user-databases`)

Source: `backend/src/ChessXiv.Api/Controllers/UserDatabasesController.cs`

1. `GET /api/user-databases/mine` (`[Authorize]`)
   - returns owned DB list (`UserDatabaseDto[]`)

2. `GET /api/user-databases/bookmarks` (`[Authorize]`)
   - returns bookmarked DB list (`BookmarkedUserDatabaseDto[]`)
   - only visible bookmarks: public DBs or own private DBs

3. `GET /api/user-databases/{id}`
   - returns `UserDatabaseDto`
   - private DB access restricted to owner

4. `POST /api/user-databases` (`[Authorize]`)
   - request: `CreateUserDatabaseRequest(name, isPublic)`
   - enforces owner+name uniqueness

5. `PUT /api/user-databases/{id}` (`[Authorize]`)
   - request: `UpdateUserDatabaseRequest(name, isPublic)`
   - owner-only, uniqueness preserved

6. `DELETE /api/user-databases/{id}` (`[Authorize]`)
   - owner-only delete

7. `POST /api/user-databases/{id}/bookmark` (`[Authorize]`)
   - creates bookmark if not already present

8. `DELETE /api/user-databases/{id}/bookmark` (`[Authorize]`)
   - idempotent removal

9. `POST /api/user-databases/{id}/games` (`[Authorize]`)
   - request: `AddGamesToDatabaseRequest(gameIds[])`
   - verifies all game IDs exist
   - skips already-linked game IDs
   - stores metadata snapshot on join row (`Date`, `Year`, `Event`, `Round`, `Site`)

10. `DELETE /api/user-databases/{id}/games/{gameId}` (`[Authorize]`)
    - removes game link from selected DB

## 7. Application Services

### 7.1 PGN Import Service

Source: `backend/src/ChessXiv.Application/Services/PgnImportService.cs`

Responsibilities:

- parse PGN stream via `IPgnParser`
- skip games without white or black player names
- optional `markAsMaster`
- compute:
  - `Year` from date
  - `MoveCount`
  - `GameHash`
- resolve/create players from normalized names
- populate full position chain for each game
- persist in batches and clear EF change tracker after each batch

### 7.2 Draft Import Service

Source: `backend/src/ChessXiv.Application/Services/DraftImportService.cs`

Key behavior:

- per-user draft session model with 7-day TTL (`DefaultDraftTtl`)
- if no `importSessionId`:
  - deletes previous unpromoted sessions for owner
  - creates a fresh session
- if `importSessionId` provided:
  - verifies ownership, not promoted, not expired
- enforces quota from `IQuotaService`
- quota thresholds from `UserQuotaService`:
   - guest: `200_000` (same as `Free`)
  - free: `200_000`
   - premium: `unlimited` (`int.MaxValue`)
- `Free`/`Premium` labels are operational quota flags only (admin-side), not monetized subscription tiers.
- wraps import in transaction
- maps parsed `Game` to `StagingGame`, computes hash, includes moves
- computes staging positions by converting to transient `Game` and reusing `PositionImportCoordinator`
- clears tracker after batch save

### 7.3 Draft Promotion Service

Source: `backend/src/ChessXiv.Application/Services/DraftPromotionService.cs`

Key behavior:

- promotion batch size `500`
- validates:
  - session exists, owner matches, not already promoted
  - target DB exists and belongs to owner
- transactional promotion (`IUnitOfWork.BeginTransactionAsync`)
- duplicate detection by `GameHash` within target user database
- duplicate handling:
  - `KeepExisting`: skip duplicate
  - `OverrideMetadata`: update existing join metadata
- for new games:
  - maps staging to new main `Game` (new IDs for `Game`/`Move`/`Position`)
  - resolves players (create missing by normalized name)
  - inserts `UserDatabaseGame` link
- deletes promoted staging rows
- marks session as promoted (`PromotedAtUtc`)
- rollback on exception

### 7.4 Explorer Service

Source: `backend/src/ChessXiv.Application/Services/GameExplorerService.cs`

Search behavior:

- validates access to specific user database (if provided)
- normalizes pagination (`page >= 1`, `pageSize <= 200`, default `50`)
- normalizes player filters via `PlayerNameNormalizer`
- resolves player IDs from partial name matching
- early-empty if requested white/black filter has no matching player IDs
- position filtering:
  - `Exact`: parse FEN and compute Zobrist hash
  - `Subset`: requires non-empty FEN input

Move-tree behavior:

- requires non-empty owner user id
- normalizes and parses FEN
- computes fen hash for repository query
- clamps `MaxMoves` to `1..100` (default `20`)
- computes and rounds outcome percentages to two decimals

### 7.5 Position Play Service

Source: `backend/src/ChessXiv.Application/Services/PositionPlayService.cs`

Purpose:

- apply one move from client coordinates (`from`/`to`) to a FEN position
- return validated SAN and resulting FEN

Highlights:

- validates coordinate format (`a1`-`h8`)
- validates source piece belongs to side-to-move
- defaults pawn promotion to queen when destination rank is promotion rank and explicit promotion is missing
- generates SAN candidates (including castling and disambiguations), tests candidates via `TryApplySan`, then verifies resulting board matches requested move
- returns structured invalid reasons instead of throwing

### 7.6 Parser and Utility Services

### PGN Parser (`PgnService`)

Source: `backend/src/ChessXiv.Application/Services/PgnService.cs`

- async stream parsing (`IAsyncEnumerable<Game>`)
- robust game block detection for streamed input
- parses tags, moves, comments
- extracts `%eval` and `%clk` from comment payloads
- supports both `1.` and `1...` numbering patterns
- ignores result tokens and variation parentheses during move list parse
- infers trailing result token if result tag is `*`
- date handling:
  - parse exact `yyyy.MM.dd`
  - fallback to year extraction when full date not parseable

### Game Hash (`GameHashCalculator`)

Source: `backend/src/ChessXiv.Application/Services/GameHashCalculator.cs`

- SHA-256 hash over normalized players + normalized move stream
- normalizes castling notation (`0-0`/`0-0-0` to `O-O`/`O-O-O`)
- strips SAN annotations (`+`, `#`, `?`, `!`)
- strips en-passant notation markers
- supports UCI-like move token normalization for stable comparisons

### Player Name Normalizer

Source: `backend/src/ChessXiv.Application/Services/PlayerNameNormalizer.cs`

- trims and lowercases
- removes diacritics via unicode decomposition
- collapses whitespace
- transliteration map for selected characters (`l`/`d` cases)
- supports both `Last, First` and `First ... Last` name parsing

### Position Import Coordinator

Source: `backend/src/ChessXiv.Application/Services/PositionImportCoordinator.cs`

- starts from initial board state
- appends starting position at ply `0`
- replays SAN moves in order
- adds one `Position` per half-move (ply)
- stores:
  - `Fen`
  - `FenHash`
  - `PlyCount`
  - `LastMove`
  - `SideToMove`
- stops processing current game if SAN application fails at any step

## 8. Repository Behavior

### 8.1 Game Explorer Repository

Source: `backend/src/ChessXiv.Infrastructure/Repositories/GameExplorerRepository.cs`

Search dataset rules:

- when `UserDatabaseId` set: query only that DB (access-checked)
- otherwise: aggregate distinct games from:
  - public user DBs
  - owner's own DBs (if authenticated)

Filters supported:

- player filters by white/black IDs with optional color-agnostic mode (`IgnoreColors`)
- ELO filters (`One`, `Both`, `Avg`)
- year range
- ECO prefix
- exact result
- move count range
- position filters:
  - exact by `FenHash` (+ exact `Fen` string when provided)
  - subset by piece placement using SQL `LIKE`

Sorting options:

- `Year`, `White`, `WhiteElo`, `Result`, `Black`, `BlackElo`, `Eco`, `Event`, `MoveCount`
- default fallback: `Year DESC, Id`

Move-tree sources:

- user database positions (`Positions`)
- staging session positions (`StagingPositions`)

Move aggregation:

- groups by SAN of next ply move
- computes counts of white wins, draws, black wins
- limits by `MaxMoves`

### 8.2 Draft Repositories

`DraftImportRepository` (`backend/src/ChessXiv.Infrastructure/Repositories/DraftImportRepository.cs`):

- load session by id+owner
- count staging games for session+owner
- delete unpromoted sessions by owner
- add session and staging games

`DraftPromotionRepository` (`backend/src/ChessXiv.Infrastructure/Repositories/DraftPromotionRepository.cs`):

- load session
- load target user database
- fetch staging game pages with moves+positions eager loaded
- find existing `UserDatabaseGame` links by `GameHash`
- add main games and DB links
- delete staging games by ID set
- mark session promoted with `ExecuteUpdateAsync`

### 8.3 Player/Game Repositories

`PlayerRepository`:

- get players by normalized full names
- add missing players
- search candidate IDs via SQL `LIKE` on normalized first/last names

`GameRepository`:

- add range of games

## 9. Domain Model and Persistence

Sources:

- entities in `backend/src/ChessXiv.Domain/Entities`
- mappings in `backend/src/ChessXiv.Infrastructure/Data/ChessXivDbContext.cs`

### 9.1 Core Entities

- `Game`: metadata, players, PGN, hash, move count, master flag
- `Move`: per-move SAN and optional eval/clock for both sides
- `Position`: per-ply FEN snapshot and hash
- `Player`: normalized and display name fields

### 9.2 User Database Entities

- `UserDatabase`: user-owned collection with visibility flag
- `UserDatabaseGame`: many-to-many join with metadata snapshot at link time
- `UserDatabaseBookmark`: user bookmark join to user databases

### 9.3 Staging Entities

- `StagingImportSession`: owner, created/expiry/promotion timestamps
- `StagingGame`: draft game payload and hash
- `StagingMove`: draft move rows
- `StagingPosition`: draft position rows

### 9.4 Identity Extension

- `ApplicationUser`: identity user + `CreatedAtUtc` + `UserTier`

### 9.5 Key Constraints and Indexes

Highlights from model config:

- `Players.NormalizedFullName` unique
- `Games.GameHash` indexed
- `Games.Year, Id` indexed
- `Games.MoveCount` indexed
- `Positions.FenHash` indexed
- `Positions.Fen` indexed
- `Positions(GameId, PlyCount)` indexed
- `UserDatabases(OwnerUserId, Name)` unique
- `UserDatabaseGames` composite key `(UserDatabaseId, GameId)`
- `UserDatabaseBookmarks` composite key `(UserId, UserDatabaseId)`
- `UserDatabaseBookmarks(UserId, CreatedAtUtc)` indexed
- `StagingGames` indexed by owner/session/hash and owner/session/player names
- `StagingPositions.FenHash` indexed
- `StagingPositions(StagingGameId, PlyCount)` indexed
- `ApplicationUser.Email` unique

### 9.6 Deletion Behavior

- user delete cascades to user databases and related joins/bookmarks
- user database delete cascades to `UserDatabaseGames` and `UserDatabaseBookmarks`
- game delete cascades to `Moves`, `Positions`, and `UserDatabaseGames` links
- staging session delete cascades to staging games/moves/positions

## 10. Chess Engine Subsystem

Folder: `backend/src/ChessXiv.Domain/Engine`

Key components:

- `FenBoardStateSerializer`: strict 6-field FEN parse/serialize, occupancy recompute
- `BitboardBoardStateTransition`: SAN application with legality checks
- `ZobristPositionHasher`: deterministic hash from board state
- `BoardStateFactory`: initial board construction
- engine model/types: `BoardState`, `Bitboard`, `Square`, `Piece`, `PieceType`, `Color`, `CastlingRights`

Supported transition capabilities include:

- standard SAN piece and pawn moves
- captures and disambiguation
- castling (`O-O`, `O-O-O`, `0-0`, `0-0-0`)
- promotions (`=N`, `=B`, `=R`, default queen)
- en passant handling
- legality filtering by king safety (`IsKingInCheck` simulation)
- Zobrist key updates for piece placement, side-to-move, castling rights, and en-passant file

## 11. Data and Workflow Lifecycles

### 11.1 Standard Import

1. API receives PGN string.
2. Parser emits `Game` stream.
3. Invalid games (missing white/black) skipped.
4. Hash + players + position chain computed.
5. Batch persisted to main tables.

### 11.2 Draft Import

1. User imports to staging session.
2. Service validates ownership/session and quota.
3. Staging tables receive games, moves, positions.
4. Session can be incrementally appended using `importSessionId`.

### 11.3 Promotion

1. User selects target `UserDatabase`.
2. Staging page loaded.
3. Duplicate detection by `GameHash` against target DB links.
4. Non-duplicates promoted to main `Games` + `UserDatabaseGames`.
5. Duplicates either skipped or metadata-overridden.
6. Staging rows removed.
7. Session marked promoted.

### 11.4 Explorer

1. Query scope built from public and/or owned databases.
2. Name filters resolve player ID sets.
3. Scalar and position filters applied.
4. Sorted page projected into lightweight DTO.
5. Move-tree query aggregates next SAN moves and result distribution.

## 12. Migrations

Location: `backend/src/ChessXiv.Infrastructure/Migrations`

Recent migration timeline includes:

- `20260318120841_AddIdentityAndUserDatabases`
- `20260318185037_AddStagingArea`
- `20260323111724_AddUserDatabaseBookmarks`
- `20260323173714_AddUserTierAndPositionFenIndex`

Model snapshot: `ChessXivDbContextModelSnapshot.cs`

## 13. Testing Strategy

### 13.1 Unit Tests

Location: `backend/tests/ChessXiv.UnitTests`

Coverage includes:

- PGN parsing tags/moves and parser edge cases
- SAN transition legality and board state transitions
- FEN serialization
- game hashing and player name normalization
- import/promotion service behavior and mappings
- game explorer service logic
- JWT token service
- auth controller behavior

### 13.2 Integration Tests

Location: `backend/tests/ChessXiv.IntegrationTests`

Uses PostgreSQL Testcontainers (`postgres:16-alpine`) via `PostgresTestFixture`.

Coverage includes:

- PGN import API contract behavior and 500 problem-details handling
- persistence of imported games/moves/positions
- draft promotion happy-path, duplicate handling modes, transactional rollback
- user database ownership, many-to-many linking, cascade integrity
- user tier quota behavior (`Free`, `Premium`, guest default)
- bookmark uniqueness and cascading behavior
