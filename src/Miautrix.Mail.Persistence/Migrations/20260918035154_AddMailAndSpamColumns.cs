using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Miautrix.Mail.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMailAndSpamColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dkim_result",
                table: "spam_verdicts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dmarc_result",
                table: "spam_verdicts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "dnsbl_listed",
                table: "spam_verdicts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "greylisted",
                table: "spam_verdicts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_spam",
                table: "spam_verdicts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "reasons_json",
                table: "spam_verdicts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "spam_verdicts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "score",
                table: "spam_verdicts",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "spam_verdicts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "spf_result",
                table: "spam_verdicts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "threshold",
                table: "spam_verdicts",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "content",
                table: "sieve_scripts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "sieve_scripts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "mailbox_id",
                table: "sieve_scripts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "sieve_scripts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_delivered",
                table: "quarantine",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "quarantined_at",
                table: "quarantine",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "raw_message",
                table: "quarantine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "reasons_json",
                table: "quarantine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "quarantine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "released_at",
                table: "quarantine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "quarantine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "spam_score",
                table: "quarantine",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "quarantine",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "subject",
                table: "quarantine",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "threshold",
                table: "quarantine",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "trained_at",
                table: "quarantine",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body_html",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "body_text",
                table: "messages",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "date",
                table: "messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "flags",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "folder_id",
                table: "messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_read",
                table: "messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "mailbox_id",
                table: "messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "raw_headers",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                table: "messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "storage_path",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "subject",
                table: "messages",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "uid",
                table: "messages",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "message_recipients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "message_id",
                table: "message_recipients",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "message_recipients",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "flag",
                table: "message_flags",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "message_id",
                table: "message_flags",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "engine",
                table: "malware_verdicts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_malware",
                table: "malware_verdicts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "recipient",
                table: "malware_verdicts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "sender",
                table: "malware_verdicts",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "threat_name",
                table: "malware_verdicts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "mailboxes",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "domain_id",
                table: "mailboxes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "mailboxes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "quota_bytes",
                table: "mailboxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "used_bytes",
                table: "mailboxes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "actions_json",
                table: "mail_flow_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "conditions_json",
                table: "mail_flow_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "mail_flow_rules",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "mail_flow_rules",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "mail_flow_rules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "groups",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "group_id",
                table: "group_members",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "member_address",
                table: "group_members",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "mailbox_id",
                table: "folders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "name",
                table: "folders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "folders",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "uid_next",
                table: "folders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "uid_validity",
                table: "folders",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "domain_name",
                table: "dkim_keys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "private_key_pem",
                table: "dkim_keys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "public_key_pem",
                table: "dkim_keys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "selector",
                table: "dkim_keys",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "content_hash",
                table: "attachments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "content_type",
                table: "attachments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "file_name",
                table: "attachments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "message_id",
                table: "attachments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "size_bytes",
                table: "attachments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "storage_path",
                table: "attachments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "aliases",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "target_address",
                table: "aliases",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dkim_result",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "dmarc_result",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "dnsbl_listed",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "greylisted",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "is_spam",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "reasons_json",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "score",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "spf_result",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "threshold",
                table: "spam_verdicts");

            migrationBuilder.DropColumn(
                name: "content",
                table: "sieve_scripts");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "sieve_scripts");

            migrationBuilder.DropColumn(
                name: "mailbox_id",
                table: "sieve_scripts");

            migrationBuilder.DropColumn(
                name: "name",
                table: "sieve_scripts");

            migrationBuilder.DropColumn(
                name: "is_delivered",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "quarantined_at",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "raw_message",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "reasons_json",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "released_at",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "spam_score",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "status",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "subject",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "threshold",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "trained_at",
                table: "quarantine");

            migrationBuilder.DropColumn(
                name: "body_html",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "body_text",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "date",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "flags",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "folder_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "is_read",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "mailbox_id",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "raw_headers",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "storage_path",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "subject",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "uid",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "address",
                table: "message_recipients");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "message_recipients");

            migrationBuilder.DropColumn(
                name: "type",
                table: "message_recipients");

            migrationBuilder.DropColumn(
                name: "flag",
                table: "message_flags");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "message_flags");

            migrationBuilder.DropColumn(
                name: "engine",
                table: "malware_verdicts");

            migrationBuilder.DropColumn(
                name: "is_malware",
                table: "malware_verdicts");

            migrationBuilder.DropColumn(
                name: "recipient",
                table: "malware_verdicts");

            migrationBuilder.DropColumn(
                name: "sender",
                table: "malware_verdicts");

            migrationBuilder.DropColumn(
                name: "threat_name",
                table: "malware_verdicts");

            migrationBuilder.DropColumn(
                name: "address",
                table: "mailboxes");

            migrationBuilder.DropColumn(
                name: "domain_id",
                table: "mailboxes");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "mailboxes");

            migrationBuilder.DropColumn(
                name: "quota_bytes",
                table: "mailboxes");

            migrationBuilder.DropColumn(
                name: "used_bytes",
                table: "mailboxes");

            migrationBuilder.DropColumn(
                name: "actions_json",
                table: "mail_flow_rules");

            migrationBuilder.DropColumn(
                name: "conditions_json",
                table: "mail_flow_rules");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "mail_flow_rules");

            migrationBuilder.DropColumn(
                name: "name",
                table: "mail_flow_rules");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "mail_flow_rules");

            migrationBuilder.DropColumn(
                name: "address",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "name",
                table: "groups");

            migrationBuilder.DropColumn(
                name: "group_id",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "member_address",
                table: "group_members");

            migrationBuilder.DropColumn(
                name: "mailbox_id",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "name",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "role",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "uid_next",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "uid_validity",
                table: "folders");

            migrationBuilder.DropColumn(
                name: "domain_name",
                table: "dkim_keys");

            migrationBuilder.DropColumn(
                name: "private_key_pem",
                table: "dkim_keys");

            migrationBuilder.DropColumn(
                name: "public_key_pem",
                table: "dkim_keys");

            migrationBuilder.DropColumn(
                name: "selector",
                table: "dkim_keys");

            migrationBuilder.DropColumn(
                name: "content_hash",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "content_type",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "file_name",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "message_id",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "size_bytes",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "storage_path",
                table: "attachments");

            migrationBuilder.DropColumn(
                name: "address",
                table: "aliases");

            migrationBuilder.DropColumn(
                name: "target_address",
                table: "aliases");
        }
    }
