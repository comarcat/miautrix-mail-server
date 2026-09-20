import React from 'react';
import { ALL_DOMAINS } from '../utils/domainFilter';

export type ScreenId =
  | 'dashboard'
  | 'mail-flow'
  | 'users'
  | 'domains'
  | 'identity'
  | 'security'
  | 'anti-spam'
  | 'anti-malware'
  | 'queue'
  | 'quarantine'
  | 'logs'
  | 'reports'
  | 'backup'
  | 'system'
  | 'licensing'
  | 'rule-designer';

interface MenuItem {
  id: ScreenId;
  label: string;
  icon: string;
  group: string;
}

export const MENU_ITEMS: MenuItem[] = [
  { id: 'dashboard', label: 'Dashboard', icon: 'grid', group: 'Operations' },
  { id: 'mail-flow', label: 'Mail Flow', icon: 'flow', group: 'Operations' },
  { id: 'queue', label: 'Queue', icon: 'list', group: 'Operations' },
  { id: 'rule-designer', label: 'Rule Designer', icon: 'branch', group: 'Operations' },
  { id: 'users', label: 'Users', icon: 'users', group: 'Directory' },
  { id: 'domains', label: 'Domains', icon: 'globe', group: 'Directory' },
  { id: 'identity', label: 'Identity', icon: 'shield', group: 'Directory' },
  { id: 'security', label: 'Security', icon: 'lock', group: 'Protection' },
  { id: 'anti-spam', label: 'Anti-Spam', icon: 'ban', group: 'Protection' },
  { id: 'anti-malware', label: 'Anti-Malware', icon: 'bug', group: 'Protection' },
  { id: 'quarantine', label: 'Quarantine', icon: 'archive', group: 'Protection' },
  { id: 'logs', label: 'Logs', icon: 'file-text', group: 'Observability' },
  { id: 'reports', label: 'Reports', icon: 'bar-chart', group: 'Observability' },
  { id: 'backup', label: 'Backup', icon: 'database', group: 'Platform' },
  { id: 'system', label: 'System', icon: 'server', group: 'Platform' },
  { id: 'licensing', label: 'Licensing', icon: 'key', group: 'Platform' },
];

const MenuIcon: React.FC<{ name: string }> = ({ name }) => {
  const paths: Record<string, React.ReactNode> = {
    grid: <><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></>,
    flow: <><circle cx="6" cy="6" r="3" /><circle cx="18" cy="18" r="3" /><path d="M9 6h6a3 3 0 0 1 3 3v6" /></>,
    list: <><line x1="8" y1="6" x2="21" y2="6" /><line x1="8" y1="12" x2="21" y2="12" /><line x1="8" y1="18" x2="21" y2="18" /><line x1="3" y1="6" x2="3.01" y2="6" /><line x1="3" y1="12" x2="3.01" y2="12" /><line x1="3" y1="18" x2="3.01" y2="18" /></>,
    branch: <><line x1="6" y1="3" x2="6" y2="15" /><circle cx="18" cy="6" r="3" /><circle cx="6" cy="18" r="3" /><path d="M18 9a9 9 0 0 1-9 9" /></>,
    users: <><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" /><circle cx="9" cy="7" r="4" /><path d="M23 21v-2a4 4 0 0 0-3-3.87" /><path d="M16 3.13a4 4 0 0 1 0 7.75" /></>,
    globe: <><circle cx="12" cy="12" r="10" /><line x1="2" y1="12" x2="22" y2="12" /><path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" /></>,
    shield: <><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" /></>,
    lock: <><rect x="3" y="11" width="18" height="11" rx="2" ry="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /></>,
    ban: <><circle cx="12" cy="12" r="10" /><line x1="4.93" y1="4.93" x2="19.07" y2="19.07" /></>,
    bug: <><path d="M8 2v4M16 2v4M9 7h6a3 3 0 0 1 3 3v4a6 6 0 0 1-12 0v-4a3 3 0 0 1 3-3z" /><path d="M3 13h3M18 13h3" /></>,
    archive: <><polyline points="21 8 21 21 3 21 3 8" /><rect x="1" y="3" width="22" height="5" rx="1" /><line x1="10" y1="12" x2="14" y2="12" /></>,
    'file-text': <><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" /><polyline points="14 2 14 8 20 8" /><line x1="16" y1="13" x2="8" y2="13" /><line x1="16" y1="17" x2="8" y2="17" /></>,
    'bar-chart': <><line x1="12" y1="20" x2="12" y2="10" /><line x1="18" y1="20" x2="18" y2="4" /><line x1="6" y1="20" x2="6" y2="16" /></>,
    database: <><ellipse cx="12" cy="5" rx="9" ry="3" /><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3" /><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5" /></>,
    server: <><rect x="2" y="2" width="20" height="8" rx="2" ry="2" /><rect x="2" y="14" width="20" height="8" rx="2" ry="2" /><line x1="6" y1="6" x2="6.01" y2="6" /><line x1="6" y1="18" x2="6.01" y2="18" /></>,
    key: <><path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4" /></>,
  };

  return (
    <svg
      width="16"
      height="16"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name] || paths.grid}
    </svg>
  );
};

