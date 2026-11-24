using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class ImapRecentLegacyBaseline : Migration
{
    /// <inheritdoc />
    // Intentionally a no-op. It previously marked every stored message \Recent; that
    // was removed before any production deploy because it would flood clients with
    // "new" mail after the upgrade. Kept so databases that applied it stay consistent.
    protected override void Up(MigrationBuilder migrationBuilder) { }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
