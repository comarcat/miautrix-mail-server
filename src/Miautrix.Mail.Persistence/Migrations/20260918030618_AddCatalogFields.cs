using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCatalogFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Idempotent: add Code/Name columns only when missing
        migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='permissions' AND column_name='code') THEN ALTER TABLE permissions ADD COLUMN code text; END IF; END $$;");
        migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='permissions' AND column_name='name') THEN ALTER TABLE permissions ADD COLUMN name text; END IF; END $$;");
        migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='roles' AND column_name='code') THEN ALTER TABLE roles ADD COLUMN code text; END IF; END $$;");
        migrationBuilder.Sql("DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='roles' AND column_name='name') THEN ALTER TABLE roles ADD COLUMN name text; END IF; END $$;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "code", table: "permissions");
        migrationBuilder.DropColumn(name: "name", table: "permissions");
        migrationBuilder.DropColumn(name: "code", table: "roles");
        migrationBuilder.DropColumn(name: "name", table: "roles");
    }
}