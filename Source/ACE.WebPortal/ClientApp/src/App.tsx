import { useEffect } from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import WorldViewer from './components/WorldViewer'
import PropertyExplorer from './components/PropertyExplorer'
import LookupTables from './components/LookupTables'
import ServerParams from './components/ServerParams'
import LoginPage from './components/LoginPage'
import CharacterList from './components/CharacterList'
import CharacterDetail from './components/CharacterDetail'
import { useAuthStore } from './store/useAuthStore'
import PlayerList from './components/PlayerList'
import MainLayout from './layouts/MainLayout'
import { FileCode, Terminal, Activity } from 'lucide-react'

function App() {
  const { isAuthenticated, bootstrap, accessLevel, isPortalDisabled, isBootstrapping } = useAuthStore()
  
  useEffect(() => {
    bootstrap()
  }, [bootstrap])

  if (isBootstrapping || isPortalDisabled) {
    const label = isPortalDisabled ? "Portal Disabled" : "Restoring Session...";
    return (
      <div className="min-h-screen bg-neutral-950 flex flex-col items-center justify-center p-8 text-center space-y-6">
        <Activity className="w-10 h-10 text-blue-500 animate-spin opacity-50" />
        <div className="text-[10px] font-black text-neutral-600 uppercase tracking-[0.2em]">{label}</div>
      </div>
    );
  }

  if (!isAuthenticated) {
    return <LoginPage />
  }

  const isAdmin = accessLevel !== null && accessLevel > 0;

  return (
    <MainLayout>
      <Routes>
        {/* Default Route */}
        <Route path="/" element={<Navigate to="/characters" replace />} />

        {/* Character Routes */}
        <Route path="/characters" element={<CharacterList />} />
        <Route path="/characters/:guid/:tab?" element={<CharacterDetail />} />

        {/* Admin Routes */}
        {isAdmin && (
          <>
            <Route path="/players" element={<PlayerList />} />
            <Route path="/players/:guid/:tab?" element={<CharacterDetail />} />
            <Route path="/map" element={<div className="w-full h-full bg-neutral-950"><WorldViewer /></div>} />
            <Route path="/properties" element={<PropertyExplorer navigateToEnum={(name) => window.location.hash = `#/lookup?enum=${name}`} />} />
            <Route path="/lookup" element={<LookupTables />} />
            <Route path="/params" element={<ServerParams />} />
            
            {/* Modules Under Construction */}
            <Route path="/weenie" element={<ConstructionPlaceholder icon={<FileCode className="w-8 h-8" />} label="Weenie Editor" />} />
            <Route path="/console" element={<ConstructionPlaceholder icon={<Terminal className="w-8 h-8" />} label="Console" />} />
          </>
        )}

        {/* Fallback */}
        <Route path="*" element={<Navigate to="/characters" replace />} />
      </Routes>
    </MainLayout>
  )
}

function ConstructionPlaceholder({ icon, label }: { icon: React.ReactNode, label: string }) {
  return (
    <div className="absolute inset-0 bg-neutral-900 flex flex-col items-center justify-center space-y-4">
      <div className="w-16 h-16 rounded-2xl bg-neutral-800 flex items-center justify-center text-neutral-600 border border-neutral-700/50">
        {icon}
      </div>
      <div className="text-neutral-500 text-sm font-medium uppercase tracking-widest">
        {label} Under Construction
      </div>
      <p className="text-neutral-600 text-xs px-12 text-center max-w-sm leading-relaxed">
        This module is currently being finalized. Management tools for this section will be live soon.
      </p>
    </div>
  )
}

export default App;
