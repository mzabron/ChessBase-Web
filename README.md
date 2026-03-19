# ChessXiv

![Status: Incubating](https://img.shields.io/badge/status-active_development-orange)

This project will include a web-based chess database explorer. I decided to build an application similar to ChessBaseReader 2017, as it is not available on some platforms, such as macOS. The application will allow users to import their own PGN databases and freely search through them using multiple filtering options. I also plan to introduce several new features that provide more open access to chess games and make it easier to share them across the community.
 
1. Local Configuration
To avoid passing connection strings in your terminal, initialize User Secrets. This stores your password safely on your machine (outside of Git).

```
dotnet user-secrets init --project src/ChessXiv.Cli
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=chessxiv_db;Username=chessxiv_user;Password=YOUR_PASSWORD" --project src/ChessXiv.Cli

```
