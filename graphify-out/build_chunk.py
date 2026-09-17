import json
from pathlib import Path

repo_root = Path(r"C:\Users\miauadmin\OneDrive\Documentos\GitHub\miautrix-mail-server")
out_dir = repo_root / "graphify-out"

doc_arch = str(repo_root / "Miautrix_Mail_Server_Final_Architecture_Design.md")
doc_pmi = str(repo_root / "PMI_Project_Charter_and_Plan.md")
doc_blueprint = str(repo_root / "blueprints" / "miautrix-mail-server" / "blueprint.md")
doc_e1 = str(repo_root / "blueprints" / "miautrix-mail-server" / "epics" / "01-core-platform.md")
doc_e2 = str(repo_root / "blueprints" / "miautrix-mail-server" / "epics" / "02-mail-transport.md")
doc_e3 = str(repo_root / "blueprints" / "miautrix-mail-server" / "epics" / "03-surfaces.md")
doc_e4 = str(repo_root / "blueprints" / "miautrix-mail-server" / "epics" / "04-operations.md")
doc_tasks = str(repo_root / "blueprints" / "miautrix-mail-server" / "tasks.json")
doc_claude = str(repo_root / "blueprints" / "miautrix-mail-server" / "workspace" / "CLAUDE.md")
doc_agents = str(repo_root / "blueprints" / "miautrix-mail-server" / "workspace" / "AGENTS.md")
doc_tenancy = str(repo_root / "blueprints" / "miautrix-mail-server" / "workspace" / ".claude" / "rules" / "tenancy.md")
doc_secrets = str(repo_root / "blueprints" / "miautrix-mail-server" / "workspace" / ".claude" / "rules" / "secrets-and-logging.md")
doc_verify = str(repo_root / "blueprints" / "miautrix-mail-server" / "workspace" / ".claude" / "skills" / "verify-task" / "SKILL.md")

nodes = []
edges = []
hyperedges = []

def add_node(id_str, label, file_type, source_file, rationale=None):
    n = {
        "id": id_str,
        "label": label,
        "file_type": file_type,
        "source_file": source_file,
        "source_location": None,
        "source_url": None,
        "captured_at": None,
        "author": None,
        "contributor": None
    }
    if rationale:
        n["rationale"] = rationale
    nodes.append(n)

def add_edge(src, tgt, relation, confidence="EXTRACTED", conf_score=1.0, source_file=doc_arch, weight=1.0):
    edges.append({
        "source": src,
        "target": tgt,
        "relation": relation,
        "confidence": confidence,
        "confidence_score": conf_score,
        "source_file": source_file,
        "source_location": None,
        "weight": weight
    })

# --- Architecture & Design ---
add_node("miautrix_mail_server_final_architecture_design_miautrix_platform", "Miautrix Mail Server Platform", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_modular_monolith", "Modular Monolith Architecture", "rationale", doc_arch,
         rationale="Clear module boundaries and provider interfaces allow components to be extracted into services later if scale requires it.")
add_node("miautrix_mail_server_final_architecture_design_clean_architecture", "Clean Architecture Pattern", "rationale", doc_arch,
         rationale="Inward dependency direction: Domain depends on nothing, Application depends only on Domain.")
add_node("miautrix_mail_server_final_architecture_design_dotnet_10", ".NET 10 LTS Core Engine", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_postgresql", "PostgreSQL Storage & Schema", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_react_typescript", "React & TypeScript Frontend", "concept", doc_arch)

