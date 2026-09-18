const fs = require('fs');
const tasks = JSON.parse(fs.readFileSync('blueprints/miautrix-mail-server/tasks.json', 'utf8'));

console.log('| ID | Title | Status | Epic |');
console.log('|----|-------|--------|------|');
for (const t of tasks) {
    const statusIcon = t.status === 'done' ? '✅' : (t.status === 'in_progress' ? '🔄' : '⏳');
    console.log(`| ${t.id} | ${t.title} | ${statusIcon} ${t.status} | ${t.epic} |`);
}
