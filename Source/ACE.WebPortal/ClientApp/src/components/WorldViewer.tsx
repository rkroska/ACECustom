import { type FC } from 'react';

const WorldViewer: FC = () => {
  return (
    <div style={{ 
      width: '100%', height: '100%', display: 'flex', 
      flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
      background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)', 
      color: '#f8fafc', fontFamily: 'system-ui, -apple-system, sans-serif'
    }}>
      <div style={{ 
        padding: '3rem', borderRadius: '24px', background: 'rgba(30, 41, 59, 0.5)', 
        backdropFilter: 'blur(20px)', border: '1px solid rgba(255, 255, 255, 0.1)',
        textAlign: 'center', boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5)'
      }}>
        <div style={{ 
          width: '64px', height: '64px', margin: '0 auto 1.5rem', 
          background: '#3b82f6', borderRadius: '16px', display: 'flex', 
          alignItems: 'center', justifyContent: 'center', fontSize: '2rem'
        }}>
          🗺️
        </div>
        <h1 style={{ fontSize: '2.25rem', fontWeight: 800, marginBottom: '0.75rem', letterSpacing: '-0.025em' }}>
          3D World Viewer
        </h1>
        <p style={{ color: '#94a3b8', fontSize: '1.125rem', maxWidth: '400px', lineHeight: '1.6' }}>
          Our high-performance terrain streaming engine is currently under development.
        </p>
        <div style={{ 
          marginTop: '2rem', display: 'inline-flex', alignItems: 'center', 
          gap: '0.5rem', padding: '0.5rem 1rem', background: 'rgba(59, 130, 246, 0.1)', 
          color: '#60a5fa', borderRadius: '9999px', fontSize: '0.875rem', fontWeight: 600
        }}>
          <span style={{ width: '8px', height: '8px', background: '#3b82f6', borderRadius: '50%' }} />
          Web Portal v1.0
        </div>
      </div>
    </div>
  );
};

export default WorldViewer;
