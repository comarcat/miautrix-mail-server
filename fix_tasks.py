import json

with open('blueprints/miautrix-mail-server/tasks.json', 'r', encoding='utf-8') as f:
    data = json.load(f)

# Extract tasks
old_tasks = data.get('tasks', [])
new_tasks = []

# Map old tasks to new schema strictly
for t in old_tasks:
    # ensure "epic" points to raw slug (e.g. 01-core-platform)
    if t['epic'] == 'E1': new_epic = '01-core-platform'
    elif t['epic'] == 'E2': new_epic = '02-mail-transport'
    elif t['epic'] == 'E3': new_epic = '03-surfaces'
    else: new_epic = '04-operations'

    new_task = {
        "id": t["id"],
        "title": t["title"],
        "epic": new_epic,
        "dependencies": t.get("depends_on", []),
        "priority": "p0" if "T1" in t["id"] or "T2" in t["id"] else "p1",  # Simplified mock priority
        "acceptance": [t["acceptance"]] if isinstance(t["acceptance"], str) else t["acceptance"],
        "verify": [t["verify"]] if isinstance(t["verify"], str) else t["verify"],
        "checkpoint": f"step-{t['id'].replace('T', '').zfill(2)}-{t['checkpoint']}", # strict tag align
        "files": ["src/**/*"], # placeholder, would be explicitly populated
        "status": "pending"
    }
    
    # Specific files mapping based on the text context
    if "T1" in t["id"]: new_task["files"] = ["Miautrix.Mail.sln", "Directory.Build.props"]
    elif "T2" in t["id"]: new_task["files"] = ["src/Miautrix.Mail.Domain/**/*", "src/Miautrix.Mail.Persistence/**/*", "migrations/**/*"]
    elif "T15" in t["id"]: new_task["files"] = ["apps/admin/**/*"]

    new_tasks.append(new_task)

# Output valid JSON ARRAY
with open('blueprints/miautrix-mail-server/tasks.json', 'w', encoding='utf-8') as f:
    json.dump(new_tasks, f, indent=2)

print("tasks.json converted successfully to an Array strict schema.")