interface LayoutProps {
  activeScreen: ScreenId;
  onNavigate: (screen: ScreenId) => void;
  children: React.ReactNode;
  userEmail?: string;
  onLogout?: () => void;
  domains?: string[];
  selectedDomain?: string;
  onDomainChange?: (domain: string) => void;
}

export const Layout: React.FC<LayoutProps> = ({
  activeScreen,
  onNavigate,
  children,
  userEmail = 'admin@internal.domain',
  onLogout,
  domains = [],
  selectedDomain = ALL_DOMAINS,
  onDomainChange,
}) => {
  const groups = Array.from(new Set(MENU_ITEMS.map((i) => i.group)));

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <div className="brand-mark" aria-hidden="true">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8">
              <path d="M3 7l9 6 9-6" />
              <rect x="3" y="5" width="18" height="14" rx="2" />
            </svg>
          </div>
          <div className="brand-text">
            <span className="brand-name">Miautrix</span>
            <span className="brand-sub">Mail Server</span>
          </div>
        </div>

        <nav className="sidebar-nav" aria-label="Admin sections">
          {groups.map((group) => (
            <div key={group} className="nav-group">
              <div className="nav-group-label">{group}</div>
              {MENU_ITEMS.filter((i) => i.group === group).map((item) => (
                <button
                  key={item.id}
                  className={`nav-item ${activeScreen === item.id ? 'nav-item-active' : ''}`}
                  onClick={() => onNavigate(item.id)}
                  aria-current={activeScreen === item.id ? 'page' : undefined}
                  data-testid={`nav-${item.id}`}
                >
                  <span className="nav-icon"><MenuIcon name={item.icon} /></span>
                  <span className="nav-label">{item.label}</span>
                </button>
              ))}
            </div>
          ))}
        </nav>
      </aside>

      <div className="main-area">
        <header className="topbar">
          <div className="topbar-left">
            <span className="topbar-screen">{MENU_ITEMS.find((i) => i.id === activeScreen)?.label || 'Dashboard'}</span>
          </div>
          <div className="topbar-right">
            <label className="tenant-picker">
              <span className="tenant-picker-label">Domain</span>
              <select
                className="input-select input-select-sm"
                value={selectedDomain}
                onChange={(e) => onDomainChange?.(e.target.value)}
                aria-label="Active domain filter"
                data-testid="domain-filter"
              >
                <option value={ALL_DOMAINS}>All Domains</option>
                {domains.map((d) => (
                  <option key={d} value={d}>{d}</option>
                ))}
              </select>
            </label>
            <span className="status-dot status-dot-ok" title="Mail engine healthy" />
            <span className="topbar-user">{userEmail}</span>
            {onLogout && (
              <button
                type="button"
                className="btn btn-secondary"
                style={{ padding: '4px 10px', fontSize: '12px' }}
                onClick={onLogout}
              >
                Sign Out
              </button>
            )}
          </div>
        </header>

        <main className="content-area">{children}</main>
      </div>
    </div>
  );
};
