import { useEffect } from 'react'

import { Routes, Route, Navigate, useLocation } from 'react-router-dom'

import WorldViewer from './components/WorldViewer'

import PropertyExplorer from './components/PropertyExplorer'

import LookupTables from './components/LookupTables'

import ServerParams from './components/ServerParams'

import ServerEvents from './components/ServerEvents'

import ItemSearch from './components/ItemSearch'

import StampSearch from './components/StampSearch'

import LoginPage from './components/LoginPage'

import CharacterList from './components/CharacterList'

import CharacterDetail from './components/CharacterDetail'

import Leaderboards from './components/Leaderboards'

import { useAuthStore } from './store/useAuthStore'

import PlayerList from './components/PlayerList'

import CombatCalculator from './components/CombatCalculator'

import QuestBuilder from './components/quest-builder/QuestBuilder'

import PortalSecurity from './components/PortalSecurity'

import AuditLog from './components/AuditLog'
import CorpseFinder from './components/CorpseFinder'

import MainLayout from './layouts/MainLayout'

import ProtectedRoute from './components/common/ProtectedRoute'

import PatchNotesPublicShell from './components/patch-notes/PatchNotesPublicShell'

import PatchNotesList from './components/patch-notes/PatchNotesList'

import PatchNoteDetail from './components/patch-notes/PatchNoteDetail'

import PatchNotesManage from './components/patch-notes/PatchNotesManage'

import { isPublicPatchNotesPath } from './utils/patchNotesPaths'

import { FileCode, Terminal, Activity } from 'lucide-react'



function App() {

  const { isAuthenticated, bootstrap, isPortalDisabled, isBootstrapping } = useAuthStore()

  const location = useLocation()

  const publicPatchNotes = isPublicPatchNotesPath(location.pathname)

  

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



  if (!isAuthenticated && publicPatchNotes) {

    return (

      <PatchNotesPublicShell>

        <Routes>

          <Route path="/patch-notes" element={<PatchNotesList />} />

          <Route path="/patch-notes/:slug" element={<PatchNoteDetail />} />

          <Route path="*" element={<Navigate to="/patch-notes" replace />} />

        </Routes>

      </PatchNotesPublicShell>

    )

  }



  if (!isAuthenticated) {

    return <LoginPage />

  }



  return (

    <MainLayout>

      <Routes>

        <Route path="/" element={<Navigate to="/characters" replace />} />



        <Route path="/patch-notes/manage" element={
          <ProtectedRoute pageKey="patch-notes-admin">
            <PatchNotesManage />
          </ProtectedRoute>
        } />

        <Route path="/patch-notes" element={<PatchNotesList />} />

        <Route path="/patch-notes/:slug" element={<PatchNoteDetail />} />



        <Route path="/characters" element={

          <ProtectedRoute pageKey="characters">

            <CharacterList />

          </ProtectedRoute>

        } />

        <Route path="/characters/:guid/:tab?" element={

          <ProtectedRoute pageKey="characters">

            <CharacterDetail />

          </ProtectedRoute>

        } />



        <Route path="/leaderboards" element={

          <ProtectedRoute pageKey="leaderboards">

            <Leaderboards />

          </ProtectedRoute>

        } />



        <Route path="/players" element={

          <ProtectedRoute pageKey="players">

            <PlayerList />

          </ProtectedRoute>

        } />

        <Route path="/players/:guid/:tab?" element={

          <ProtectedRoute pageKey="players">

            <CharacterDetail />

          </ProtectedRoute>

        } />



        <Route path="/audit" element={

          <ProtectedRoute pageKey="audit-log">

            <AuditLog />

          </ProtectedRoute>

        } />

        <Route path="/corpse-finder" element={

          <ProtectedRoute pageKey="corpse-finder">

            <CorpseFinder />

          </ProtectedRoute>

        } />



        <Route path="/map" element={

          <ProtectedRoute pageKey="map">

            <div className="w-full h-full bg-neutral-950"><WorldViewer /></div>

          </ProtectedRoute>

        } />



        <Route path="/properties" element={

          <ProtectedRoute pageKey="properties">

            <PropertyExplorer navigateToEnum={(name) => window.location.hash = `#/lookup?enum=${name}`} />

          </ProtectedRoute>

        } />



        <Route path="/lookup" element={

          <ProtectedRoute pageKey="lookup">

            <LookupTables />

          </ProtectedRoute>

        } />



        <Route path="/params" element={

          <ProtectedRoute pageKey="params">

            <ServerParams />

          </ProtectedRoute>

        } />



        <Route path="/events" element={

          <ProtectedRoute pageKey="events">

            <ServerEvents />

          </ProtectedRoute>

        } />



        <Route path="/items" element={

          <ProtectedRoute pageKey="items">

            <ItemSearch />

          </ProtectedRoute>

        } />



        <Route path="/stamps" element={

          <ProtectedRoute pageKey="stamps">

            <StampSearch />

          </ProtectedRoute>

        } />



        <Route path="/combat-calculator" element={

          <ProtectedRoute pageKey="combat-calculator">

            <CombatCalculator />

          </ProtectedRoute>

        } />



        <Route path="/quest-builder" element={

          <ProtectedRoute pageKey="quest-builder">

            <QuestBuilder />

          </ProtectedRoute>

        } />



        <Route path="/portal-security" element={

          <ProtectedRoute pageKey="portal-security">

            <PortalSecurity />

          </ProtectedRoute>

        } />



        <Route path="/weenie" element={

          <ProtectedRoute pageKey="weenie">

            <ConstructionPlaceholder icon={<FileCode className="w-8 h-8" />} label="Weenie Editor" />

          </ProtectedRoute>

        } />



        <Route path="/console" element={

          <ProtectedRoute pageKey="console">

            <ConstructionPlaceholder icon={<Terminal className="w-8 h-8" />} label="Console" />

          </ProtectedRoute>

        } />

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


