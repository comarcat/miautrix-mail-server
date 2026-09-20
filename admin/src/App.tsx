import React, { useState, useEffect } from 'react';
import { Layout, ScreenId } from './components/Layout';
import { DashboardScreen } from './components/DashboardScreen';
import { QueueScreen } from './components/QueueScreen';
import { RuleDesignerScreen } from './components/RuleDesignerScreen';
import { MailFlowScreen } from './components/MailFlowScreen';
import { UsersScreen } from './components/UsersScreen';
import { DomainsScreen } from './components/DomainsScreen';
import { IdentityScreen } from './components/IdentityScreen';
import { SecurityScreen } from './components/SecurityScreen';
import { AntiSpamScreen } from './components/AntiSpamScreen';
import { AntiMalwareScreen } from './components/AntiMalwareScreen';
import { QuarantineScreen } from './components/QuarantineScreen';
import { LogsScreen } from './components/LogsScreen';
import { ReportsScreen } from './components/ReportsScreen';
import { BackupScreen } from './components/BackupScreen';
import { SystemScreen } from './components/SystemScreen';
import { LicensingScreen } from './components/LicensingScreen';
import { LoginScreen } from './components/LoginScreen';
import { apiClient } from './api/client';
import { ALL_DOMAINS } from './utils/domainFilter';

export const App: React.FC = () => {
  const [activeScreen, setActiveScreen] = useState<ScreenId>('dashboard');
  const [isAuthenticated, setIsAuthenticated] = useState<boolean>(false);
  const [isInitializing, setIsInitializing] = useState<boolean>(true);
  const [currentUserEmail, setCurrentUserEmail] = useState<string>('admin@internal.domain');
  const [domains, setDomains] = useState<string[]>([]);
  const [selectedDomain, setSelectedDomain] = useState<string>(ALL_DOMAINS);

  const loadDomains = React.useCallback(async () => {
    try {
      const res = await apiClient.getDomains();
      setDomains((res.data || []).map((d) => d.name));
    } catch {
      setDomains([]);
    }
  }, []);

  useEffect(() => {
    const initAuth = async () => {
      const token = apiClient.getToken();
      if (!token) {
        setIsAuthenticated(false);
        setIsInitializing(false);
        return;
      }

      try {
        const res = await apiClient.me();
        if (res.data?.email) {
          setCurrentUserEmail(res.data.email);
        }
        setIsAuthenticated(true);
        loadDomains();
      } catch (err) {
        // me() throws on 401, which also clears the token in apiClient
        setIsAuthenticated(false);
      } finally {
        setIsInitializing(false);
      }
    };

    initAuth();

    const handleAuthExpired = () => {
      setIsAuthenticated(false);
    };

    window.addEventListener('miautrix:auth:expired', handleAuthExpired);
    return () => {
      window.removeEventListener('miautrix:auth:expired', handleAuthExpired);
    };
  }, []);

  const handleLogout = async () => {
    await apiClient.logout();
    setIsAuthenticated(false);
  };

  const renderScreen = () => {
    switch (activeScreen) {
      case 'dashboard':
        return <DashboardScreen />;
      case 'queue':
        return <QueueScreen domainFilter={selectedDomain} />;
      case 'rule-designer':
        return <RuleDesignerScreen />;
      case 'mail-flow':
        return <MailFlowScreen onOpenDesigner={() => setActiveScreen('rule-designer')} />;
      case 'users':
        return <UsersScreen domainFilter={selectedDomain} />;
      case 'domains':
        return <DomainsScreen />;
      case 'identity':
        return <IdentityScreen />;
      case 'security':
        return <SecurityScreen />;
      case 'anti-spam':
        return <AntiSpamScreen />;
      case 'anti-malware':
        return <AntiMalwareScreen />;
      case 'quarantine':
        return <QuarantineScreen domainFilter={selectedDomain} />;
      case 'logs':
        return <LogsScreen />;
      case 'reports':
        return <ReportsScreen />;
      case 'backup':
        return <BackupScreen />;
      case 'system':
        return <SystemScreen />;
      case 'licensing':
        return <LicensingScreen />;
      default:
        return <DashboardScreen />;
    }
  };

  if (isInitializing) {
    return (
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100vh', background: 'var(--page)', color: 'var(--text)' }}>
        Loading...
      </div>
    );
  }

  if (!isAuthenticated) {
    return (
      <LoginScreen
        onLoginSuccess={(email) => {
          if (email) setCurrentUserEmail(email);
          setIsAuthenticated(true);
          loadDomains();
        }}
      />
    );
  }

  return (
    <Layout
      activeScreen={activeScreen}
      onNavigate={setActiveScreen}
      userEmail={currentUserEmail}
      onLogout={handleLogout}
      domains={domains}
      selectedDomain={selectedDomain}
      onDomainChange={setSelectedDomain}
    >
      {renderScreen()}
    </Layout>
  );
};
