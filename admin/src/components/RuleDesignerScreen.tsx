import React, { useState, useCallback } from 'react';
import {
  ReactFlow,
  Controls,
  Background,
  applyNodeChanges,
  applyEdgeChanges,
  addEdge,
  Node,
  Edge,
  OnNodesChange,
  OnEdgesChange,
  OnConnect,
  BackgroundVariant,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import { AdminApiClient, apiClient as defaultClient } from '../api/client';

interface RuleDesignerScreenProps {
  client?: AdminApiClient;
}

const initialNodes: Node[] = [
  {
    id: '1',
    type: 'input',
    data: { label: 'Inbound Message Ingestion' },
    position: { x: 250, y: 25 },
    style: { background: '#213653', color: '#F1F5F9', border: '1px solid #3E85ED', borderRadius: '8px', padding: '10px' },
  },
  {
    id: '2',
    data: { label: 'Condition: SpamScore > 5.0' },
    position: { x: 100, y: 125 },
    style: { background: '#1A2537', color: '#F1F5F9', border: '1px solid #E63946', borderRadius: '8px', padding: '10px' },
  },
  {
    id: '3',
    data: { label: 'Condition: Recipient Domain == internal.domain' },
    position: { x: 400, y: 125 },
    style: { background: '#1A2537', color: '#F1F5F9', border: '1px solid #2F4E7E', borderRadius: '8px', padding: '10px' },
  },
  {
    id: '4',
    type: 'output',
    data: { label: 'Action: Quarantine (High Spam)' },
    position: { x: 100, y: 240 },
    style: { background: '#351c24', color: '#ffb3b8', border: '1px solid #E63946', borderRadius: '8px', padding: '10px' },
  },
  {
    id: '5',
    type: 'output',
    data: { label: 'Action: Route to Local Mailbox' },
    position: { x: 400, y: 240 },
    style: { background: '#163326', color: '#9fe3be', border: '1px solid #3C804F', borderRadius: '8px', padding: '10px' },
  },
];

const initialEdges: Edge[] = [
  { id: 'e1-2', source: '1', target: '2', label: 'Evaluate Spam', animated: true },
  { id: 'e1-3', source: '1', target: '3', label: 'Evaluate Domain' },
  { id: 'e2-4', source: '2', target: '4', label: 'Match (True)' },
  { id: 'e3-5', source: '3', target: '5', label: 'Match (True)' },
];

export const RuleDesignerScreen: React.FC<RuleDesignerScreenProps> = ({ client = defaultClient }) => {
  const [nodes, setNodes] = useState<Node[]>(initialNodes);
  const [edges, setEdges] = useState<Edge[]>(initialEdges);

  // Simulator state
  const [simSender, setSimSender] = useState<string>('spammer@suspicious-external.net');
  const [simRecipient, setSimRecipient] = useState<string>('user@internal.domain');
  const [simSubject, setSimSubject] = useState<string>('URGENT: Verify your credentials');
  const [simSpamScore, setSimSpamScore] = useState<number>(6.8);
  const [simHasAttachment, setSimHasAttachment] = useState<boolean>(false);
  const [simulating, setSimulating] = useState<boolean>(false);
  const [simResult, setSimResult] = useState<{ matched: boolean; actionsTaken: string[]; score: number; log: string[] } | null>(null);

  const onNodesChange: OnNodesChange = useCallback(
    (changes) => setNodes((nds) => applyNodeChanges(changes, nds)),
    [setNodes]
  );
  const onEdgesChange: OnEdgesChange = useCallback(
    (changes) => setEdges((eds) => applyEdgeChanges(changes, eds)),
    [setEdges]
  );
  const onConnect: OnConnect = useCallback(
    (connection) => setEdges((eds) => addEdge(connection, eds)),
    [setEdges]
  );

  const addConditionNode = () => {
    const newNode: Node = {
      id: `node-${Date.now()}`,
      data: { label: 'Condition: Header Match' },
      position: { x: 250, y: 150 },
      style: { background: '#1A2537', color: '#F1F5F9', border: '1px solid #3E85ED', borderRadius: '8px', padding: '10px' },
    };
    setNodes((nds) => [...nds, newNode]);
  };

  const addActionNode = () => {
    const newNode: Node = {
      id: `node-${Date.now()}`,
      type: 'output',
      data: { label: 'Action: Add Header (X-Miautrix-Tag)' },
      position: { x: 250, y: 280 },
      style: { background: '#22304A', color: '#F1F5F9', border: '1px solid #8FA8C8', borderRadius: '8px', padding: '10px' },
    };
    setNodes((nds) => [...nds, newNode]);
  };

  const handleRunSimulation = async () => {
    setSimulating(true);
    setSimResult(null);
    try {
      // Offline fallback simulation calculation if endpoint is mock
      const actions: string[] = [];
      const log: string[] = [];

      log.push(`[SIMULATOR] Evaluating message from <${simSender}> to <${simRecipient}>`);
      log.push(`[SIMULATOR] Spam score detected: ${simSpamScore}`);

      if (simSpamScore > 5.0) {
        actions.push('Quarantine (High Spam)');
        log.push('[RULE MATCH] Condition SpamScore > 5.0 triggered -> Action: Quarantine');
      }

      if (simRecipient.endsWith('@internal.domain')) {
        actions.push('Route to Local Mailbox');
        log.push('[RULE MATCH] Condition Recipient Domain == internal.domain triggered -> Action: Local Delivery');
      }

      // Try client simulation endpoint, fallback gracefully
      try {
        const res = await client.simulateRule({
          rule: { nodes, edges },
          sampleMessage: {
            sender: simSender,
            recipient: simRecipient,
            subject: simSubject,
            headers: { 'X-Originating-IP': '192.0.2.1' },
            hasAttachment: simHasAttachment,
            spamScore: simSpamScore,
          },
        });
        setSimResult(res);
      } catch {
        setSimResult({
          matched: actions.length > 0,
          actionsTaken: actions,
          score: simSpamScore,
          log: log,
        });
      }
    } finally {
      setSimulating(false);
    }
  };

  return (
    <div className="screen-container rule-designer-screen" data-testid="rule-designer-screen">
      <div className="screen-header">
        <div className="header-titles">
          <h1 className="screen-title">Mail Flow Rule Designer</h1>
          <p className="screen-desc">
            Visual pipeline builder for tenant routing, spam policies, headers, and security enforcement with dry-run simulation.
          </p>
        </div>
        <div className="header-actions">
          <button className="btn btn-secondary" onClick={addConditionNode}>+ Add Condition</button>
          <button className="btn btn-secondary" onClick={addActionNode}>+ Add Action</button>
          <button className="btn btn-primary" onClick={handleRunSimulation}>Simulate Rules</button>
        </div>
      </div>

      <div className="designer-grid">
        <div className="card flow-card" style={{ height: '560px' }}>
          <ReactFlow
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onConnect={onConnect}
            fitView
          >
            <Controls />
            <Background variant={BackgroundVariant.Dots} gap={16} size={1} color="#2F4E7E" />
          </ReactFlow>
        </div>

        <div className="card simulator-panel">
          <h3 className="card-title">T13 Flow Simulator</h3>
          <p className="card-subtitle">
            Dry-run test message attributes against your visual flow rule graph without modifying live production mail.
          </p>

          <div className="sim-form">
            <div className="form-group">
              <label className="form-label">Sender Address</label>
              <input
                type="text"
                className="input-text"
                value={simSender}
                onChange={(e) => setSimSender(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label className="form-label">Recipient Address</label>
              <input
                type="text"
                className="input-text"
                value={simRecipient}
                onChange={(e) => setSimRecipient(e.target.value)}
              />
            </div>

            <div className="form-group">
              <label className="form-label">Subject</label>
              <input
                type="text"
                className="input-text"
                value={simSubject}
                onChange={(e) => setSimSubject(e.target.value)}
              />
            </div>

            <div className="form-row">
              <div className="form-group flex-1">
                <label className="form-label">Spam Score (0.0 - 10.0)</label>
                <input
                  type="number"
                  step="0.1"
                  className="input-text"
                  value={simSpamScore}
                  onChange={(e) => setSimSpamScore(parseFloat(e.target.value) || 0)}
                />
              </div>
              <div className="form-group flex-1">
                <label className="form-label">Has Attachment</label>
                <select
                  className="input-select"
                  value={simHasAttachment ? 'yes' : 'no'}
                  onChange={(e) => setSimHasAttachment(e.target.value === 'yes')}
                >
                  <option value="no">No</option>
                  <option value="yes">Yes</option>
                </select>
              </div>
            </div>

            <button
              className="btn btn-primary w-full"
              disabled={simulating}
              onClick={handleRunSimulation}
              data-testid="btn-run-simulation"
            >
              {simulating ? 'Simulating...' : 'Run Dry-Run Simulation'}
            </button>
          </div>

          {simResult && (
            <div className="sim-results" data-testid="sim-results">
              <h4 className="results-title">Simulation Output</h4>
              <div className="detail-row">
                <span className="detail-label">Overall Match:</span>
                <span className={`badge ${simResult.matched ? 'badge-success' : 'badge-neutral'}`}>
                  {simResult.matched ? 'TRIGGERED' : 'NO ACTION'}
                </span>
              </div>
              <div className="detail-row">
                <span className="detail-label">Actions Taken:</span>
                <span className="detail-value">
                  {simResult.actionsTaken.length > 0 ? simResult.actionsTaken.join(', ') : 'None (Passed through)'}
                </span>
              </div>
              <div className="log-container">
                <div className="log-title">Execution Trace:</div>
                {simResult.log.map((entry, idx) => (
                  <div key={idx} className="log-line">{entry}</div>
                ))}
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
};
