# ChessXiv

**A High-Performance Chess Database & Explorer Web App**

## Introduction
ChessXiv was created to bring a ChessBase-style experience to all platforms. It enables users to natively import their own PGN databases and efficiently search through them using a range of advanced filtering options. In addition, it introduces new functionality such as user accounts, allowing individuals to create community databases and share access with others. More features will be released soon!

**Live Project:** [chessxiv.org](https://chessxiv.org)  

## What ChessXiv offers in the current state?
Note that ChessXiv is currently in early development stage. Described feature will be actively upadted along adding new functionalities. However right now it can offer:
* **Imports up to 200 MB** - Users can import (.pgn) files up to 200 MB (approximately 50 000 - 80 000, based on games metadata). When importing games can take some time (about 1s for each 400 games), then all filters and sorting filters works almost instantly which make it really convinient
* **Multiple filter options** -  User can filter imported games by names, ratings, year, board positions, ECO code, result and move count setting specified options to each of listed categories also imported games/ filtered result might be sorted by each of dipslayed column.
* **Board Set Up** - users can move pieces around the board creating custom games, and then using it as a filter for imported games. Also you can set up a position a single position (not going from the start position) with the board editor.
* **Moves Tree** - for each set of imported games moves tress is automatically created - it shows the user what's are the most common played next moves, taking the current position on the board and all imported games, or just then one that you filter for.
* **Convinient UI** - UI is very flexible, you can change the size fo the components dynamically by dragging vertical line in between components, there's also a focus mode which changes the componentes aligment and make board bigger and dark mode.

In current state be able to import games you need to create an account and verify your email (confirmation email will be sent automatically). As right now it may seem useless, in the future more features related to user accounts will be added. Also that's the way to avoid mass-bot imports that might crash the server.


---

## Architecture & Technicals

### High-Performance Chess Logic (Bitboards)
At the core of ChessXiv is a custom-built chess engine logic layer designed for maximum computational efficiency.
* Bitboard Representation (ulong64): Board states are represented using 64-bit integers (UInt64). This allows for lightning-fast legal move generation and board validation using bitwise operations (AND, OR, XOR, Shifting), which is significantly more efficient than traditional array-based board representations.
* Zobrist Hashing: Every unique board position is reduced to a 64-bit hash. This enables transposition-aware searches, meaning the system can identify the same board state even if it was reached through a different sequence of moves.

### Dual-Database Staging Architecture
To handle massive PGN uploads without impacting the performance of the primary production data, ChessXiv utilizes a decoupled storage strategy:
* The Staging Layer: When a user uploads a PGN, data is first ingested into a Temporary Staging Environment. This allows for high-speed validation and indexing of draft games without polluting the main global database.
* Automated Data Lifecycle: To ensure optimal storage efficiency, a background Hosted Service (StagingDraftCleanupService) monitors the staging tables. Any temporary data older than 24 hours is automatically purged, keeping the system lean and responsive.
* PostgreSQL Binary COPY: The infrastructure layer utilizes the Npgsql Binary COPY protocol. This maps C# objects directly to the PostgreSQL internal binary format, streaming data into the database at the maximum hardware limit—bypassing the overhead of standard SQL INSERT statements.

---

## User Identity & Lifecycle
ChessXiv implements a robust, enterprise-grade account system using ASP.NET Core Identity.

* Two-Step Email Confirmation: To maintain system integrity and prevent bot spam, accounts require email verification. I integrated the Brevo API to handle transactional emails for registration and password resets.
* Secure Token Handling: Confirmation and password-reset tokens are Base64Url-encoded and cryptographically signed, ensuring they cannot be tampered with.
* Account Maintenance: An automated cleanup service handles the lifecycle of unconfirmed accounts, ensuring that abandoned registrations are removed after 24 hours.
* JWT Security: Authentication is handled via stateless JSON Web Tokens (JWT) with 256-bit signing keys, providing a secure, scalable bridge between the Angular frontend and the .NET API.

---

## Database Schema
The schema is optimized for Deep Explorer queries, allowing users to find specific positions and move frequencies across millions of records.

![Database Schema](docs/assets/database-schema.png)