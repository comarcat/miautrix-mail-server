#!/usr/bin/env bash
export MIAUTRIX_DB_CONNECTION="Host=localhost;Database=miautrix_dev;Username=postgres;Password=postgres"
dotnet ef migrations add InitialCreate --project src/Miautrix.Mail.Persistence --output-dir Migrations
