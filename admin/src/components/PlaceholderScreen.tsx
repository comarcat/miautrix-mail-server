import React from 'react';

export const PlaceholderScreen: React.FC<{ name: string; description: string }> = ({ name, description }) => (
  <div className="screen-container placeholder-screen">
    <div className="screen-header">
      <div className="header-titles">
        <h1 className="screen-title">{name}</h1>
        <p className="screen-desc">{description}</p>
      </div>
    </div>
    <div className="card empty-state-card">
      <p>This module is under active development and will be fully implemented in its dedicated task milestone.</p>
    </div>
  </div>
);
