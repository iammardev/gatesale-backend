# GateSale — Backend

Backend for **GateSale**, a secure school-based marketplace where verified
students buy and sell within their own school community. Built in **ASP.NET Core**
with a clean, layered architecture, EF Core + PostgreSQL, and AWS for auth,
storage, and delivery.

## Features
- **Verified accounts** — school-email domain whitelisting + parental-consent flow
- **Auth** — AWS Cognito (registration, email verification, password reset, JWT)
- **Products** — listings, images on AWS S3, search, moderation
- **Locker delivery** — PUDO locker integration with webhooks + live order tracking
- **Disputes & transactions** — dispute records, order-tracking events, transactions

## Architecture
GateSale.API             # Controllers, middleware, DI, Program.cs
GateSale.Core            # Entities, DTOs, enums, interfaces, models
GateSale.Infrastructure  # EF Core DbContext, migrations, service implementations
GateSale.Tests           # Tests
Controllers: Auth, Product, Locker, UserLocker, OrderTracking, PudoWebhook
Services: CognitoService, S3StorageService, PudoLockerService, DomainValidationService, EmailService

## Tech stack
ASP.NET Core (C#) · EF Core + PostgreSQL (AWS RDS) · AWS Cognito · AWS S3 · PUDO lockers

## Getting started
```bash
dotnet restore
cp GateSale.API/appsettings.example.json GateSale.API/appsettings.json  # fill in values
dotnet ef database update --project GateSale.Infrastructure --startup-project GateSale.API
dotnet run --project GateSale.API

▎ Secrets go in appsettings.json / env vars and must not be committed.
