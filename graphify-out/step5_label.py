import json
from pathlib import Path
from graphify.build import build_from_json
from graphify.cluster import score_all
from graphify.analyze import god_nodes, surprising_connections, suggest_questions
from graphify.report import generate
from graphify.export import to_json

repo_root = Path(r"C:\Users\miauadmin\OneDrive\Documentos\GitHub\miautrix-mail-server")
out_dir = repo_root / "graphify-out"

extraction = json.loads((out_dir / ".graphify_extract.json").read_text(encoding="utf-8"))
detection = json.loads((out_dir / ".graphify_detect.json").read_text(encoding="utf-8"))
analysis = json.loads((out_dir / ".graphify_analysis.json").read_text(encoding="utf-8"))

G = build_from_json(extraction, root=str(repo_root), directed=False)
communities = {int(k): v for k, v in analysis["communities"].items()}
cohesion = {int(k): v for k, v in analysis["cohesion"].items()}
tokens = {"input": extraction.get("input_tokens", 0), "output": extraction.get("output_tokens", 0)}

labels = {
    0: "Core Platform & Identity (Epic 1)",
    1: "Mail Transport & Protocols (Epic 2)",
    2: "Operations & Governance (Epic 4)",
    3: "SMTP, Anti-Spam & Malware Seams",
    4: "Platform Architecture & Core Stack",
    5: "Client Surfaces & APIs (Epic 3)",
    6: "IMAP Storage & Sieve Filtering",
    7: "Search Indexing & Mail Flow Rules",
    8: "Dependency Version Verifier",
    9: "JMAP Protocol Engine"
}

questions = suggest_questions(G, communities, labels)

report = generate(G, communities, cohesion, labels, analysis["gods"], analysis["surprises"], detection, tokens, str(repo_root), suggested_questions=questions)
(out_dir / "GRAPH_REPORT.md").write_text(report, encoding="utf-8")
(out_dir / ".graphify_labels.json").write_text(json.dumps({str(k): v for k, v in labels.items()}, ensure_ascii=False), encoding="utf-8")

wrote = to_json(G, communities, str(out_dir / "graph.json"), community_labels=labels)
print("Report and graph.json updated with community labels.")
