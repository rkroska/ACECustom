import { useState, useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ChevronLeft, LogOut, Award, Star, Info, Activity, Package, ScrollText, MapPin } from 'lucide-react'
import { api } from '../services/api'
import { Character } from '../types'
import CharacterStats from './character/CharacterStats'
import CharacterSkills from './character/CharacterSkills'
import CharacterInventory from './character/CharacterInventory'
import CharacterStamps from './character/CharacterStamps'
import Modal from './common/Modal'
import TabButton from './common/TabButton'

interface CharacterDetailData extends Character {
}

export default function CharacterDetail() {
  const { guid: guidParam, tab: tabParam } = useParams<{ guid: string; tab: string }>()
  const navigate = useNavigate()
  const guid = parseInt(guidParam || '0')
  
  const [detail, setDetail] = useState<CharacterDetailData | null>(null)
  const VALID_TABS = ['general', 'skills', 'inventory', 'stamps']
  const [activeTab, setActiveTab] = useState(tabParam && VALID_TABS.includes(tabParam) ? tabParam : 'general')
  const [isLoading, setIsLoading] = useState(true)
  const [isLoggingOut, setIsLoggingOut] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  // Modal state
  const [modalMode, setModalMode] = useState<'none' | 'logout' | 'error'>('none')
  const [modalTitle, setModalTitle] = useState('')
  const [modalDesc, setModalDesc] = useState('')

  // Sync activeTab with URL param - normalize invalid tabs to 'general'
  useEffect(() => {
    const nextTab = tabParam && VALID_TABS.includes(tabParam) ? tabParam : 'general'
    if (activeTab !== nextTab) {
      setActiveTab(nextTab)
    }
  }, [tabParam])

  const handleTabChange = (tab: string) => {
    setActiveTab(tab)
    // Maintain the current route context (characters vs players)
    const context = window.location.hash.includes('/players/') ? 'players' : 'characters'
    navigate(`/${context}/${guid}/${tab}`)
  }

  const navigateBack = () => {
    const context = window.location.hash.includes('/players/') ? 'players' : 'characters'
    navigate(`/${context}`)
  }

  useEffect(() => {
    let isAborted = false;

    // Early-exit for invalid guids to avoid stuck loading state
    if (Number.isNaN(guid) || guid <= 0) {
      setIsLoading(false);
      setDetail(null);
      return;
    }

    const runFetch = async () => {
      try {
        setIsLoading(true);
        setError(null);
        // Reset detail to avoid showing stale data from previous guid
        setDetail(null);

        const data = await api.get<CharacterDetailData>(`/api/character/detail/${guid}`);
        
        if (!isAborted) {
          setDetail(data);
        }
      } catch (err) {
        if (!isAborted) {
          setError(err instanceof Error ? err.message : 'Unknown error');
        }
      } finally {
        if (!isAborted) {
          setIsLoading(false);
        }
      }
    };

    runFetch();

    return () => {
      isAborted = true;
    };
  }, [guid]);

  const handleLogout = async () => {
    if (!detail?.isOnline || isLoggingOut) return
    
    setModalTitle('Force Logout')
    setModalDesc(`Are you sure you want to force logout ${detail.name}? This will immediately terminate the player's session.`)
    setModalMode('logout')
  }

  const executeLogout = async () => {
    try {
      setIsLoggingOut(true)
      await api.post(`/api/character/logout/${guid}`)
      
      // Optimistic update
      setDetail(prev => prev ? { ...prev, isOnline: false } : null)
      return true
    } catch (err) {
      setModalTitle('Logout Error')
      setModalDesc(err instanceof Error ? err.message : 'Failed to process logout request.')
      setModalMode('error')
      return false // Return false to prevent the Modal from auto-closing on failure
    } finally {
      setIsLoggingOut(false)
    }
  }

  if (isLoading) {
    return (
      <div className="flex-1 flex items-center justify-center min-h-[400px]">
        <Activity className="w-8 h-8 text-blue-500 animate-spin" />
      </div>
    )
  }

  if (error || !detail) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center text-red-400 p-8">
        <Info className="w-12 h-12 mb-4 opacity-50" />
        <h2 className="text-xl font-bold mb-2">Error Loading Character</h2>
        <p className="text-sm opacity-80 mb-6">{error || 'Character not found'}</p>
        <button 
          onClick={navigateBack}
          className="px-6 py-2 bg-neutral-800 hover:bg-neutral-700 text-white rounded-xl transition-colors"
        >
          Back to List
        </button>
      </div>
    )
  }

  return (
    <div className="flex-1 flex flex-col h-full bg-neutral-900 border-l border-neutral-800/10">
      <Modal 
        isOpen={modalMode !== 'none'} 
        onClose={() => setModalMode('none')}
        title={modalTitle}
        description={modalDesc}
        type={modalMode === 'logout' ? 'confirm' : 'error'}
        confirmLabel={modalMode === 'logout' ? 'Force Logout' : 'OK'}
        onConfirm={modalMode === 'logout' ? executeLogout : () => setModalMode('none')}
      />

      {/* Top Header / Breadcrumb */}
      <div className="p-4 border-b border-neutral-800 bg-neutral-950/50 flex items-center justify-between shrink-0">
        <button 
          onClick={navigateBack}
          className="flex items-center gap-2 text-neutral-400 hover:text-white transition-colors text-sm font-medium pr-4"
        >
          <ChevronLeft className="w-4 h-4" />
          Back to {window.location.hash.includes('/players/') ? 'Players' : 'Characters'}
        </button>
        
        <div className="flex items-center gap-2">
          <div className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[10px] font-bold uppercase tracking-wider ${
            detail.isOnline 
              ? 'bg-green-500/10 text-green-500 border border-green-500/20' 
              : 'bg-neutral-800/50 text-neutral-500 border border-neutral-800'
          }`}>
            <div className={`w-1.5 h-1.5 rounded-full ${detail.isOnline ? 'bg-green-500 animate-pulse' : 'bg-neutral-600'}`} />
            {detail.isOnline ? 'Active Session' : 'Logged Out'}
          </div>
          
          {detail.isOnline && (
            <button 
              onClick={handleLogout}
              disabled={isLoggingOut}
              className="flex items-center gap-2 px-3 py-1.5 bg-red-500/10 hover:bg-red-500 text-red-500 hover:text-white border border-red-500/20 rounded-lg text-xs font-bold transition-all disabled:opacity-50"
            >
              <LogOut className={`w-3.5 h-3.5 ${isLoggingOut ? 'animate-pulse' : ''}`} />
              FORCE LOGOUT
            </button>
          )}
        </div>
      </div>

      {/* Hero Header */}
      <div className="px-8 pt-8 pb-4 bg-gradient-to-b from-neutral-950 to-neutral-900">
        <div className="max-w-6xl mx-auto">
          <div className="space-y-3">
            <div className="group flex items-baseline gap-4 w-fit px-3">
              <h1 className="text-3xl font-black text-white tracking-tight leading-none">
                {detail.name}
              </h1>
            </div>

            {detail.location && (
              <div className="flex items-center gap-2 text-blue-400/90 text-[10px] font-bold uppercase tracking-widest bg-blue-500/5 px-3 py-1.5 rounded-lg border border-blue-500/10 w-fit">
                <MapPin className="w-3.5 h-3.5" />
                <span>{detail.location.name || detail.location.hex || 'Unknown Location'}</span>
                {detail.location.coordinates && (
                  <span className="text-neutral-500 font-medium normal-case tracking-normal ml-0.5">
                    ({detail.location.coordinates})
                  </span>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Traditional Tabs Navigation */}
      <div className="shrink-0 px-8 bg-neutral-900">
        <div className="max-w-6xl mx-auto">
          <div className="flex items-end gap-1 border-b border-neutral-800">
            <TabButton 
              label="General" 
              active={activeTab === 'general'} 
              onClick={() => handleTabChange('general')} 
              icon={<Award className="w-3.5 h-3.5" />} 
            />
            <TabButton 
              label="Skills" 
              active={activeTab === 'skills'} 
              onClick={() => handleTabChange('skills')} 
              icon={<Star className="w-3.5 h-3.5 text-yellow-500/60" />} 
            />
            <TabButton 
              label="Inventory" 
              active={activeTab === 'inventory'} 
              onClick={() => handleTabChange('inventory')} 
              icon={<Package className="w-3.5 h-3.5 text-blue-400" />} 
            />
            <TabButton 
              label="Stamps" 
              active={activeTab === 'stamps'} 
              onClick={() => handleTabChange('stamps')} 
              icon={<ScrollText className="w-3.5 h-3.5 text-neutral-400" />} 
            />
          </div>
        </div>
      </div>

      {/* Module Content */}
      <div className="flex-1 overflow-y-auto custom-scrollbar px-8 mt-8">
        <div className="max-w-6xl mx-auto pb-12">
          {activeTab === 'general' && <CharacterStats guid={guid} />}
          {activeTab === 'skills' && <CharacterSkills guid={guid} />}
          {activeTab === 'inventory' && <CharacterInventory guid={guid} />}
          {activeTab === 'stamps' && <CharacterStamps guid={guid} />}
        </div>
      </div>
    </div>
  )
}

