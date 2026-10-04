import json
from graphify.cache import save_semantic_cache
from pathlib import Path

repo_root = Path(r'C:\Users\miauadmin\OneDrive\Documentos\GitHub\miautrix-mail-server')
out_dir = repo_root / 'graphify-out'
spec_path = Path(r'C:\Users\miauadmin\.claude\skills\graphify\references\extraction-spec.md')

new = json.loads((out_dir / '.graphify_chunk_01.json').read_text(encoding='utf-8'))
(out_dir / '.graphify_semantic_new.json').write_text(json.dumps({
    'nodes': new.get('nodes', []),
    'edges': new.get('edges', []),
    'hyperedges': new.get('hyperedges', []),
    'input_tokens': new.get('input_tokens', 0),
    'output_tokens': new.get('output_tokens', 0),
}, indent=2, ensure_ascii=False), encoding='utf-8')

uncached = [line for line in (out_dir / '.graphify_uncached.txt').read_text(encoding='utf-8').splitlines() if line]
saved = save_semantic_cache(new.get('nodes', []), new.get('edges', []), new.get('hyperedges', []), root=str(repo_root), allowed_source_files=uncached, prompt_file=str(spec_path))
print(f'Cached {saved} files')

# Merge cached + new
all_nodes = new.get('nodes', [])
all_edges = new.get('edges', [])
all_hyperedges = new.get('hyperedges', [])
seen = set()
deduped = []
for n in all_nodes:
    if n['id'] not in seen:
        seen.add(n['id'])
        deduped.append(n)

merged = {
    'nodes': deduped,
    'edges': all_edges,
    'hyperedges': all_hyperedges,
    'input_tokens': new.get('input_tokens', 0),
    'output_tokens': new.get('output_tokens', 0),
}
(out_dir / '.graphify_semantic.json').write_text(json.dumps(merged, indent=2, ensure_ascii=False), encoding='utf-8')
print(f'Semantic: {len(deduped)} nodes, {len(all_edges)} edges')
