import React, { useState } from 'react';
import { Layout, ScreenId } from './components/Layout';
import { DashboardScreen } from './components/DashboardScreen';
import { QueueScreen } from './components/QueueScreen';
import { RuleDesignerScreen } from './components/RuleDesignerScreen';
import { PlaceholderScreen } from './components/PlaceholderScreen';

const PLACEHOLDER_DESCRIPTIONS: Record<string, string> = {
  'mail-flow': 'Visualize and control inbound and outbound message routing across connectors and providers.',
  users: 'Manage tenant users, mailbox assignments, and membership-based roles.',
  domains: 'Provision and verify accepted domains, DKIM keys, and DNS configuration.',
  identity: 'Configure single sign-on, service principals, and identity providers.',
  security: 'Define security policies, transport encryption, and API token management.',
  'anti-spam': 'Tune Rspamd scoring thresholds, whitelists, and blocklists.',
  'anti-malware': 'Configure malware scanning policies and attachment inspection.',
  quarantine: 'Review and release or discard quarantined messages.',
  logs: 'Search and audit operational logs across the mail engine.',
  reports: 'Generate delivery, security, and compliance reports.',
  backup: 'Configure and monitor tenant data backups.',
  system: 'Inspect server health, storage, and background services.',
  licensing: 'Review edition limits, MFA, and backup entitlements.',
};

export const App: React.FC = () => {
  const [activeScreen, setActiveScreen] = useState<ScreenId>('dashboard');

  const renderScreen = () => {
    switch (activeScreen) {
      case 'dashboard':
        return <DashboardScreen />;
      case 'queue':
        return <QueueScreen />;
      case 'rule-designer':
        return <RuleDesignerScreen />;
      default:
        return (
          <PlaceholderScreen
            name={activeScreen}
            description={PLACEHOLDER_DESCRIPTIONS[activeScreen] || 'Under development.'}
          />
        );
    }
  };

  return (
    <Layout activeScreen={activeScreen} onNavigate={setActiveScreen}>
      {renderScreen()}
    </Layout>
  );
};