# Protocols & Transport
add_node("miautrix_mail_server_final_architecture_design_smtp_listener", "SMTP Listener & Queue", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_imap_listener", "IMAP Protocol Listener", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_jmap_protocol", "JMAP Protocol Engine", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_managesieve", "ManageSieve Script Engine", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_mailflow_rules", "Mail-Flow Rule Engine & Simulator", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_dns_auth", "DNS Authentication (SPF, DKIM, DMARC, ARC)", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_antispam_quarantine", "Anti-Spam & Quarantine Subsystem", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_fts_search", "Full-Text Search (FTS) Indexing", "concept", doc_arch)

# Provider Seams
add_node("miautrix_mail_server_final_architecture_design_iidentityprovider", "IIdentityProvider", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_ispamprovider", "ISpamProvider", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_imalwareprovider", "IMalwareProvider", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_imailstorage", "IMailStorage", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_isearchprovider", "ISearchProvider", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_iqueueprovider", "IQueueProvider", "concept", doc_arch)
add_node("miautrix_mail_server_final_architecture_design_iauthenticationprovider", "IAuthenticationProvider", "concept", doc_arch)

# Core Rules & Tenancy
add_node("blueprints_miautrix_mail_server_workspace__claude_rules_tenancy_tenant_isolation", "Tenant Isolation Enforcement", "rationale", doc_tenancy,
         rationale="Every tenant-scoped read and write passes through the single authorization helper. Cross-tenant resources return 404.")
add_node("blueprints_miautrix_mail_server_workspace__claude_rules_secrets_and_logging_secrets_logging", "Secrets & Redaction Policy", "rationale", doc_secrets,
         rationale="Redaction list lives in the logger itself. Passwords, TOTP, tokens, session cookies, and message bodies are never logged.")
add_node("blueprints_miautrix_mail_server_workspace_claude_licensing_rules", "Licensing & Over-Allowance Policy", "rationale", doc_claude,
         rationale="Licence service unreachable keeps mail flowing. Over allowance blocks new mailboxes only; never destroys existing data.")
add_node("blueprints_miautrix_mail_server_workspace__claude_skills_verify_task_skill_verify_task", "Verify-Task Protocol", "concept", doc_verify)

# Blueprint Epics
add_node("blueprints_miautrix_mail_server_epics_01_core_platform_epic_1", "Epic 1: Core Platform (T1-T6)", "concept", doc_e1)
add_node("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "Epic 2: Mail Transport & Policy (T7-T13)", "concept", doc_e2)
add_node("blueprints_miautrix_mail_server_epics_03_surfaces_epic_3", "Epic 3: Surfaces (T14-T17)", "concept", doc_e3)
add_node("blueprints_miautrix_mail_server_epics_04_operations_epic_4", "Epic 4: Operations (T18-T21)", "concept", doc_e4)

# Blueprint Tasks
tasks_data = [
    ("t1", "T1: Solution scaffold and CI", doc_tasks, "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1"),
    ("t2", "T2: Domain model and EF Core schema", doc_tasks, "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1"),
    ("t3", "T3: Seed data and indexes", doc_tasks, "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1"),
    ("t4", "T4: Identity: credentials, MFA, sessions", doc_tasks, "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1"),
    ("t5", "T5: Authorization, roles, tenant isolation", doc_tasks, "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1"),
    ("t6", "T6: Audit trail", doc_tasks, "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1"),
    ("t7", "T7: SMTP listener and queue", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t8", "T8: SPF, DKIM, DMARC", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t9", "T9: Anti-spam baseline and quarantine", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t10", "T10: IMAP, storage abstraction, attachments", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t11", "T11: ManageSieve", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t12", "T12: Full-text search indexing", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t13", "T13: Mail-flow rule engine & simulator", doc_tasks, "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2"),
    ("t14", "T14: API contract and OpenAPI", doc_tasks, "blueprints_miautrix_mail_server_epics_03_surfaces_epic_3"),
    ("t15", "T15: Web admin GUI", doc_tasks, "blueprints_miautrix_mail_server_epics_03_surfaces_epic_3"),
    ("t16", "T16: Webmail", doc_tasks, "blueprints_miautrix_mail_server_epics_03_surfaces_epic_3"),
    ("t17", "T17: CLI", doc_tasks, "blueprints_miautrix_mail_server_epics_03_surfaces_epic_3"),
    ("t18", "T18: Desktop admin application", doc_tasks, "blueprints_miautrix_mail_server_epics_04_operations_epic_4"),
    ("t19", "T19: Backup and restore", doc_tasks, "blueprints_miautrix_mail_server_epics_04_operations_epic_4"),
    ("t20", "T20: Blue/green update & migrations ladder", doc_tasks, "blueprints_miautrix_mail_server_epics_04_operations_epic_4"),
    ("t21", "T21: Security hardening & licence gating", doc_tasks, "blueprints_miautrix_mail_server_epics_04_operations_epic_4"),
]

for tid, tlabel, tfile, epic_node in tasks_data:
    nid = f"blueprints_miautrix_mail_server_tasks_{tid}"
    add_node(nid, tlabel, "concept", tfile)
    add_edge(epic_node, nid, "implements", "EXTRACTED", 1.0, doc_blueprint)

# Task dependencies
task_deps = [
    ("t2", "t1"), ("t3", "t2"), ("t4", "t3"), ("t5", "t4"), ("t6", "t5"),
    ("t7", "t6"), ("t8", "t7"), ("t9", "t8"), ("t10", "t9"), ("t11", "t10"),
    ("t12", "t11"), ("t13", "t12"), ("t14", "t13"), ("t15", "t14"),
    ("t16", "t15"), ("t17", "t16"), ("t18", "t17"), ("t19", "t18"),
    ("t20", "t19"), ("t21", "t20")
]
for succ, pred in task_deps:
    add_edge(f"blueprints_miautrix_mail_server_tasks_{succ}", f"blueprints_miautrix_mail_server_tasks_{pred}", "references", "EXTRACTED", 1.0, doc_tasks)

# Semantic and Architectural Connections
add_edge("miautrix_mail_server_final_architecture_design_miautrix_platform", "miautrix_mail_server_final_architecture_design_modular_monolith", "conceptually_related_to", "EXTRACTED", 1.0, doc_arch)
add_edge("miautrix_mail_server_final_architecture_design_modular_monolith", "miautrix_mail_server_final_architecture_design_clean_architecture", "conceptually_related_to", "EXTRACTED", 1.0, doc_arch)
add_edge("miautrix_mail_server_final_architecture_design_miautrix_platform", "miautrix_mail_server_final_architecture_design_dotnet_10", "conceptually_related_to", "EXTRACTED", 1.0, doc_arch)
add_edge("miautrix_mail_server_final_architecture_design_miautrix_platform", "miautrix_mail_server_final_architecture_design_postgresql", "conceptually_related_to", "EXTRACTED", 1.0, doc_arch)
add_edge("miautrix_mail_server_final_architecture_design_miautrix_platform", "miautrix_mail_server_final_architecture_design_react_typescript", "conceptually_related_to", "EXTRACTED", 1.0, doc_arch)

# Linking Epics to Architecture
add_edge("blueprints_miautrix_mail_server_epics_01_core_platform_epic_1", "miautrix_mail_server_final_architecture_design_clean_architecture", "conceptually_related_to", "INFERRED", 0.95, doc_e1)
add_edge("blueprints_miautrix_mail_server_epics_01_core_platform_epic_1", "blueprints_miautrix_mail_server_workspace__claude_rules_tenancy_tenant_isolation", "implements", "EXTRACTED", 1.0, doc_e1)
add_edge("blueprints_miautrix_mail_server_epics_01_core_platform_epic_1", "blueprints_miautrix_mail_server_workspace__claude_rules_secrets_and_logging_secrets_logging", "implements", "EXTRACTED", 1.0, doc_e1)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_smtp_listener", "implements", "EXTRACTED", 1.0, doc_e2)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_dns_auth", "implements", "EXTRACTED", 1.0, doc_e2)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_antispam_quarantine", "implements", "EXTRACTED", 1.0, doc_e2)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_imap_listener", "implements", "EXTRACTED", 1.0, doc_e2)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_managesieve", "implements", "EXTRACTED", 1.0, doc_e2)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_fts_search", "implements", "EXTRACTED", 1.0, doc_e2)
add_edge("blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2", "miautrix_mail_server_final_architecture_design_mailflow_rules", "implements", "EXTRACTED", 1.0, doc_e2)

# Linking Provider Seams
add_edge("blueprints_miautrix_mail_server_tasks_t4", "miautrix_mail_server_final_architecture_design_iidentityprovider", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_tasks_t4", "miautrix_mail_server_final_architecture_design_iauthenticationprovider", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_tasks_t7", "miautrix_mail_server_final_architecture_design_iqueueprovider", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_tasks_t9", "miautrix_mail_server_final_architecture_design_ispamprovider", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_tasks_t9", "miautrix_mail_server_final_architecture_design_imalwareprovider", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_tasks_t10", "miautrix_mail_server_final_architecture_design_imailstorage", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_tasks_t12", "miautrix_mail_server_final_architecture_design_isearchprovider", "implements", "EXTRACTED", 1.0, doc_tasks)

# Operations and Licensing
add_edge("blueprints_miautrix_mail_server_tasks_t21", "blueprints_miautrix_mail_server_workspace_claude_licensing_rules", "implements", "EXTRACTED", 1.0, doc_tasks)
add_edge("blueprints_miautrix_mail_server_epics_04_operations_epic_4", "blueprints_miautrix_mail_server_workspace__claude_skills_verify_task_skill_verify_task", "references", "INFERRED", 0.85, doc_e4)

# Hyperedges
hyperedges.append({
    "id": "miautrix_provider_seams",
    "label": "Miautrix Provider Seams",
    "nodes": [
        "miautrix_mail_server_final_architecture_design_iidentityprovider",
        "miautrix_mail_server_final_architecture_design_ispamprovider",
        "miautrix_mail_server_final_architecture_design_imalwareprovider",
        "miautrix_mail_server_final_architecture_design_imailstorage",
        "miautrix_mail_server_final_architecture_design_isearchprovider",
        "miautrix_mail_server_final_architecture_design_iqueueprovider"
    ],
    "relation": "implement",
    "confidence": "EXTRACTED",
    "confidence_score": 1.0,
    "source_file": doc_agents
})

hyperedges.append({
    "id": "miautrix_core_epics",
    "label": "Miautrix Epics Hierarchy",
    "nodes": [
        "blueprints_miautrix_mail_server_epics_01_core_platform_epic_1",
        "blueprints_miautrix_mail_server_epics_02_mail_transport_epic_2",
        "blueprints_miautrix_mail_server_epics_03_surfaces_epic_3",
        "blueprints_miautrix_mail_server_epics_04_operations_epic_4"
    ],
    "relation": "participate_in",
    "confidence": "EXTRACTED",
    "confidence_score": 1.0,
    "source_file": doc_blueprint
})

output_data = {
    "nodes": nodes,
    "edges": edges,
    "hyperedges": hyperedges,
    "input_tokens": 0,
    "output_tokens": 0
}

chunk_path = out_dir / ".graphify_chunk_01.json"
chunk_path.write_text(json.dumps(output_data, indent=2, ensure_ascii=False), encoding="utf-8")
print(f"Wrote {len(nodes)} nodes, {len(edges)} edges, {len(hyperedges)} hyperedges to {chunk_path}")
